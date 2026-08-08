using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /// <summary>
    /// RoundContext를 씬에서 구동한다. UI는 이 컴포넌트의 이벤트를 구독하고 입력만 되돌려준다.
    /// Game은 UI를 모른다 (DESIGN §2).
    /// </summary>
    public sealed class RoundController : MonoBehaviour
    {
        readonly System.Random seedSource = new System.Random();

        RoundContext round;

        public event Action<IReadOnlyList<Card>> HandChanged;
        public event Action<RoundPhase> PhaseChanged;
        public event Action<HandResult> Evaluated;

        public bool IsLocked(int index) => round.IsLocked(index);

        public int ExchangeableCount => round.ExchangeableCount;

        // 라운드가 아직 안 열렸으면 Draw로 본다
        public RoundPhase Phase => round == null ? RoundPhase.Draw : round.Phase;

        // 라운드를 여는 주체는 GameFlowController다. 여기서 스스로 시작하면 루프 주인이 둘이 된다
        /// <summary>새 라운드. 덱을 새로 셔플하고 5장 뽑는다 (DESIGN §3.2 — 라운드마다 새 덱).</summary>
        public void StartRound()
        {
            round = new RoundContext(seedSource.Next());
            round.Draw();

            PhaseChanged?.Invoke(round.Phase);
            HandChanged?.Invoke(round.Hand);
        }

        /// <summary>고른 자리만 교체한다. 페이즈는 Exchange에 머물러 여러 번 부를 수 있다.</summary>
        public void ExchangeCards(IReadOnlyList<int> indices)
        {
            round.Exchange(indices);

            HandChanged?.Invoke(round.Hand);
        }

        /// <summary>
        /// 교체를 끝낸다. Evaluate는 종료 조건이 "즉시"라(DESIGN §1) 이어서 바로 판정한다.
        /// </summary>
        public void ConfirmHand()
        {
            round.FinishExchange();

            HandResult result = round.Evaluate();
            PhaseChanged?.Invoke(round.Phase);
            Evaluated?.Invoke(result);
        }
    }
}
