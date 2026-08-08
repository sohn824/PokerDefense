using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /**
     * CardText
     *
     * 카드의 화면 표기
     * Card.ToString()은 "4h" 같은 로그·테스트용 표기라 화면에 그대로 쓰지 않는다
     *
     * 다이아는 ♦(U+2666)가 Maplestory 폰트에 없어서 ◆(U+25C6)로 대체했다
     */
    public static class CardText
    {
        public static string Of(Card card) => RankOf(card.Rank) + SuitOf(card.Suit);

        public static string RankOf(Rank rank) => rank switch
        {
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)rank).ToString(),
        };

        public static string SuitOf(Suit suit) => suit switch
        {
            Suit.Spade => "♠",
            Suit.Heart => "♥",
            Suit.Diamond => "◆",
            _ => "♣",
        };

        public static bool IsRed(Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;
    }
}
