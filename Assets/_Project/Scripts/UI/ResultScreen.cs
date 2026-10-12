using System.Text;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ResultScreen
     *
     * 게임이 끝나면(클리어 또는 게임 오버) 이번 판 결과를 보여주고, 재시작 버튼으로 새 판을 시작함
     *
     * 재시작하면 이번 판의 진행 상태(웨이브·라이프·덱·보드의 유닛과 적 등)를 하나씩 되돌리지 않고
     * Game 씬을 다시 로드해 통째로 새로 만듦 (상태가 늘어날 때 초기화 누락 방지)
     */
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] Button restartButton;
        [SerializeField] RunReporter reporter;  // 없으면 랭킹 줄을 표시하지 않음

        void Awake()
        {
            restartButton.onClick.AddListener(Restart);
            flow.FlowChanged += Refresh;
            if (reporter != null)
            {
                reporter.StatusChanged += Refresh;
            }

            panel.SetActive(false);
        }

        void Refresh()
        {
            panel.SetActive(flow.IsFinished);

            if (flow.IsFinished == false)
            {
                return;
            }

            StageContext context = stage.Stage;
            RunStats stats = stage.Stats;
            bool cleared = context.IsAllWavesCleared;

            titleLabel.text = cleared ? "스테이지 클리어" : "게임 오버";

            StringBuilder body = new StringBuilder();
            body.AppendLine($"도달 웨이브   {context.WaveIndex} / {context.TotalWaves}");
            body.AppendLine($"최고 족보   {BestHand(stats)}");
            body.AppendLine($"최고 유닛   {BestUnit(stats)}");
            body.AppendLine($"총 소환   {stats.Summons}");
            body.Append($"남은 라이프   {context.Life}");

            if (cleared)
            {
                body.AppendLine();
                body.Append($"클리어 시간   {Duration(flow.ElapsedSeconds)}");
            }
            else if (flow.LastRound.HasValue)
            {
                string left = EnemyTypeNames.Remnants(flow.LastRound.Value.Remnants);

                if (string.IsNullOrEmpty(left) == false)
                {
                    body.AppendLine();
                    body.Append($"마지막 웨이브   남은 적 {left}");
                }
            }

            string ranking = RankingLine();
            if (ranking != null)
            {
                body.AppendLine();
                body.Append(ranking);
            }

            bodyLabel.text = body.ToString();
        }

        // RankingLine - 결과 화면에 넣을 랭킹 줄 문구를 만듦
        // 보여줄 상태가 아니면 null (RunReporter가 없거나 None·Disabled·Skipped일 때)
        string RankingLine()
        {
            if (reporter == null)
            {
                return null;
            }

            switch (reporter.Status)
            {
                case RunReportStatus.Sending:
                    return "랭킹   기록 전송 중...";
                case RunReportStatus.Sent:
                    return $"랭킹   기록 {reporter.Total}개 중 {reporter.Rank}위";
                case RunReportStatus.Offline:
                    return "랭킹   서버 연결 실패 · 다음에 다시 전송";
                case RunReportStatus.Rejected:
                    return "랭킹   서버가 기록을 받지 않음";
                default:
                    return null;
            }
        }

        static string BestHand(RunStats stats)
            => stats.BestHand.HasValue ? HandCategoryNames.Of(stats.BestHand.Value) : "-";

        static string BestUnit(RunStats stats)
            => stats.BestUnit == null
                ? "-"
                : stats.BestUnit.Definition.DisplayName + " " + new string('★', stats.BestUnit.Star);

        static string Duration(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            return $"{total / 60}분 {total % 60}초";
        }

        static void Restart()
        {
            GameSession.Load(GameSession.GameScene);
        }
    }
}
