using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageController
     *
     *
     */
    public sealed class StageController : MonoBehaviour
    {
        [SerializeField] StageDefinition definition;

        // HUD가 구독
        public event Action Changed;

        StageContext stage;

        /**
         * 한 판의 상태
         *
         * Awake에서 만들지 않는다. 다른 컴포넌트의 Awake가 먼저 돌 수 있고
         * 그 순서는 씬 설정에 달려 있어 코드만 봐서는 알 수 없다 - 처음 묻는 쪽이 만들게 한다
         */
        public StageContext Stage => stage ?? (stage = new StageContext(definition));

        // 결과 화면용 성과 기록
        public RunStats Stats { get; } = new RunStats();

        public bool TryUseJoker()
        {
            if (Stage.TryUseJoker() == false)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public void ApplyCombatResult(CombatOutcome outcome, int unresolvedEnemies, int unresolvedBosses)
        {
            Stage.ApplyResult(outcome, unresolvedEnemies, unresolvedBosses);
            Changed?.Invoke();
        }
    }
}
