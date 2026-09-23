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
     *
     * 스레드를 강제로 끊으면 뒷정리를 못 하므로, 신호를 주고 스스로 빠져나오게 하는 협력적 취소를 사용
     * 취소를 걸어도 이미 도는 계산은 다음 확인 지점까지 돌고, 그 전에 끝나버릴 수도 있음
     * 결과를 내보낼지는 Poll()에서 요청 참조를 비교해 결정, 토큰은 버려질 계산을 빨리 멈추는 용도
     */
    public sealed class HandOddsWorker : IDisposable
    {
        // 슬롯 몇 개부터 경우의 수 계산을 병렬로 돌릴지 정하는 상수
        const int ParallelSlotThreshold = 3;

        // 병렬 처리할 때 스레드를 동시에 몇 개까지 쓸지 정하는 상수
        const int MaxParallelism = 2;

        // 작업 결과 데이터 클래스
        sealed class Completion
        {
            public List<HandOddsEntry> Result;
            public Exception Error;
            public bool Cancelled;
        }

        // 교체 확률 계산 함수
        readonly Func<HandOddsRequest, CancellationToken, List<HandOddsEntry>> calculate;

        HandOddsRequest currentRequest;  // 화면에 표시해야 하는 최신 요청
        HandOddsRequest pendingRequest;  // 아직 작업을 시작하지 못한 대기 요청
        HandOddsRequest activeRequest;   // 지금 Worker 스레드에서 실제로 도는 요청
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

        // Submit - 화면에 표시할 확률 계산을 Worker 스레드에 맡김
        public void Submit(HandOddsRequest request)
        {
            // 끝난 Worker는 결과를 돌려줄 수 없으므로 예외를 던짐
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(HandOddsWorker));
            }

            // request가 null이면 표시할 게 없다는 뜻이므로 취소로 처리
            if (request == null)
            {
                Cancel();
                return;
            }

            // UI가 같은 요청을 다시 올린 경우 재계산하지 않고 early return
            if (request.Matches(currentRequest))
            {
                return;
            }

            currentRequest = request;
            pendingRequest = request;      // 아직 시작 못 한 이전 대기 요청이 있어도 그냥 버림
            Result = null;
            Error = null;
            State = HandOddsState.Computing;
            cancellationSource?.Cancel();  // 돌고 있는 계산이 있다면 그만하라고 알림 (협력적 취소)
            StartPending();                // 돌고 있는 작업이 없었다면 바로 시작, 있으면 Poll이 그 작업을 정리한 뒤 시작
        }

        // Poll - 끝난 계산 결과를 가져와 화면에 반영할지 정함
        public bool Poll()
        {
            // 완료를 통보받는 대신 매 프레임 여기서 확인(콜백으로 받으면 Worker 스레드에서 돌아 Unity API를 못 씀)
            // 아직 안 끝났으면 메인 스레드가 Worker 스레드를 기다리지 않고 early return
            if (disposed || calculationTask == null || calculationTask.IsCompleted == false)
            {
                return false;
            }

            Completion completion = calculationTask.GetAwaiter().GetResult();

            // 끝난 계산이 지금도 화면이 원하는 요청인지 확인 (그 사이 손패 선택이 바뀌었으면 계산한 결과를 버림)
            bool isPublishNeeded = ReferenceEquals(activeRequest, currentRequest) && completion.Cancelled == false;
            calculationTask = null;        // 다음 요청을 받을 수 있게 자리를 비움
            activeRequest = null;
            cancellationSource.Dispose();
            cancellationSource = null;

            // 화면에 표시할 요청이 맞으면 결과를 화면에 반영
            if (isPublishNeeded)
            {
                Result = completion.Result?.AsReadOnly();
                Error = completion.Error;
                State = Error == null ? HandOddsState.Ready : HandOddsState.Failed;
            }

            StartPending();                // 자리를 비웠으니 대기 요청이 있으면 여기서 바로 시작
            return isPublishNeeded;
        }

        // Cancel - 이미 끝난 계산도 현재 요청에서 분리해 화면에 반영되지 않게 함
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

        // StartPending - 대기 요청이 있으면 Worker 스레드에서 계산을 시작
        void StartPending()
        {
            // 작업은 한 번에 하나만 돌림
            // 이미 도는 작업이 있거나 대기 요청이 없으면 early return
            if (calculationTask != null || pendingRequest == null)
            {
                return;
            }

            // 대기 요청을 가져와 Worker 스레드에서 계산 시작
            activeRequest = pendingRequest;
            pendingRequest = null;
            // 작업이 끝나면 Poll이 이 필드들을 비우므로, 람다가 쓸 값은 지역 변수에 미리 담아 백업
            HandOddsRequest request = activeRequest;
            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;
            Func<HandOddsRequest, CancellationToken, List<HandOddsEntry>> compute = calculate;

            // 예외를 Task 밖으로 던지지 않고 Completion(결과 데이터 클래스)에 담음
            calculationTask = Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();   // 시작 전에 이미 취소됐는지
                    List<HandOddsEntry> result = compute(request, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();   // 계산 도중에 취소됐는지
                    return new Completion { Result = result };
                }
                // cancellationToken이 부른 취소만 정상 종료로 봄
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

        // 손패 교체 확률 계산 - 병렬 처리 여부를 판단해 HandOdds.FindParallel 또는 HandOdds.Find 호출
        static List<HandOddsEntry> Calculate(HandOddsRequest request, CancellationToken cancellationToken)
        {
            // 적은 조합은 그냥 순차로 처리함 (적은 조합인 경우 병렬로 작업을 쪼개고 합치는 비용이 계산보다 크기 때문)
            return request.Indices.Count >= ParallelSlotThreshold && Environment.ProcessorCount > 1
                ? HandOdds.FindParallel(request.Hand, request.Unseen, request.Indices, MaxParallelism, cancellationToken)
                : HandOdds.Find(request.Hand, request.Unseen, request.Indices, cancellationToken);
        }

        // Dispose - 메인 스레드에서 종료를 기다리지 않고 작업 완료 뒤 취소 자원을 해제
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Cancel();
            disposed = true;

            // 아래에서 필드를 비우므로 지역 변수에 담아 넘김 (콜백이 도는 시점에는 필드가 이미 null)
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
