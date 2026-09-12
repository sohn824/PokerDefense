using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RoundRecap
     *
     * 다음 준비 단계의 상황 카드에 "직전 전투" 접이 한 줄을 띄운다
     * 탭하면 남은 적 상세를 펼치고, 새 전투가 시작되면 접어 다음 결과로 교체한다
     * 표시만 하고 정산·보상은 다시 계산하지 않는다 (GameFlowController.LastRound 스냅샷을 읽을 뿐)
     */
    public sealed class RoundRecap : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] CombatController combat;

        [Tooltip("켜고 끌 줄 전체")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text label;

        [Tooltip("줄을 탭하면 상세를 펼치고 접는다")]
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
                        && flow.IsFinished == false
                        && flow.ShopCards == null;

            // 전투가 다시 시작되면 접힌 상태로 되돌린다
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
                    string left = EnemyTypeNames.Remnants(s.Remnants);

                    if (string.IsNullOrEmpty(left) == false)
                    {
                        line += $"\n<size=76%>남은 적: {left}</size>";
                    }
                }

                if (s.JokerGained > 0)
                {
                    line += $"\n<size=76%>조커 +{s.JokerGained}</size>";
                }
            }

            label.text = line;
        }
    }
}
