namespace PokerDefense.Poker
{
    /// <summary>
    /// 판정 결과 분류. **enum 순서를 족보의 강함으로 사용하지 않는다.**
    /// (백스트레이트는 정통 포커에서 가장 약한 스트레이트지만 이 게임에서는 특수 유닛을 준다.)
    /// 카테고리 -> 보상 유닛 연결은 데이터 테이블이 담당한다.
    /// </summary>
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

        // 특수 족보. 기본 카테고리의 하위 분류를 별도로 승격시킨 것이다.
        BackStraight,       // A-2-3-4-5
        Mountain,           // A-K-Q-J-10
        BackStraightFlush,  // A-2-3-4-5 동일 무늬
        RoyalStraightFlush, // A-K-Q-J-10 동일 무늬
    }
}
