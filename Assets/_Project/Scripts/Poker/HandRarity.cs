using System;

namespace PokerDefense.Poker
{
    /**
     * HandRarity
     *
     * 족보를 흔한 것부터 희귀한 것 순으로 세운 표
     * RankOf(족보) = 이 표에서의 위치 (클수록 희귀)
     * 족보끼리 비교하는 곳에서 사용 (최고 족보 기록, 족보 확정 효과음·축하 연출 단계, 교체 확률 목록 정렬)
     *
     * HandCategory의 enum 순서는 희귀도 순서가 아니므로 비교에 enum 값을 쓰지 않음
     * 예) BackStraight는 enum에서 StraightFlush 뒤에 있지만 실제로는 FourOfAKind보다 흔함
     *
     * 이 순서는 밸런스로 정한 값이 아니라 52장에서 5장을 뽑을 때의 조합 수로 정해지므로 Poker 계층에 둠
     * 예) 마운틴·백스트레이트는 각 1,020가지, 포카드는 624가지라 포카드가 더 희귀
     * 족보별로 어떤 유닛을 보상으로 줄지는 별개이며 HandUnitTable 담당
     */
    public static class HandRarity
    {
        // 확률이 같은 족보들(Mountain/BackStraight, RoyalStraightFlush/BackStraightFlush)은
        // 순서를 임의로 정했으므로
        // 둘 사이의 RankOf 비교 결과에는 의미가 없음
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

        // 족보의 희귀도 등수 (표에서의 위치 0~12, 희귀할수록 큼)
        public static int RankOf(HandCategory category)
        {
            for (int i = 0; i < CommonFirst.Length; i++)
            {
                if (CommonFirst[i] == category)
                {
                    return i;
                }
            }

            // HandCategory에 족보를 추가하고 이 표를 안 고친 경우
            // 가장 흔한 족보(0)로 취급하고 넘어가면 원인을 찾기 어려우므로 예외를 던짐
            // (StageTests에서 미리 확인)
            throw new InvalidOperationException($"희귀도 표에 {category}가 없음");
        }
    }
}
