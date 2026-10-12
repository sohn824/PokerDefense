using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * GameFlowController
     *
     * 라운드 루프 제어
     * 손패 교체 -> 유닛 배치 -> 전투 -> 라운드 결과 화면 -> 다음 라운드 (게임 오버나 마지막 웨이브 클리어면 Run 종료)
     *
     * 이 클래스는 단계 사이를 잇기만 함
     * - 라운드 시작 (RoundController.StartRound 호출)
     * - 전투가 끝나면 RoundSettled로 라운드 결과 화면을 띄우고, DismissBreak가 올 때까지 다음 라운드를 보류
     *   (DismissBreak = 라운드 결과 화면이 닫혔다고 UI가 알리는 메서드)
     * 각 단계 안의 플레이어 입력은 RoundController·PlacementController·CombatController가 맡음
     */
    public sealed class GameFlowController : MonoBehaviour
    {
        // 전투 한 판을 마친 직후의 요약 (라운드 결과 화면이 사용)
        public readonly struct RoundSummary
        {
            public RoundSummary(int wave, bool cleared, int enemiesLeft, int lifeLost, int jokerGained, int nextAct,
                int bossLeft, IReadOnlyList<CombatContext.Remnant> remnants)
            {
                Wave = wave;
                Cleared = cleared;
                EnemiesLeft = enemiesLeft;
                LifeLost = lifeLost;
                JokerGained = jokerGained;
                NextAct = nextAct;
                BossLeft = bossLeft;
                Remnants = remnants;
            }

            public int Wave { get; }
            public bool Cleared { get; }
            public int EnemiesLeft { get; }
            public int LifeLost { get; }
            public int JokerGained { get; }

            // 다음 라운드가 새 Act를 여는 경우 그 번호(2~5), 아니면 0
            // (Act = 50웨이브를 10웨이브씩 나눈 구간, 각 Act는 보스 웨이브로 끝남)
            public int NextAct { get; }

            // 이번 웨이브에서 처리하지 못한 보스 수 (아직 나오지 않은 보스 포함)
            public int BossLeft { get; }

            // 처리하지 못한 적을 종류별로 센 스냅샷 (필드에 남은 적 + 아직 나오지 않은 적)
            // 전투가 끝난 순간에 만든 값이라 이후에 바뀌지 않음
            public IReadOnlyList<CombatContext.Remnant> Remnants { get; }
        }

        [SerializeField] RoundController round;
        [SerializeField] CombatController combat;
        [SerializeField] StageController stage;

        // 라운드가 새로 열리거나 루프가 멈췄을 때 발생하는 이벤트
        public event Action FlowChanged;

        // 전투 한 판을 마쳐 라운드 결과 화면을 띄워야 할 때 발생 (DismissBreak 전까지 다음 단계로 안 넘어감)
        public event Action<RoundSummary> RoundSettled;

        // 지금까지 시작한 라운드 수
        // StageContext.WaveIndex(0부터)와 따로 세지만 라운드 진행 중에는 항상 WaveIndex + 1과 같음
        public int RoundNumber { get; private set; }

        // 게임 오버나 마지막 웨이브 클리어로 루프가 멈췄는지
        public bool IsFinished { get; private set; }

        // 직전 전투의 요약 스냅샷 (라운드 결과 화면·직전 전투 회고(RoundRecap)·최종 결과 화면이 공유)
        // 다음 전투가 끝날 때 교체됨, 첫 전투 전에는 null
        public RoundSummary? LastRound { get; private set; }

        // 루프가 멈춘 시점까지 걸린 시간 (최종 결과 화면이 사용)
        public float ElapsedSeconds { get; private set; }

        float startedAt;

        // 라운드 결과 화면이 닫히길(DismissBreak) 기다리는 중인지
        public bool IsWaitingForBreak => waitingForBreak;

        bool waitingForBreak;
        // DismissBreak가 불릴 때 실행할 작업 (지금은 항상 AdvanceRound)
        Action afterBreak;

        void Awake()
        {
            // 전투 종료를 구독해 라운드 루프 처리, 족보 판정 결과는 Run 통계에 기록
            combat.CombatFinished += OnCombatFinished;
            round.Evaluated += OnEvaluated;

        }

        void OnEvaluated(HandResult result)
        {
            stage.Stats.RecordHand(result.Category);
        }

        void Start()
        {
            startedAt = Time.time;

            // 첫 라운드도 GameFlowController가 제어함
            // (라운드 루프의 책임을 GameFlowController 한 곳에 모으기 위함)
            StartNextRound();
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            // 스냅샷은 게임이 끝났든 아니든 항상 남김 (최종 결과 화면도 이 값을 씀)
            RoundSummary summary = BuildSummary(outcome, unresolved);
            LastRound = summary;

            // 게임이 끝났으면 라운드 결과 화면 없이 최종 결과 화면으로 바로 넘김
            if (stage.Stage.IsGameOver || stage.Stage.IsAllWavesCleared)
            {
                AdvanceRound();
                return;
            }

            // 라운드 결과 화면을 띄우고, 닫힐 때 라운드 루프를 이어 감 (전이는 DismissBreak 한 번으로만)
            afterBreak = () => AdvanceRound();
            waitingForBreak = true;
            RoundSettled?.Invoke(summary);
        }

        RoundSummary BuildSummary(CombatOutcome outcome, int unresolved)
        {
            bool cleared = outcome == CombatOutcome.Cleared;
            int lifeLost = cleared ? 0 : stage.Stage.LifeDamageFor(unresolved, combat.Combat.UnresolvedBosses);
            int jokerGained = cleared ? combat.Combat.Wave.JokerReward : 0;

            // 11·21·31·41라운드가 각각 Act 2~5의 시작
            int next = RoundNumber + 1;
            int nextAct = next == 11 || next == 21 || next == 31 || next == 41 ? next / 10 + 1 : 0;

            return new RoundSummary(RoundNumber, cleared, unresolved, lifeLost, jokerGained, nextAct,
                combat.Combat.UnresolvedBosses, combat.Combat.CollectUnresolved());
        }

        // DismissBreak - 라운드 결과 화면이 닫혔다는 신호, 대기를 풀고 다음 라운드로 넘김
        // RoundBreakScreen이 자동 타이머 또는 탭으로 호출 (대기 중이 아니면 무시)
        public void DismissBreak()
        {
            if (waitingForBreak == false)
            {
                return;
            }

            waitingForBreak = false;
            Action next = afterBreak;
            afterBreak = null;
            next?.Invoke();
        }

        /**
         * 전투 결과 하나를 라운드 루프에 반영함 (StageContext에는 이미 반영된 뒤)
         * 실전 전투(OnCombatFinished)와 DevMode 웨이브 스킵이 이 판정을 공유함
         */
        void AdvanceRound()
        {
            // 게임 오버나 마지막 웨이브 클리어 시 라운드 루프를 멈춤
            if (stage.Stage.IsGameOver || stage.Stage.IsAllWavesCleared)
            {
                IsFinished = true;
                ElapsedSeconds = Time.time - startedAt;
                FlowChanged?.Invoke();
                return;
            }

            StartNextRound();
        }

        void StartNextRound()
        {
            RoundNumber++;
            round.StartRound();
            FlowChanged?.Invoke();
        }

#if UNITY_EDITOR
        // 전투 중이거나 이미 끝난 Run이면 스킵 불가
        public bool CanDevSkip => combat.IsFighting == false && IsFinished == false;

        // 이번 Run에 웨이브 스킵을 한 번이라도 썼는지 (결과 전송에서 제외하는 데 사용)
        public bool UsedDevSkip { get; private set; }

        // 전투 없이 지금 웨이브를 클리어한 것으로 치고 다음 라운드로 넘김 (개발 전용)
        public void DevSkipWave()
        {
            if (CanDevSkip == false || stage.Stage.IsAllWavesCleared)
            {
                return;
            }

            UsedDevSkip = true;
            stage.ApplyCombatResult(CombatOutcome.Cleared, 0, 0);
            AdvanceRound();
        }

        // targetWaveIndex에 닿을 때까지 DevSkipWave를 반복 (개발 전용)
        public void DevJumpToWave(int targetWaveIndex)
        {
            while (CanDevSkip && stage.Stage.WaveIndex < targetWaveIndex && stage.Stage.IsAllWavesCleared == false)
            {
                DevSkipWave();
            }
        }
#endif // UNITY_EDITOR
    }
}
