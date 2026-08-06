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

    /// <summary>
    /// A는 14로 둔다. 백스트레이트(A-2-3-4-5) 판정에서만 1로 재해석한다.
    /// </summary>
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

    public readonly struct Card : IEquatable<Card>
    {
        public readonly Rank Rank;
        public readonly Suit Suit;

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => ((int)Rank * 4) + (int)Suit;

        public static bool operator ==(Card left, Card right) => left.Equals(right);

        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        /// <summary>"As", "Th", "2c" 형태. 로그와 테스트 실패 메시지용.</summary>
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
