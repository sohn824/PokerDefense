using System.Collections.Generic;
using System.Text;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ThreatPreview
     *
     * 전투 전(교체 · 준비) 단계에서 이번 웨이브의 적 구성을 한 줄로 보여준다
     * "어떤 족보를 원하는가"를 정할 근거를 주는 것이 목적
     * 바를 탭하면 종류별 특성 한 줄을 펼치고, 전투가 시작되면 숨긴다
     */
    public sealed class ThreatPreview : MonoBehaviour
    {
        // 표시 순서 (보스는 항상 끝)
        static readonly EnemyType[] Order =
        {
            EnemyType.Normal, EnemyType.Swarm, EnemyType.Runner, EnemyType.Tank, EnemyType.Boss,
        };

        static readonly int TypeCount = System.Enum.GetValues(typeof(EnemyType)).Length;

        [SerializeField] StageController stage;
        [SerializeField] CombatController combat;
        [SerializeField] GameFlowController flow;

        [Tooltip("켜고 끌 바 전체")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text label;

        [Tooltip("바를 탭하면 특성 상세를 펼치고 접는다")]
        [SerializeField] Button toggle;

        // 특성 상세를 펼친 상태인지 (탭으로 토글, 세션 동안 유지)
        bool expanded;

        void Awake()
        {
            toggle.onClick.AddListener(Toggle);
        }

        void Toggle()
        {
            expanded = !expanded;
        }

        void Update()
        {
            StageContext s = stage.Stage;
            WaveDefinition wave = s.CurrentWave;

            bool show = wave != null
                        && combat.IsFighting == false
                        && s.IsGameOver == false
                        && flow.IsFinished == false
                        && flow.ShopCards == null;

            if (root.activeSelf != show)
            {
                root.SetActive(show);
            }

            if (show)
            {
                label.text = Describe(wave, expanded);
            }
        }

        // 적 종류별로 수를 묶고, 펼친 상태면 종류별 특성 한 줄을 덧붙인다
        static string Describe(WaveDefinition wave, bool expanded)
        {
            int[] count = new int[TypeCount];

            IReadOnlyList<WaveDefinition.SpawnEntry> entries = wave.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                WaveDefinition.SpawnEntry e = entries[i];

                if (e.enemy != null)
                {
                    count[(int)e.enemy.Type] += e.count;
                }
            }

            StringBuilder sb = new StringBuilder("이번 적");

            // 특성을 펼칠 만한 특수 종류(일반 제외)가 있는지
            bool hasDetail = false;

            for (int i = 0; i < Order.Length; i++)
            {
                EnemyType type = Order[i];
                int n = count[(int)type];

                if (n <= 0)
                {
                    continue;
                }

                if (type == EnemyType.Boss)
                {
                    sb.Append(n > 1 ? $" · <color=#E8894F>보스 {n}</color>" : " · <color=#E8894F>보스</color>");
                }
                else
                {
                    sb.Append(" · ").Append(EnemyTypeNames.Of(type)).Append(' ').Append(n);
                }

                if (type != EnemyType.Normal)
                {
                    hasDetail = true;
                }
            }

            if (hasDetail == false)
            {
                return sb.ToString();
            }

            sb.Append(expanded ? "  ▲" : "  ▼");

            if (expanded)
            {
                for (int i = 0; i < Order.Length; i++)
                {
                    EnemyType type = Order[i];

                    if (type != EnemyType.Normal && count[(int)type] > 0)
                    {
                        sb.Append("\n<size=76%>").Append(EnemyTypeNames.Of(type)).Append(": ")
                            .Append(EnemyTypeNames.TraitOf(type)).Append("</size>");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
