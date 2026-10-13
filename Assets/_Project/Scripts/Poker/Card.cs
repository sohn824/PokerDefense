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

    /// A는 14로 두고 백스트레이트(A-2-3-4-5)의 판정과 정렬에서만 1로 취급
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
     * 카드 한 장을 나타내는 값 타입 (Rank + Suit)
     * 덱 검색·손패 비교처럼 카드끼리 비교할 일이 많아 IEquatable<Card>를 직접 구현함
     * (구현하지 않으면 List.IndexOf 같은 비교가 boxing을 거쳐 느려짐)
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

        // object로 들어온 비교도 Equals(Card)로 넘김 (Card가 아니면 바로 false)
        // 재정의하지 않으면 ValueType 기본 구현이 필드 구성에 따라 리플렉션 비교를 해 느릴 수 있음
        // (리플렉션 비교 = 실행 중에 필드 목록을 조회해 값을 object로 꺼내 하나씩 비교, 필드마다 boxing 발생)
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
