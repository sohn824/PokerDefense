using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.EditorTools
{
    /**
     * HandCheatTable
     *
     * 개발 전용. 족보 13개를 각각 만들어 내는 카드 5장
     *
     * 게임 코드가 아니라 에디터 어셈블리에 둔다 - 빌드에 들어갈 이유가 없고,
     * RoundContext.ForceHand는 "이 5장으로 갈아끼워라"만 알면 된다
     *
     * **표가 틀리면 조용히 다른 유닛이 소환된다.** 특수 승격 넷(백스트레이트/마운틴/
     * 백스트레이트 플러시/로열)이 정통 스트레이트나 스트레이트 플러시로 떨어지기 쉬우므로
     * HandCheatTableTests가 13개 전부를 HandEvaluator에 넣어 고정한다
     */
    public static class HandCheatTable
    {
        // 표기 순서. 족보가 센 순이 아니라 눈으로 찾기 쉬운 순이다
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
            // 무늬를 갈라 플러시로 승격되지 않게 한다
            [HandCategory.Straight] = new[]
            {
                C(Rank.Five, Suit.Spade), C(Rank.Six, Suit.Heart), C(Rank.Seven, Suit.Diamond),
                C(Rank.Eight, Suit.Club), C(Rank.Nine, Suit.Spade),
            },
            // 숫자를 띄워 스트레이트로 승격되지 않게 한다
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
            // A를 넣지 않아야 마운틴/백스트레이트 플러시로 안 간다
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

        // 화면 표기용 한글 이름. 유닛 이름은 HandUnitTable에서 읽으므로 여기 적지 않는다
        public static string NameOf(HandCategory category) => category switch
        {
            HandCategory.HighCard => "하이카드",
            HandCategory.OnePair => "원페어",
            HandCategory.TwoPair => "투페어",
            HandCategory.ThreeOfAKind => "트리플",
            HandCategory.Straight => "스트레이트",
            HandCategory.Flush => "플러시",
            HandCategory.FullHouse => "풀하우스",
            HandCategory.FourOfAKind => "포카드",
            HandCategory.StraightFlush => "스트레이트 플러시",
            HandCategory.BackStraight => "백스트레이트",
            HandCategory.Mountain => "마운틴",
            HandCategory.BackStraightFlush => "백스트레이트 플러시",
            HandCategory.RoyalStraightFlush => "로열 스트레이트 플러시",
            _ => category.ToString(),
        };
    }
}
