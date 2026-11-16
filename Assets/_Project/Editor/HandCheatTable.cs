using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.EditorTools
{
    /**
     * HandCheatTable
     *
     * 족보 13개를 각각 만드는 카드 5장을 모아 둔 클래스 (개발 전용)
     * 표가 틀리면 다른 유닛이 소환되므로 HandCheatTableTests로 13개 전부를 검사함
     */
    public static class HandCheatTable
    {
        // 표시 순서 - 일반 족보를 익숙한 포커 순서로 두고 특수 족보를 뒤에 모음
        // 희귀도 순(HandRarity)과는 다름
        public static readonly HandCategory[] Order =
        {
            HandCategory.HighCard,
            HandCategory.OnePair,
            HandCategory.TwoPair,
            HandCategory.ThreeOfAKind,
            HandCategory.Straight,
            HandCategory.Flush,
            HandCategory.FullHouse,
            HandCategory.FourOfAKind,
            HandCategory.StraightFlush,
            HandCategory.BackStraight,
            HandCategory.Mountain,
            HandCategory.BackStraightFlush,
            HandCategory.RoyalStraightFlush,
        };

        static readonly Dictionary<HandCategory, Card[]> Hands = new Dictionary<HandCategory, Card[]>
        {
            [HandCategory.HighCard] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Four, Suit.Heart), C(Rank.Six, Suit.Diamond),
                C(Rank.Nine, Suit.Club), C(Rank.Jack, Suit.Spade),
            },
            [HandCategory.OnePair] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Six, Suit.Diamond),
                C(Rank.Nine, Suit.Club), C(Rank.Jack, Suit.Spade),
            },
            [HandCategory.TwoPair] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Six, Suit.Diamond),
                C(Rank.Six, Suit.Club), C(Rank.Jack, Suit.Spade),
            },
            [HandCategory.ThreeOfAKind] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Two, Suit.Diamond),
                C(Rank.Six, Suit.Club), C(Rank.Jack, Suit.Spade),
            },
            // 무늬를 섞어 스트레이트 플러시가 되지 않게 함
            [HandCategory.Straight] = new[]
            {
                C(Rank.Five, Suit.Spade), C(Rank.Six, Suit.Heart), C(Rank.Seven, Suit.Diamond),
                C(Rank.Eight, Suit.Club), C(Rank.Nine, Suit.Spade),
            },
            // 숫자를 띄워 스트레이트 플러시가 되지 않게 함
            [HandCategory.Flush] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Four, Suit.Spade), C(Rank.Six, Suit.Spade),
                C(Rank.Nine, Suit.Spade), C(Rank.Jack, Suit.Spade),
            },
            [HandCategory.FullHouse] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Two, Suit.Diamond),
                C(Rank.Six, Suit.Club), C(Rank.Six, Suit.Spade),
            },
            [HandCategory.FourOfAKind] = new[]
            {
                C(Rank.Two, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Two, Suit.Diamond),
                C(Rank.Two, Suit.Club), C(Rank.Jack, Suit.Spade),
            },
            // A를 빼서 마운틴·백스트레이트 계열이 되지 않게 함
            [HandCategory.StraightFlush] = new[]
            {
                C(Rank.Five, Suit.Spade), C(Rank.Six, Suit.Spade), C(Rank.Seven, Suit.Spade),
                C(Rank.Eight, Suit.Spade), C(Rank.Nine, Suit.Spade),
            },
            [HandCategory.BackStraight] = new[]
            {
                C(Rank.Ace, Suit.Spade), C(Rank.Two, Suit.Heart), C(Rank.Three, Suit.Diamond),
                C(Rank.Four, Suit.Club), C(Rank.Five, Suit.Spade),
            },
            [HandCategory.Mountain] = new[]
            {
                C(Rank.Ace, Suit.Spade), C(Rank.King, Suit.Heart), C(Rank.Queen, Suit.Diamond),
                C(Rank.Jack, Suit.Club), C(Rank.Ten, Suit.Spade),
            },
            [HandCategory.BackStraightFlush] = new[]
            {
                C(Rank.Ace, Suit.Spade), C(Rank.Two, Suit.Spade), C(Rank.Three, Suit.Spade),
                C(Rank.Four, Suit.Spade), C(Rank.Five, Suit.Spade),
            },
            [HandCategory.RoyalStraightFlush] = new[]
            {
                C(Rank.Ace, Suit.Spade), C(Rank.King, Suit.Spade), C(Rank.Queen, Suit.Spade),
                C(Rank.Jack, Suit.Spade), C(Rank.Ten, Suit.Spade),
            },
        };

        static Card C(Rank rank, Suit suit) => new Card(rank, suit);

        public static IReadOnlyList<Card> HandFor(HandCategory category) => Hands[category];
    }
}
