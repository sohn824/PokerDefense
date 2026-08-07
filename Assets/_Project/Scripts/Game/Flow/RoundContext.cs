using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /// <summary>
    /// 라운드 하나의 카드 상태. Draw -> Exchange(여러 번) -> Evaluate 순서로만 진행한다.
    /// MonoBehaviour가 아니라서 EditMode 테스트에서 그대로 쓸 수 있다.
    ///
    /// 교체는 자리 단위로 잠긴다. 한 라운드에 여러 번 교체할 수 있지만 같은 자리는 한 번뿐이라,
    /// 결국 최대 5장까지 바뀐다. 한 번에 몰아서 바꾸지 않고 나눠 바꿀 수 있게 한 것은
    /// 바뀐 결과를 보고 다음 선택을 하게 만들기 위함이다.
    ///
    /// 교체도 같은 덱에서 뽑으므로 한 라운드 안에서는 중복 카드가 나오지 않는다 (DESIGN §3.2).
    /// </summary>
    public sealed class RoundContext
    {
        public const int HandSize = 5;

        readonly Deck deck;
        readonly Card[] hand = new Card[HandSize];
        readonly bool[] locked = new bool[HandSize];

        public RoundContext(int seed)
        {
            deck = new Deck(seed);
        }

        public RoundPhase Phase { get; private set; } = RoundPhase.Draw;

        public IReadOnlyList<Card> Hand => hand;

        /// <summary>Evaluate를 부르기 전에는 의미 없는 값이다.</summary>
        public HandResult Result { get; private set; }

        /// <summary>이번 라운드에 이미 교체해서 더는 바꿀 수 없는 자리인지.</summary>
        public bool IsLocked(int index) => locked[index];

        /// <summary>아직 바꿀 수 있는 자리 수.</summary>
        public int ExchangeableCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < HandSize; i++)
                {
                    if (!locked[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Draw()
        {
            Require(RoundPhase.Draw);

            for (int i = 0; i < HandSize; i++)
            {
                hand[i] = deck.Draw();
            }

            Phase = RoundPhase.Exchange;
        }

        /// <summary>
        /// 고른 자리의 카드를 새로 뽑은 카드로 바꾸고 그 자리를 잠근다.
        /// 이미 잠긴 자리를 고르면 예외를 던지며, 이때 손패는 한 장도 바뀌지 않는다.
        /// </summary>
        public void Exchange(IReadOnlyList<int> indices)
        {
            Require(RoundPhase.Exchange);

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            Validate(indices);

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                hand[index] = deck.Draw();
                locked[index] = true;
            }
        }

        /// <summary>교체를 끝내고 판정 단계로 넘어간다.</summary>
        public void FinishExchange()
        {
            Require(RoundPhase.Exchange);

            Phase = RoundPhase.Evaluate;
        }

        public HandResult Evaluate()
        {
            Require(RoundPhase.Evaluate);

            Result = HandEvaluator.Evaluate(hand);
            Phase = RoundPhase.Place;
            return Result;
        }

        /// <summary>손패를 건드리기 전에 전부 검사한다. 일부만 교체되고 실패하는 일이 없어야 한다.</summary>
        void Validate(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];

                if (index < 0 || index >= HandSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), $"손패 인덱스 범위를 벗어났다: {index}");
                }

                if (locked[index])
                {
                    throw new InvalidOperationException($"이미 교체한 자리는 이번 라운드에 다시 바꿀 수 없다: {index}");
                }

                for (int j = i + 1; j < indices.Count; j++)
                {
                    if (indices[j] == index)
                    {
                        throw new ArgumentException($"같은 인덱스를 두 번 교체할 수 없다: {index}", nameof(indices));
                    }
                }
            }
        }

        void Require(RoundPhase expected)
        {
            if (Phase != expected)
            {
                throw new InvalidOperationException($"{expected} 페이즈에서만 가능하다. 현재: {Phase}");
            }
        }
    }
}
