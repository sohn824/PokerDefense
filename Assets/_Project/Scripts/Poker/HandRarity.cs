using System;

namespace PokerDefense.Poker
{
    /**
     * HandRarity
     *
     * 족보를 "얼마나 나오기 어려운가"로 줄 세운다
     *
     * HandCategory의 enum 순서는 강함이 아니다 (DESIGN §3.3) - 백스트레이트가 Straight 뒤에 있는 것이 그 예다
     * 그래서 족보끼리 비교해야 하는 곳은 enum 값 대신 이 표를 쓴다
     *
     * 이 순서는 밸런스 값이 아니라 52장 덱에서 5장을 뽑는 조합 수로 정해지는 포커의 사실이라 Poker에 둔다
     * 보상 세기(어떤 유닛이 나오는가)는 여전히 별개 축이며 HandUnitTable이 담당한다
     */
    public static class HandRarity
    {
        // 흔한 것부터. 확률이 같은 짝(마운틴/백스트레이트, 로열/백스트레이트 플러시)의 앞뒤는 임의다
        static readonly HandCategory[] CommonFirst =
        {
            HandCategory.HighCard,
            HandCategory.OnePair,
            HandCategory.TwoPair,
            HandCategory.ThreeOfAKind,
            HandCategory.Straight,
            HandCategory.Flush,
            HandCategory.FullHouse,
            HandCategory.Mountain,
            HandCategory.BackStraight,
            HandCategory.FourOfAKind,
            HandCategory.StraightFlush,
            HandCategory.RoyalStraightFlush,
            HandCategory.BackStraightFlush,
        };

        /// <summary>희귀할수록 큰 값. 족보를 비교할 때 enum 값 대신 이것을 쓴다.</summary>
        public static int RankOf(HandCategory category)
        {
            for (int i = 0; i < CommonFirst.Length; i++)
            {
                if (CommonFirst[i] == category)
                {
                    return i;
                }
            }

            // 카테고리가 늘었는데 표를 안 고친 것이다. 조용히 0등으로 취급하면 나중에 이유를 못 찾는다
            throw new InvalidOperationException($"희귀도 표에 {category}가 없다.");
        }
    }
}
