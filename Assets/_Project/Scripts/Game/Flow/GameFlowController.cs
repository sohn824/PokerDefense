using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * GameFlowController
     *
     * 라운드 루프를 잇는다
     * 자동으로 넘어가는 구간만 여기서 처리하고, 플레이어 입력이 필요한 구간은 각 컨트롤러가 그대로 맡는다
     *
     *   Draw      즉시              <- StartRound
     *   Exchange  플레이어 확정      <- 확정 버튼
     *   Evaluate  즉시              <- ConfirmHand 안에서 이어짐
     *   Place     플레이어 확정      <- 전투 시작 버튼
     *   Combat    전멸 or 시간 초과
     *   Result    즉시              <- 여기서 다음 라운드를 연다
     *
     * 라운드 N은 웨이브 N을 치른다. 둘은 1:1이다
     */
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] CombatController combat;
        [SerializeField] StageController stage;

        // 라운드가 새로 열리거나 루프가 멈췄을 때
        public event Action FlowChanged;

        // 유지 보너스를 받았을 때 (인자: 받은 Chip)
        public event Action<int> HoldBonusEarned;

        // 지금까지 시작한 라운드 수
        public int RoundNumber { get; private set; }

        // 게임 오버나 스테이지 클리어로 루프가 멈췄는지
        public bool IsFinished { get; private set; }

        void Awake()
        {
            combat.CombatFinished += OnCombatFinished;
            round.Evaluated += OnEvaluated;
        }

        // 교체를 덜 쓸수록 Chip을 준다 (DESIGN §9.1)
        // 확정 시점에 확정되는 값이라 여기서 지급한다
        void OnEvaluated(PokerDefense.Poker.HandResult result)
        {
            int bonus = stage.Economy.HoldBonusFor(round.UsedExchanges);

            if (bonus <= 0)
            {
                return;
            }

            stage.AddChip(bonus);
            HoldBonusEarned?.Invoke(bonus);
        }

        void Start()
        {
            // 첫 라운드도 여기서 연다. RoundController가 스스로 시작하면 루프 주인이 둘이 된다
            StartNextRound();
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            // Result는 종료 조건이 "즉시"라 결과를 확인시킨 뒤 곧바로 다음 라운드로 넘긴다
            if (combat.Stage.IsGameOver || combat.Stage.IsAllWavesCleared)
            {
                IsFinished = true;
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
    }
}
