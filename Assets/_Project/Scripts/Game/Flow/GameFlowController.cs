using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * GameFlowController
     *
     * 게임 플로우 제어
     * 라운드 루프를 처리함 (자동으로 넘어가는 구간 처리, 플레이어 입력이 필요한 구간은 각각의 컨트롤러가 맡음)
     * 
     * 보스를 잡은 웨이브만 예외로 멈춤 (특전을 고를 때까지 다음 라운드를 열지 않음)
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

        // 보스를 잡아 특전을 고를 차례가 됐을 때 호출하는 이벤트 (인자: 나온 특전 선택지 리스트)
        public event Action<IReadOnlyList<PerkId>> PerkOffered;

        readonly System.Random seedSource = new System.Random();

        // 특전 선택지 컨트롤러
        PerkOffer perkOffer;

        // 지금까지 시작한 라운드 수
        public int RoundNumber { get; private set; }

        // 고르기를 기다리는 특전 선택지 리스트 (기다리는 중이 아니면 null)
        public IReadOnlyList<PerkId> Offer { get; private set; }

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

            perkOffer = new PerkOffer(stage.PerkTable, seedSource.Next());
        }

        // 교체를 덜 쓸수록 Chip을 준다
        void OnEvaluated(PokerDefense.Poker.HandResult result)
        {
            stage.Stats.RecordHand(result.Category);

            // 특전에 보너스 Chip이 있다면 적용 (없으면 미적용)
            stage.AddChip(stage.Stage.Perks.ConfirmBonusChip(result.Category, round.UsedExchanges));

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
            // 게임 오버나 게임 클리어 시 라운드 루프를 멈춤
            if (combat.Stage.IsGameOver || combat.Stage.IsAllWavesCleared)
            {
                IsFinished = true;
                ElapsedSeconds = Time.time - startedAt;
                FlowChanged?.Invoke();
                return;
            }

            // 특전을 주는 라운드라면 특전을 고르기 전까지 라운드 루프를 멈춤
            // (Stage.CurrentWave는 이미 다음 웨이브를 가리키므로 방금 클리어한 웨이브를 기준으로 판정)
            if (outcome == CombatOutcome.Cleared && combat.Combat.Wave.PerkReward)
            {
                Offer = perkOffer.DrawPerks(stage.Stage.Perks);

                if (Offer.Count > 0)
                {
                    PerkOffered?.Invoke(Offer);
                    return;
                }

                Offer = null;
            }

            StartNextRound();
        }


        // 특전을 고름
        // 고르기 전에는 라운드 루프가 멈춘 상태이므로 다음 라운드가 열리지 않고
        // 여기서 특전을 적용한 후 StartNextRound()를 호출해 다음 라운드를 연다
        public void ChoosePerk(PerkId id)
        {
            if (WasOffered(id) == false)
            {
                return;
            }

            stage.AddPerk(id);
            Offer = null;
            StartNextRound();
        }

        // 특전 선택지에 있던 특전인지 확인
        bool WasOffered(PerkId id)
        {
            for (int i = 0; Offer != null && i < Offer.Count; i++)
            {
                if (Offer[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        void StartNextRound()
        {
            RoundNumber++;
            round.StartRound();
            FlowChanged?.Invoke();
        }
    }
}
