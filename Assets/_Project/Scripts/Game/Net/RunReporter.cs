using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PokerDefense.Game
{
    public enum RunReportStatus
    {
        None,      // 아직 게임이 끝나지 않음
        Disabled,  // 전송 꺼 둠 (기록은 파일로 남음)
        Skipped,   // 웨이브 스킵을 쓴 판 - 보내지 않음
        Sending,   // 전송 중
        Sent,      // 서버가 받음 (Rank / Total 사용 가능)
        Offline,   // 못 보냄 - 다음 실행 때 다시 보냄
        Rejected,  // 서버가 거부함
    }

    /**
     * RunReporter
     *
     * 게임이 끝나면 결과를 outbox에 저장하고 백그라운드에서 서버로 보냄 (시작할 때는 못 보낸 기록부터)
     * 전송은 Task.Run, 완료 확인과 상태 변경은 Update(메인 스레드)에서
     * 전송이 실패해도 게임 결과는 그대로, 기록은 ACK를 받을 때까지 파일로 남음
     */
    public sealed class RunReporter : MonoBehaviour
    {
        // 기록 하나의 전송 결과
        readonly struct Delivery
        {
            public Delivery(Guid runId, SubmitOutcome outcome)
            {
                RunId = runId;
                Outcome = outcome;
            }

            public Guid RunId { get; }
            public SubmitOutcome Outcome { get; }
        }

        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] bool submitEnabled = true;  // 끄면 파일로 저장만 하고 보내지 않음
        [SerializeField] string host = "127.0.0.1";  // 랭킹 서버 주소
        [SerializeField] int port = 7777;
        [SerializeField] float timeoutSeconds = 3f;  // 연결~응답 전체 제한 시간

        // Status가 바뀔 때 (메인 스레드)
        public event Action StatusChanged;

        // 이번 판 기록의 전송 상태
        public RunReportStatus Status { get; private set; }
        public int Rank { get; private set; }   // 순위 (1부터)
        public int Total { get; private set; }  // 전체 기록 수

        RunOutbox outbox;
        RunSubmitter submitter;
        CancellationTokenSource cancellation;  // 씬을 나갈 때 전송 중단용
        Task<List<Delivery>> sending;          // 지금 도는 전송 루프 (없으면 null)
        bool sendAgain;       // 전송 중에 새 기록이 들어와 한 번 더 돌아야 함
        Guid currentRunId;    // 결과 화면에 보여줄 이번 판 기록
        bool reported;        // 종료 처리를 했는지 (한 판에 한 번)

        // Awake - 대기열·전송기 준비, 게임 종료 구독
        void Awake()
        {
            outbox = new RunOutbox(Path.Combine(Application.persistentDataPath, "RunOutbox"));
            submitter = new RunSubmitter(host, port, TimeSpan.FromSeconds(timeoutSeconds));
            cancellation = new CancellationTokenSource();
            flow.FlowChanged += OnFlowChanged;
        }

        // Start - 지난 실행에서 못 보낸 기록부터 보냄
        void Start()
        {
            if (submitEnabled)
            {
                StartSending();
            }
        }

        // OnFlowChanged - 게임 종료 시 한 번만 결과를 저장하고 전송 시작
        void OnFlowChanged()
        {
            if (flow.IsFinished == false || reported)
            {
                return;
            }

            reported = true;

#if UNITY_EDITOR
            // 웨이브 스킵을 쓴 판은 실제 기록이 아니므로 제외
            if (flow.UsedDevSkip)
            {
                SetStatus(RunReportStatus.Skipped);
                return;
            }
#endif

            // 보내기 전에 파일부터 저장 (꺼져도 다음 실행 때 다시 보냄)
            RunResult result = Capture();
            currentRunId = result.RunId;
            outbox.Save(result);

            if (submitEnabled == false)
            {
                SetStatus(RunReportStatus.Disabled);
                return;
            }

            SetStatus(RunReportStatus.Sending);
            StartSending();
        }

        // Capture - 현재 스테이지 상태로 결과 생성 (runId 새로 발급)
        RunResult Capture()
        {
            StageContext context = stage.Stage;
            RunStats stats = stage.Stats;
            return new RunResult(Guid.NewGuid(), RunResult.CurrentRuleset, context.WaveIndex, context.TotalWaves,
                context.IsAllWavesCleared, context.Life, stats.Summons, stats.BestHand,
                Mathf.RoundToInt(flow.ElapsedSeconds * 1000f));
        }

        // StartSending - 남은 기록을 백그라운드에서 전송
        void StartSending()
        {
            // 전송 루프는 하나만 - 도는 중이면 끝난 뒤 한 번 더 돌게 표시
            if (sending != null)
            {
                sendAgain = true;
                return;
            }

            // 워커 스레드가 MonoBehaviour 필드를 건드리지 않게 지역 변수로 넘김
            RunOutbox box = outbox;
            RunSubmitter client = submitter;
            CancellationToken token = cancellation.Token;
            sending = Task.Run(() => SendPendingAsync(box, client, token));
        }

        // SendPendingAsync - (워커 스레드) 대기열을 오래된 순으로 보내고 결과를 모아 돌려줌
        static async Task<List<Delivery>> SendPendingAsync(RunOutbox box, RunSubmitter client, CancellationToken token)
        {
            List<Delivery> deliveries = new List<Delivery>();
            foreach (RunResult result in box.LoadPending())
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                SubmitOutcome outcome = await client.SubmitAsync(result, token).ConfigureAwait(false);
                deliveries.Add(new Delivery(result.RunId, outcome));

                // 저장됐으면 삭제, 거부됐으면 치움
                if (outcome.IsStored)
                {
                    box.Remove(result.RunId);
                }
                else if (outcome.Status == SubmitStatus.Rejected)
                {
                    box.MarkRejected(result.RunId);
                }
                else
                {
                    // 서버에 안 닿으면 나머지도 안 될 테니 멈추고 다음 기회에
                    break;
                }
            }

            return deliveries;
        }

        // Update - 전송이 끝났으면 메인 스레드에서 결과 반영 (폴링)
        void Update()
        {
            if (sending == null || sending.IsCompleted == false)
            {
                return;
            }

            List<Delivery> deliveries;
            try
            {
                deliveries = sending.GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                // 파일 처리 실패 등 - 기록은 남아 있으니 다음 기회에 다시 시도
                Debug.LogWarning("결과 전송 대기열을 처리하지 못함: " + error.Message);
                deliveries = new List<Delivery>();
            }

            sending = null;
            Apply(deliveries);

            // 도는 사이에 새 기록이 들어왔으면 한 번 더 돌림
            if (sendAgain)
            {
                sendAgain = false;
                StartSending();
            }
        }

        // Apply - 이번 판 기록의 전송 결과를 상태에 반영
        void Apply(List<Delivery> deliveries)
        {
            if (Status != RunReportStatus.Sending)
            {
                return;
            }

            foreach (Delivery delivery in deliveries)
            {
                if (delivery.RunId != currentRunId)
                {
                    continue;
                }

                Rank = delivery.Outcome.Rank;
                Total = delivery.Outcome.Total;
                SetStatus(delivery.Outcome.IsStored ? RunReportStatus.Sent
                    : delivery.Outcome.Status == SubmitStatus.Rejected ? RunReportStatus.Rejected
                    : RunReportStatus.Offline);
                return;
            }

            // 이번 판 기록 전에 시작한 루프였다면 다음 루프에서 보냄
            if (sendAgain)
            {
                return;
            }

            // 앞선 기록에서 연결이 막혀 못 보냄
            SetStatus(RunReportStatus.Offline);
        }

        // SetStatus - 상태를 바꾸고 결과 화면에 알림
        void SetStatus(RunReportStatus status)
        {
            Status = status;
            StatusChanged?.Invoke();
        }

        // OnDestroy - 전송 중단 후 자원 정리
        void OnDestroy()
        {
            flow.FlowChanged -= OnFlowChanged;

            // 기다리지 않고 취소 신호만 줌 - 자원은 작업이 끝난 뒤 정리
            // 못 보낸 기록은 파일로 남아 다음 실행 때 전송
            cancellation.Cancel();
            CancellationTokenSource source = cancellation;
            if (sending != null)
            {
                sending.ContinueWith(_ => source.Dispose(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else
            {
                source.Dispose();
            }
        }
    }
}
