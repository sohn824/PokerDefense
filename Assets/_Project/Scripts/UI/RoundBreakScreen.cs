using System.Collections;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RoundBreakScreen
     *
     * 전투가 끝나면 라운드 결과 화면을 띄우고, 닫힐 때 다음 라운드로 넘기는 클래스
     */
    public sealed class RoundBreakScreen : MonoBehaviour
    {
        // 탭하지 않아도 닫히는 시간
        const float AutoAdvanceSeconds = 1.8f;

        [SerializeField] GameFlowController flow;
        [SerializeField] GameObject panel;
        [SerializeField] Button dismissArea;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] TMP_Text hintLabel;

        Coroutine autoAdvance;

        void Awake()
        {
            flow.RoundSettled += OnRoundSettled;
            dismissArea.onClick.AddListener(Proceed);
            panel.SetActive(false);
        }

        void OnDestroy()
        {
            flow.RoundSettled -= OnRoundSettled;
        }

        void OnRoundSettled(GameFlowController.RoundSummary s)
        {
            titleLabel.text = s.Cleared ? $"웨이브 {s.Wave} 클리어" : $"웨이브 {s.Wave} 시간 초과";
            bodyLabel.text = BodyText(s);
            hintLabel.text = "탭하여 계속";

            panel.SetActive(true);

            if (autoAdvance != null)
            {
                StopCoroutine(autoAdvance);
            }

            autoAdvance = StartCoroutine(AutoAdvance());
        }

        // 라운드 결과 화면의 본문 문구를 만듦 (전투 결과 요약, 다음 Act 예고)
        static string BodyText(GameFlowController.RoundSummary s)
        {
            string line;

            if (s.Cleared)
            {
                line = s.JokerGained > 0 ? $"적 전멸 · 승급권 +{s.JokerGained}" : "적 전멸";
            }
            else
            {
                line = $"라이프 -{s.LifeLost}";

                string left = EnemyTypeNames.Unresolved(s.Unresolved);

                if (string.IsNullOrEmpty(left) == false)
                {
                    line += $"\n<size=80%>남은 적: {left}</size>";
                }
            }

            if (s.NextAct > 0)
            {
                line += $"\n<size=80%>다음 · ACT {s.NextAct} · {ActSubtitle(s.NextAct)}</size>";
            }

            return line;
        }

        // 새 Act의 적 예고 문구 (Act 2~5, 그 외는 빈 문자열)
        static string ActSubtitle(int act)
        {
            switch (act)
            {
                case 2:
                    return "여러 종류의 적 출현";
                case 3:
                    return "체력이 높은 적 출현";
                case 4:
                    return "다수의 적 출현";
                case 5:
                    return "총력전 예고";
                default:
                    return string.Empty;
            }
        }

        IEnumerator AutoAdvance()
        {
            yield return new WaitForSeconds(AutoAdvanceSeconds);
            Proceed();
        }

        // 패널을 닫고 DismissBreak로 다음 라운드 시작 (자동 타이머와 탭 중 먼저 온 쪽만 처리)
        void Proceed()
        {
            if (panel.activeSelf == false)
            {
                return;
            }

            if (autoAdvance != null)
            {
                StopCoroutine(autoAdvance);
                autoAdvance = null;
            }

            panel.SetActive(false);
            flow.DismissBreak();
        }
    }
}
