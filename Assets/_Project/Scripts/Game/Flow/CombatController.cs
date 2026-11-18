using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CombatController
     *
     * 웨이브 하나의 전투(CombatContext)를 시작하고 매 프레임 진행시키는 객체
     * CombatContext는 MonoBehaviour가 아닌 순수 C# 객체라 Unity가 Update를 불러 주지 않음
     * 그래서 이 객체의 Update에서 Time.deltaTime을 넘겨 CombatContext.Tick을 대신 부름
     *
     * 전투가 끝나면 결과를 StageController에 반영함
     * 시간 초과 - 라이프 감소
     * 클리어 - 승급권 보상
     * 어느 쪽이든 다음 웨이브로 넘어감
     */
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] StageController stageController;

        // 전투가 시작될 때 발생 (인자: 방금 시작한 전투를 진행하는 CombatContext)
        // 구독 - RunJournal, ActionBarController, AudioManager, CombatScreen
        public event Action<CombatContext> CombatStarted;

        // 전투가 끝날 때 발생 (인자: 전투 결과, 처치하지 못한 적 수)
        // 이때는 결과가 StageController에 이미 반영돼 Stage가 다음 웨이브를 가리키므로,
        // 직전 웨이브 정보는 방금 끝난 CombatContext에서 읽어야 함 (예: LatestCombatContext.Wave.JokerReward)
        // 구독 - GameFlowController, RunJournal, ActionBarController, AudioManager, CombatScreen, BalanceSimRunner
        public event Action<CombatOutcome, int> CombatFinished;

        public StageContext Stage => stageController.Stage;

        // 지금 또는 직전 전투 context (전투가 끝나도 다음 StartCombat 전까지 남아 결과 화면이 읽음, 첫 전투 전에는 null)
        public CombatContext LatestCombatContext { get; private set; }

        // 전투가 진행 중인지 (끝난 전투가 남아 있어도 false)
        public bool IsFighting => LatestCombatContext != null && LatestCombatContext.Outcome == CombatOutcome.InProgress;

        // 전투를 시작할 수 있는지 여부
        public bool CanStart => IsFighting == false
                                && round.Phase == RoundPhase.Place
                                && placement.Pending == null
                                && Stage.IsGameOver == false
                                && Stage.IsAllWavesCleared == false;

        // 전투를 시작할 수 있으면 현재 웨이브로 전투를 만들고 CombatStarted로 알림
        public void StartCombat()
        {
            if (CanStart == false)
            {
                return;
            }

            LatestCombatContext = new CombatContext(placement.Board, Stage.CurrentWave);
            CombatStarted?.Invoke(LatestCombatContext);
        }

        // 이어하기 - 전투 중에 꺼진 웨이브를 화면 없이 끝까지 계산해 결과만 반영함 (호출부는 RunSaver)
        // 전투에는 무작위가 없으므로 저장한 보드와 웨이브로 돌린 결과를 그대로 씀
        // 지고 있을 때 꺼서 같은 웨이브를 다시 하는 것을 막기 위함
        public void ResolveImmediately()
        {
            if (CanStart == false)
            {
                throw new InvalidOperationException("지금은 전투를 계산할 수 없음");
            }

            LatestCombatContext = new CombatContext(placement.Board, Stage.CurrentWave);

            while (LatestCombatContext.Outcome == CombatOutcome.InProgress)
            {
                LatestCombatContext.Tick(ResolveStep);
            }

            Finish();
        }

        // 즉시 계산할 때 Tick 한 번에 넘기는 시간 (60fps 한 프레임)
        const float ResolveStep = 1f / 60f;

        // 전투 중이고 일시정지가 아니면 Tick을 진행하고, 결과가 나면 Stage에 반영한 뒤 CombatFinished로 알림
        void Update()
        {
            if (IsFighting == false || GameSession.IsPaused)
            {
                return;
            }

            LatestCombatContext.Tick(Time.deltaTime);

            if (LatestCombatContext.Outcome == CombatOutcome.InProgress)
            {
                return;
            }

            Finish();
        }

        void Finish()
        {
            int unresolved = LatestCombatContext.UnresolvedEnemies;
            stageController.ApplyCombatResult(LatestCombatContext.Outcome, unresolved, LatestCombatContext.UnresolvedBosses);
            CombatFinished?.Invoke(LatestCombatContext.Outcome, unresolved);
        }
    }
}
