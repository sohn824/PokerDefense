using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    public enum HandOddsState
    {
        Idle,
        Computing,
        Ready,
        Failed,
    }

    /**
     * HandOddsWorker
     *
     * 교체 확률 계산의 실행·취소·완료 상태를 관리
     * Submit / Poll / Cancel / Dispose는 소유 스레드에서만 호출
     * 계산 작업은 복사된 값만 읽으며 Unity 객체를 참조하지 않음
     */
    public sealed class HandOddsWorker : IDisposable
    {
        // 계산량에 따른 실행 정책은 교체 상한과 별도로 유지한다.
        const int ParallelSlotThreshold = 3;
        const int MaxParallelism = 2;

        sealed class Completion
        {
            public List<HandOddsEntry> Result;
            public Exception Error;
            public bool Cancelled;
        }

        readonly Func<HandOddsRequest, CancellationToken, List<HandOddsEntry>> calculate;
        HandOddsRequest currentRequest;  // 화면이 지금 보고 싶어하는 요청 (최신)
        HandOddsRequest pendingRequest;  // 아직 작업을 시작하지 못한 대기 요청
        HandOddsRequest activeRequest;   // 지금 워커 스레드에서 실제로 도는 요청
        CancellationTokenSource cancellationSource;
        Task<Completion> calculationTask;
        bool disposed;

        public HandOddsWorker(Func<HandOddsRequest, CancellationToken, List<HandOddsEntry>> calculate = null)
        {
            this.calculate = calculate ?? Calculate;
        }

        public HandOddsState State { get; private set; }
        public IReadOnlyList<HandOddsEntry> Result { get; private set; }
        public Exception Error { get; private set; }
        public HandOddsSource? Source => currentRequest?.Source;

        // 같은 요청은 재실행하지 않고, 실행 중이면 대기 요청을 최신 하나로 교체
        public void Submit(HandOddsRequest request)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(HandOddsWorker));
            }

            if (request == null)
            {
                Cancel();
                return;
            }

            if (request.Matches(currentRequest))
            {
                return;
            }

            currentRequest = request;
            pendingRequest = request;
            Result = null;
            Error = null;
            State = HandOddsState.Computing;
            cancellationSource?.Cancel();
            StartPending();
        }

        // 완료 여부를 먼저 확인하므로 메인 스레드를 기다리게 하지 않는다.
        public bool Poll()
        {
            if (disposed || calculationTask == null || calculationTask.IsCompleted == false)
            {
                return false;
            }

            Completion completion = calculationTask.GetAwaiter().GetResult();
            bool publish = ReferenceEquals(activeRequest, currentRequest) && completion.Cancelled == false;
            calculationTask = null;
            activeRequest = null;
            cancellationSource.Dispose();
            cancellationSource = null;

            if (publish)
            {
                Result = completion.Result?.AsReadOnly();
                Error = completion.Error;
                State = Error == null ? HandOddsState.Ready : HandOddsState.Failed;
            }

            StartPending();
            return publish;
        }

        // 이미 끝난 계산도 현재 요청에서 분리해 화면에 반영되지 않게 함
        public void Cancel()
        {
            if (disposed)
            {
                return;
            }

            currentRequest = null;
            pendingRequest = null;
            Result = null;
            Error = null;
            State = HandOddsState.Idle;
            cancellationSource?.Cancel();
        }

        void StartPending()
        {
            if (calculationTask != null || pendingRequest == null)
            {
                return;
            }

            activeRequest = pendingRequest;
            pendingRequest = null;
            HandOddsRequest request = activeRequest;
            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;
            Func<HandOddsRequest, CancellationToken, List<HandOddsEntry>> compute = calculate;

            // 예외는 결과로 회수한다. 씬 종료로 Poll이 사라져도 미관찰 Task 예외가 남지 않는다.
            calculationTask = Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    List<HandOddsEntry> result = compute(request, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new Completion { Result = result };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new Completion { Cancelled = true };
                }
                catch (Exception error)
                {
                    return new Completion { Error = error };
                }
            });
        }

        static List<HandOddsEntry> Calculate(HandOddsRequest request, CancellationToken cancellationToken)
        {
            // 적은 조합은 순차 계산. 두 코어까지만 사용해 게임 루프와의 CPU 경합을 제한한다.
            return request.Indices.Count >= ParallelSlotThreshold && Environment.ProcessorCount > 1
                ? HandOdds.FindParallel(request.Hand, request.Unseen, request.Indices, MaxParallelism, cancellationToken)
                : HandOdds.Find(request.Hand, request.Unseen, request.Indices, cancellationToken);
        }

        // 메인 스레드에서 종료를 기다리지 않고 작업 완료 뒤 취소 자원을 해제
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Cancel();
            disposed = true;
            CancellationTokenSource source = cancellationSource;
            if (calculationTask != null)
            {
                calculationTask.ContinueWith(_ => source.Dispose(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else
            {
                source?.Dispose();
            }

            calculationTask = null;
            cancellationSource = null;
        }
    }
}
