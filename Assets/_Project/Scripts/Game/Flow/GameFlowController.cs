using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * GameFlowController
     *
     * 게임 플로우 제어
     * 라운드 루프를 처리함 (자동으로 넘어가는 구간 처리, 플레이어 입력이 필요한 구간은 각각의 컨트롤러가 맡음)
     *
     * 상점 웨이브(5·10·…)만 예외로 멈춤 (상점을 닫을 때까지 다음 라운드를 열지 않음)
     */
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] CombatController combat;
        [SerializeField] StageController stage;

        // 라운드가 새로 열리거나 루프가 멈췄을 때 호출하는 이벤트
        public event Action FlowChanged;

        // 교체 미사용 Chip 보너스를 받았을 때 호출하는 이벤트 (인자: 받은 Chip)
        public event Action<int> HoldBonusEarned;

        // 카드 상점이 열렸을 때 호출하는 이벤트 (인자: 진열된 카드 4장)
        public event Action<IReadOnlyList<Card>> ShopOpened;

        // 상점이 등장하는 라운드 주기
        const int ShopEveryRounds = 5;

        readonly System.Random seedSource = new System.Random();

        // 상점 진열 컨트롤러
        ShopOffer shopOffer;

        // 지금까지 시작한 라운드 수
        public int RoundNumber { get; private set; }

        // 열려 있는 상점 진열 (닫히면 null)
        public IReadOnlyList<Card> ShopCards { get; private set; }

        // 게임 오버나 스테이지 클리어로 루프가 멈췄는지
        public bool IsFinished { get; private set; }

        // 루프가 멈춘 시점까지 걸린 시간 (결과 화면이 사용)
        public float ElapsedSeconds { get; private set; }

        float startedAt;

        void Awake()
        {
            // combatController의 라운드가 끝나는 시점 이벤트를 구독해 라운드 루프 처리
            combat.CombatFinished += OnCombatFinished;
            // roundController의 손패 확정 시점 이벤트를 구독해 남은 교체수에 비례해 Chip 보너스 지급
            round.Evaluated += OnEvaluated;

            shopOffer = new ShopOffer(seedSource.Next());
        }

        // 교체를 덜 쓸수록 Chip을 준다
        void OnEvaluated(PokerDefense.Poker.HandResult result)
        {
            stage.Stats.RecordHand(result.Category);

            int bonus = stage.HoldBonusFor(round.UsedExchanges);

            if (bonus <= 0)
            {
                return;
            }

            stage.AddChip(bonus);
            HoldBonusEarned?.Invoke(bonus);
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
            AdvanceRound(outcome);
        }

        /**
         * 전투 결과 하나를 라운드 루프에 반영한다 (StageContext에는 이미 반영된 뒤)
         * 실전 전투(OnCombatFinished)와 DevMode 웨이브 스킵이 이 판정을 공유한다
         */
        void AdvanceRound(CombatOutcome outcome)
        {
            // 게임 오버나 게임 클리어 시 라운드 루프를 멈춤
            if (stage.Stage.IsGameOver || stage.Stage.IsAllWavesCleared)
            {
                IsFinished = true;
                ElapsedSeconds = Time.time - startedAt;
                FlowChanged?.Invoke();
                return;
            }

            // 방금 클리어한 웨이브가 상점 웨이브면 상점을 열고 멈춘다 (5·10·…, 마지막 제외)
            if (outcome == CombatOutcome.Cleared
                && RoundNumber % ShopEveryRounds == 0
                && RoundNumber < stage.Stage.TotalWaves)
            {
                ShopCards = shopOffer.Roll(stage.Stage.HeldCards);

                if (ShopCards.Count > 0)
                {
                    ShopOpened?.Invoke(ShopCards);
                    return;
                }

                ShopCards = null;
            }

            StartNextRound();
        }

        // 상점을 닫는다 (사거나 스킵하고 나면 UI가 호출) — 다음 라운드가 열린다
        public void CloseShop()
        {
            if (ShopCards == null)
            {
                return;
            }

            ShopCards = null;
            StartNextRound();
        }

        void StartNextRound()
        {
            RoundNumber++;
            round.StartRound(stage.Stage.HeldCards);
            FlowChanged?.Invoke();
        }

#if UNITY_EDITOR
        // 전투 중이거나(진행 중인 CombatContext와 충돌)
        // 상점이 열려 있거나
        // 이미 끝난 판이면 스킵 불가
        public bool CanDevSkip => combat.IsFighting == false && ShopCards == null && IsFinished == false;

        // 개발 전용
        // 전투 없이 지금 웨이브를 클리어한 것으로 치고 다음 라운드로 넘김
        public void DevSkipWave()
        {
            if (CanDevSkip == false || stage.Stage.IsAllWavesCleared)
            {
                return;
            }

            stage.ApplyCombatResult(CombatOutcome.Cleared, 0, 0);
            AdvanceRound(CombatOutcome.Cleared);
        }

        // 개발 전용
        // targetWaveIndex에 닿을 때까지 DevSkipWave를 반복
        // 상점이나 게임 종료를 만나면 그 자리에서 멈춤
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
