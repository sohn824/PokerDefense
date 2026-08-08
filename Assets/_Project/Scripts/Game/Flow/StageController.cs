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

        // 라이프나 Chip이 바뀌었다. HUD가 구독한다
        public event Action Changed;

        public StageContext Stage { get; private set; }

        public EconomyDefinition Economy => economy;

        void Awake()
        {
            Stage = new StageContext(definition);
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

            Changed?.Invoke();
            return true;
        }

        public void ApplyCombatResult(CombatOutcome outcome, int unresolvedEnemies)
        {
            Stage.ApplyResult(outcome, unresolvedEnemies);
            Changed?.Invoke();
        }
    }
}
