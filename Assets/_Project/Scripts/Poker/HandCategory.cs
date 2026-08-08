namespace PokerDefense.Poker
{
    /**
     * 족보 리스트
     * 
     * 족보 -> 보상 유닛은 데이터 테이블 담당
     */
    public enum HandCategory
    {
        HighCard,
        OnePair,
        TwoPair,
        ThreeOfAKind,
        Straight,
        Flush,
        FullHouse,
        FourOfAKind,
        StraightFlush,

        // 특수 족보
        BackStraight,       // A-2-3-4-5
        Mountain,           // A-K-Q-J-10
        BackStraightFlush,  // A-2-3-4-5 동일 무늬
        RoyalStraightFlush, // A-K-Q-J-10 동일 무늬
    }
}
