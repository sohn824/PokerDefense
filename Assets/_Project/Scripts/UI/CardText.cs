using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /**
     * CardText
     *
     * 카드를 화면에 표시할 문자열로 바꾸는 클래스
     * Card.ToString()은 "4h" 같은 로그·테스트용 표기라 화면에 그대로 쓰지 않고 이 CardText를 거쳐 표시함
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
