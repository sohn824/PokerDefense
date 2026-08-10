using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageController
     *
     * 한 판 동안 유지되는 상태(라이프·Chip·웨이브 진행)의 주인
     *
     * CombatController가 들고 있었으나 Chip이 생기면서 분리했다
     * Chip은 전투가 아니라 카드·배치 단계에서 오가므로 PlacementController도 접근해야 한다
     */
    public sealed class StageController : MonoBehaviour
    {
        [SerializeField] StageDefinition definition;
        [SerializeField] EconomyDefinition economy;
        [SerializeField] PerkTable perkTable;

        // 라이프나 Chip이 바뀌었다. HUD가 구독한다
        public event Action Changed;

        StageContext stage;

        /**
         * 한 판의 상태
         *
         * Awake에서 만들지 않는다. 다른 컴포넌트의 Awake가 먼저 돌 수 있고
         * 그 순서는 씬 설정에 달려 있어 코드만 봐서는 알 수 없다 - 처음 묻는 쪽이 만들게 한다
         */
        public StageContext Stage => stage ?? (stage = new StageContext(definition, perkTable));

        // 결과 화면용 성과 기록 (DESIGN §9.6). 규칙에 관여하지 않아 StageContext와 분리했다
        public RunStats Stats { get; } = new RunStats();

        public EconomyDefinition Economy => economy;

        public PerkTable PerkTable => perkTable;

        /**
         * 특전까지 반영한 유지 보너스 (DESIGN §9.1 + §11)
         *
         * 지급하는 쪽(GameFlowController)과 미리 보여주는 쪽(RoundScreen)이
         * 반드시 같은 수를 내야 하므로 조합을 여기 한 곳에 둔다
         */
        public int HoldBonusFor(int usedExchanges)
            => Stage.Perks.HoldBonus(economy.HoldBonusFor(usedExchanges), usedExchanges);

        public void AddPerk(PerkId id)
        {
            Stage.Perks.Add(id);
            Changed?.Invoke();
        }

        public void AddChip(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Stage.AddChip(amount);
            Changed?.Invoke();
        }

        public bool TrySpendChip(int amount)
        {
            if (Stage.TrySpendChip(amount) == false)
            {
                return false;
            }

            Stats.RecordChipSpent(amount);
            Changed?.Invoke();
            return true;
        }

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
