using System;

namespace PokerDefense.Poker
{
    public enum Suit
    {
        Spade,
        Heart,
        Diamond,
        Club,
    }

    /// A는 14로 두고 백스트레이트(A-2-3-4-5) 판정에서만 1로 재해석
    public enum Rank
    {
        Two = 2,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
        Ace,
    }

    /**
     * Card
     * 
     * 카드 한장을 정의하는 구조체
     * Equal 판정을 직접 정의해야 하므로 IEquatable 상속
     */
    public readonly struct Card : IEquatable<Card>
    {
        public readonly Rank Rank;
        public readonly Suit Suit;

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        // 카드 숫자와 모양이 모두 같으면 Equal 판정
        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        // Card가 아닌 object는 바로 Equals 실패 판정
        // (모든 object 타입이 가지고 있는 Eqauls 기본 구현은 느리므로 override로 최적화)
        public override bool Equals(object obj) => obj is Card other && Equals(other);

        // Equals 재정의 시 반드시 GetHashCode()도 재정의 필요
        // (Dictionary/HashSet 등 Hash 기반 Collection 대응)
        public override int GetHashCode() => ((int)Rank * 4) + (int)Suit;

        // Equal 연산자도 재정의
        public static bool operator ==(Card left, Card right) => left.Equals(right);

        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        // 카드를 "As", "Th", "2c"등의 형태로 문자열로 표현 (DebugLog용)
        public override string ToString() => RankSymbol + SuitSymbol;

        string RankSymbol => Rank switch
        {
            Rank.Ten => "T",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)Rank).ToString(),
        };

        string SuitSymbol => Suit switch
        {
            Suit.Spade => "s",
            Suit.Heart => "h",
            Suit.Diamond => "d",
            _ => "c",
        };
    }
}
