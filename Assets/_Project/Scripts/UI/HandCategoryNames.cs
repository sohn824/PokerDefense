using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /// <summary>
    /// 화면 표기용 족보명. 프로젝트에 한글 폰트가 없어 영문으로 둔다
    /// (TMP 기본 폰트 Liberation Sans에 한글 글리프가 없다).
    /// 한글로 바꾸려면 폰트 에셋을 넣고 이 파일만 고치면 된다.
    /// </summary>
    public static class HandCategoryNames
    {
        public static string Of(HandCategory category) => category switch
        {
            HandCategory.HighCard => "High Card",
            HandCategory.OnePair => "One Pair",
            HandCategory.TwoPair => "Two Pair",
            HandCategory.ThreeOfAKind => "Three of a Kind",
            HandCategory.Straight => "Straight",
            HandCategory.Flush => "Flush",
            HandCategory.FullHouse => "Full House",
            HandCategory.FourOfAKind => "Four of a Kind",
            HandCategory.StraightFlush => "Straight Flush",
            HandCategory.BackStraight => "Back Straight",
            HandCategory.Mountain => "Mountain",
            HandCategory.BackStraightFlush => "Back Straight Flush",
            HandCategory.RoyalStraightFlush => "Royal Straight Flush",
            _ => category.ToString(),
        };
    }
}
