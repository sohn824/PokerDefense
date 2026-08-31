using System;
using System.Collections.Generic;
using PokerDefense.Poker;
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

        // 라이프나 Chip이 바뀌었을 때 호출되는 이벤트
        // HUD가 구독
        public event Action Changed;

        StageContext stage;

        /**
         * 한 판의 상태
         *
         * Awake에서 만들지 않는다. 다른 컴포넌트의 Awake가 먼저 돌 수 있고
         * 그 순서는 씬 설정에 달려 있어 코드만 봐서는 알 수 없다 - 처음 묻는 쪽이 만들게 한다
         */
        public StageContext Stage => stage ?? (stage = new StageContext(definition, economy.HeldCardCapacity));

        // 상점 보유 카드
        public IReadOnlyList<Card> HeldCards => Stage.HeldCards;

        // 결과 화면용 성과 기록
        public RunStats Stats { get; } = new RunStats();

        public EconomyDefinition Economy => economy;

        /**
         * 유지 보너스 (교체 장수에 반비례해서 줄어들음)
         */
        public int HoldBonusFor(int usedExchanges) => economy.HoldBonusFor(usedExchanges);

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

        // 상점에서 카드 한 장 구매
        // 인벤토리가 꽉 찼거나 Chip이 모자라면 false를 반환하고 아무것도 안 바뀜
        public bool TryBuyShopCard(Card card)
        {
            if (Stage.CanHoldMoreCards == false)
            {
                return false;
            }

            // TrySpendChip이 Stats 기록과 Changed 이벤트 통지도 수행
            if (TrySpendChip(economy.ShopCardPrice) == false)
            {
                return false;
            }

            Stage.TryAddHeldCard(card);
            Changed?.Invoke();
            return true;
        }

        // 손패에 놓아 인벤토리 보유 카드를 소모한다
        public bool RemoveHeldCard(Card card)
        {
            if (Stage.TryRemoveHeldCard(card) == false)
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
