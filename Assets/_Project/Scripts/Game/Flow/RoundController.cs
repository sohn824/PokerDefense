using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RoundContext를 씬에서 구동하는 클래스
     * UI는 이 컴포넌트의 이벤트를 구독하고 입력만 되돌려줌
     * Game은 UI를 모른다
     */
    public sealed class RoundController : MonoBehaviour
    {
        readonly System.Random seedSource = new System.Random();

        RoundContext round;

        public event Action<IReadOnlyList<Card>> HandChanged;
        public event Action<RoundPhase> PhaseChanged;
        public event Action<HandResult> Evaluated;

        public bool IsLocked(int index) => round.IsLocked(index);

        public int ExchangeableCount => round.ExchangeableCount;

        public int UsedExchanges => round == null ? 0 : round.UsedExchanges;

        // 라운드가 아직 안 열렸으면 Draw Phase로 본다
        public RoundPhase Phase => round == null ? RoundPhase.Draw : round.Phase;

        // 지금 확정하면 어떤 족보가 되는지 표시용
        public HandResult PreviewHand() => HandEvaluator.Evaluate(round.Hand);

        // 새 라운드가 시작될 때 호출 (호출부는 GameFlowController)
        // 덱을 새로 셔플하고 5장 뽑는다
        // heldCards(상점 보유 카드)는 이 라운드 덱에서 빠진다
        public void StartRound(IReadOnlyList<Card> heldCards = null)
        {
            round = new RoundContext(seedSource.Next(), heldCards);
            round.Draw();

            PhaseChanged?.Invoke(round.Phase);
            HandChanged?.Invoke(round.Hand);
        }

        // 상점 보유 카드 한 장을 손패 자리에 놓음
        // 인벤토리에서 빼는 것은 호출부(StageController) 책임
        public void PlaceHeldCard(int handIndex, Card card)
        {
            round.PlaceHeldCard(handIndex, card);

            HandChanged?.Invoke(round.Hand);
        }

        // 고른 자리의 카드 교체
        public void ExchangeCards(IReadOnlyList<int> indices)
        {
            round.Exchange(indices);

            HandChanged?.Invoke(round.Hand);
        }

        // 교체를 끝내고 족보 확정
        public void ConfirmHand()
        {
            round.FinishExchange();

            HandResult result = round.Evaluate();
            PhaseChanged?.Invoke(round.Phase);
            Evaluated?.Invoke(result);
        }

#if UNITY_EDITOR
        public bool CanForceHand => round != null && round.Phase == RoundPhase.Exchange;

        // 개발 전용
        // 원하는 족보의 5장으로 손패 강제 교체
        public void DevForceHand(IReadOnlyList<Card> cards)
        {
            round.ForceHand(cards);

            HandChanged?.Invoke(round.Hand);
        }
#endif //UNITY_EDITOR
    }
}
