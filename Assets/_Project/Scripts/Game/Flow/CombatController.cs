using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CombatController
     *
     * CombatContext를 씬에서 구동한다. Update에서 Time.deltaTime을 넘기는 것이 전부다
     * 전투가 끝나면 결과를 StageContext에 반영해 라이프를 깎고 다음 웨이브로 넘긴다
     *
     * M4에서는 StartCombat을 버튼이 부른다. 라운드 루프와의 자동 결합은 M5다
     */
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] PlacementController placement;
        [SerializeField] StageDefinition stageDefinition;

        CombatContext combat;

        public event Action<CombatContext> CombatStarted;
        public event Action<CombatOutcome, int> CombatFinished;

        public StageContext Stage { get; private set; }

        public CombatContext Combat => combat;

        public bool IsFighting => combat != null && combat.Outcome == CombatOutcome.InProgress;

        public bool CanStart => IsFighting == false
                                && Stage.IsGameOver == false
                                && Stage.IsAllWavesCleared == false;

        void Awake()
        {
            Stage = new StageContext(stageDefinition);
        }

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
            Stage.ApplyResult(combat.Outcome, unresolved);
            CombatFinished?.Invoke(combat.Outcome, unresolved);
        }
    }
}
