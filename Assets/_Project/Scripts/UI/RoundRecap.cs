using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RoundRecap
     *
     * 교체·배치 단계에서 직전 전투 결과를 접이식 한 줄로 보여주는 클래스
     * 탭하면 남은 적·승급권 상세를 펼치고, 전투 중에는 숨기면서 접힌 상태로 되돌림
     * GameFlowController.LastRound를 읽어 표시함
     */
    public sealed class RoundRecap : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] CombatController combat;

        [Tooltip("켜고 끌 줄 전체")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text label;

        [Tooltip("줄을 탭하면 상세를 펼치고 접음")]
        [SerializeField] Button toggle;

        bool expanded;

        void Awake()
        {
            toggle.onClick.AddListener(Toggle);
        }

        void Toggle()
        {
            expanded = !expanded;
            Render();
        }

        void Update()
        {
            bool show = flow.LastRound.HasValue
                        && combat.IsFighting == false
                        && flow.IsFinished == false;

            // 숨길 때는 접힌 상태로 되돌림
            if (show == false)
            {
                expanded = false;
            }

            if (root.activeSelf != show)
            {
                root.SetActive(show);
            }

            if (show)
            {
                Render();
            }
        }

        void Render()
        {
            if (flow.LastRound.HasValue == false)
            {
                return;
            }

            GameFlowController.RoundSummary s = flow.LastRound.Value;

            string line = s.Cleared
                ? $"직전 전투 · 웨이브 {s.Wave} 클리어"
                : $"직전 전투 · 웨이브 {s.Wave} 시간 초과 · 라이프 -{s.LifeLost}";

            line += expanded ? "  ▲" : "  ▼";

            if (expanded)
            {
                if (s.Cleared == false)
                {
                    string left = EnemyTypeNames.Unresolved(s.Unresolved);

                    if (string.IsNullOrEmpty(left) == false)
                    {
                        line += $"\n<size=76%>남은 적: {left}</size>";
                    }
                }

                if (s.JokerGained > 0)
                {
                    line += $"\n<size=76%>승급권 +{s.JokerGained}</size>";
                }
            }

            label.text = line;
        }
    }
}
