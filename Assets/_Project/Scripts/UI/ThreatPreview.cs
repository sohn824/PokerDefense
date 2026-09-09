using System.Collections.Generic;
using System.Text;
using PokerDefense.Game;
using TMPro;
using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * ThreatPreview
     *
     * 전투 전(교체 · 준비) 단계에서 이번 웨이브의 적 구성을 한 줄로 보여준다
     * "어떤 족보를 원하는가"를 정할 근거를 주는 것이 목적
     * 전투가 시작되면 숨긴다
     */
    public sealed class ThreatPreview : MonoBehaviour
    {
        [SerializeField] StageController stage;
        [SerializeField] CombatController combat;
        [SerializeField] GameFlowController flow;

        [Tooltip("켜고 끌 바 전체")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text label;

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
                label.text = Describe(wave);
            }
        }

        // 적 종류별로 수를 묶어 설명 문자열을 만들어 반환
        static string Describe(WaveDefinition wave)
        {
            int normal = 0, swarm = 0, runner = 0, tank = 0, boss = 0;

            IReadOnlyList<WaveDefinition.SpawnEntry> entries = wave.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                WaveDefinition.SpawnEntry e = entries[i];

                if (e.enemy == null)
                {
                    continue;
                }

                switch (e.enemy.Type)
                {
                    case EnemyType.Swarm:
                        swarm += e.count;
                        break;
                    case EnemyType.Runner:
                        runner += e.count;
                        break;
                    case EnemyType.Tank:
                        tank += e.count;
                        break;
                    case EnemyType.Boss:
                        boss += e.count;
                        break;
                    default:
                        normal += e.count;
                        break;
                }
            }

            StringBuilder sb = new StringBuilder("이번 적");

            if (normal > 0)
            {
                sb.Append(" · 일반 ").Append(normal);
            }

            if (swarm > 0)
            {
                sb.Append(" · 스웜 ").Append(swarm);
            }

            if (runner > 0)
            {
                sb.Append(" · 러너 ").Append(runner);
            }

            if (tank > 0)
            {
                sb.Append(" · 탱커 ").Append(tank);
            }

            if (boss > 0)
            {
                sb.Append(boss > 1 ? $" · <color=#E8894F>보스 {boss}</color>" : " · <color=#E8894F>보스</color>");
            }

            return sb.ToString();
        }
    }
}
