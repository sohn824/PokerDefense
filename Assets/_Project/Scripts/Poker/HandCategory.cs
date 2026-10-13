namespace PokerDefense.Poker
{
    /**
     * HandCategory
     *
     * 족보 리스트 (족보별 보상 유닛은 HandUnitTable 담당)
     * enum 순서는 족보의 강함·희귀도가 아님 (족보 비교는 HandRarity 사용)
     *
     * 정수 값이 HandUnitTable.asset과 서버로 보내는 결과에 그대로 저장됨
     * 그래서 순서를 바꾸거나 중간에 끼워 넣지 않고, 새 족보는 맨 뒤에 추가
     * (족보를 추가하면 HandRarity 표와 서버 Protocol.cpp의 HandLoopLastHand도 함께 고쳐야 함)
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
