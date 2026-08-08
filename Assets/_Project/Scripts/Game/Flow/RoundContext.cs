using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RoundContext
     * 
     * 라운드 하나의 흐름
     * 카드 드로우 -> 카드 교체 (카드 하나당 한번씩 가능, 교체한 자리는 잠김) -> 족보 판정 순서로 진행
     */
    public sealed class RoundContext
    {
        public const int HandSize = 5;

        readonly Deck deck; // 전체 덱
        readonly Card[] hand = new Card[HandSize]; // 5장 손패
        readonly bool[] locked = new bool[HandSize]; // 교체해서 잠긴 자리인지 여부

        public RoundContext(int seed)
        {
            deck = new Deck(seed);
        }

        public RoundPhase Phase { get; private set; } = RoundPhase.Draw;

        public IReadOnlyList<Card> Hand => hand;

        // 손패 족보 - Evaluate로 판정하기 전에는 의미 없는 값
        public HandResult Result { get; private set; }

        // 이번 라운드에 이미 교체해서 더는 바꿀 수 없는 자리인지 판별
        public bool IsLocked(int index) => locked[index];

        // 아직 바꿀 수 있는 손패 자리 수
        public int ExchangeableCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < HandSize; i++)
                {
                    if (locked[i] == false)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // Draw Phase -> 덱에서 카드를 드로우하고 Exchange Phase로 넘김
        public void Draw()
        {
            Require(RoundPhase.Draw);

            for (int i = 0; i < HandSize; i++)
            {
                hand[i] = deck.Draw();
            }

            Phase = RoundPhase.Exchange;
        }

        // 고른 자리의 카드를 교체하고 그 자리는 잠금
        // 이미 잠긴 자리를 골랐으면 예외를 던지고 실패 처리
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

        // Exchange Phase를 끝내고 Evaluate Phase로 넘김
        public void FinishExchange()
        {
            Require(RoundPhase.Exchange);

            Phase = RoundPhase.Evaluate;
        }

        // Evaluate Phase - 족보를 판정하고 Place Phase로 넘김
        public HandResult Evaluate()
        {
            Require(RoundPhase.Evaluate);

            Result = HandEvaluator.Evaluate(hand);
            Phase = RoundPhase.Place;
            return Result;
        }

        // 손패 검증 - 비정상 시도일 경우 예외를 던지고 실패 처리
        void Validate(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];

                if (index < 0 || index >= HandSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), $"손패 범위를 벗어난 인덱스 입니다: {index}");
                }

                if (locked[index])
                {
                    throw new InvalidOperationException($"이미 교체한 자리는 이번 라운드에 다시 바꿀 수 없습니다: {index}");
                }

                for (int j = i + 1; j < indices.Count; j++)
                {
                    if (indices[j] == index)
                    {
                        throw new ArgumentException($"같은 인덱스를 두 번 교체할 수 없습니다: {index}", nameof(indices));
                    }
                }
            }
        }

        void Require(RoundPhase expected)
        {
            if (Phase != expected)
            {
                throw new InvalidOperationException($"{expected} 페이즈에서만 가능한 동작입니다. 현재: {Phase}");
            }
        }
    }
}
