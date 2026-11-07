using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /**
     * HandCategoryNames
     *
     * 화면 표기용 족보명
     */
    public static class HandCategoryNames
    {
        public static string Of(HandCategory category) => category switch
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
