using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CombatController
     *
     * CombatContext(흐름도)를 씬에서 구동
     * CombatContext는 순수 C# 클래스이므로 CombatController가 Update에서 Time.deltaTime을 넘겨 줌
     * 전투가 끝나면 결과를 StageContext에 반영해 라이프를 깎고 다음 웨이브로 넘김
     */
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] StageController stageController;

        CombatContext combat;

        public event Action<CombatContext> CombatStarted;
        public event Action<CombatOutcome, int> CombatFinished;

        public StageContext Stage => stageController.Stage;

        public CombatContext Combat => combat;

        public bool IsFighting => combat != null && combat.Outcome == CombatOutcome.InProgress;

        /**
         * 전투로 넘어갈 수 있는 조건
         *
         * - Place 페이즈여야 함
         * - 배치 대기 유닛이 없어야 함
         */
        public bool CanStart => IsFighting == false
                                && round.Phase == RoundPhase.Place
                                && placement.Pending == null
                                && Stage.IsGameOver == false
                                && Stage.IsAllWavesCleared == false;

        public void StartCombat()
        {
            if (CanStart == false)
            {
                return;
            }

            combat = new CombatContext(placement.Board, Stage.CurrentWave);
            CombatStarted?.Invoke(combat);
        }

        void Update()
        {
            if (IsFighting == false)
            {
                return;
            }

            combat.Tick(Time.deltaTime);

            if (combat.Outcome == CombatOutcome.InProgress)
            {
                return;
            }

            int unresolved = combat.UnresolvedEnemies;
            stageController.ApplyCombatResult(combat.Outcome, unresolved, combat.UnresolvedBosses);
            CombatFinished?.Invoke(combat.Outcome, unresolved);
        }
    }
}
