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
     * 전투 한 판이 끝나면 결과 요약을 잠깐 보여주고 다음 단계(상점 또는 다음 손패)로 넘긴다
     */
    public sealed class RoundBreakScreen : MonoBehaviour
    {
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

        static string BodyText(GameFlowController.RoundSummary s)
        {
            string line = s.Cleared
                ? s.JokerGained > 0 ? $"적 전멸 · 조커 +{s.JokerGained}" : "적 전멸"
                : $"{s.EnemiesLeft}마리 남음 · 라이프 -{s.LifeLost}";

            if (s.NextAct > 0)
            {
                line += $"\n<size=80%>다음 · ACT {s.NextAct} · {ActSubtitle(s.NextAct)}</size>";
            }

            return line;
        }

        // 그 구간에 들어오는 위협을 한마디로
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
