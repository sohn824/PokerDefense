using System.Collections.Generic;
using System.Globalization;
using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /**
     * HandOddsText
     *
     * 교체 확률을 문구로 바꾸는 함수 모음 (RoundScreen·AssistScreen·HandOddsScreen이 함께 씀)
     */
    public static class HandOddsText
    {
        // count / total을 퍼센트 문구로 바꿈 (소수 첫째 자리까지, 0이 아닌데 0.1% 미만이면 "<0.1%")
        public static string Percent(int count, int total)
        {
            if (count <= 0 || total <= 0)
            {
                return "0%";
            }
            double percent = 100d * count / total;
            return percent < 0.1d ? "<0.1%" : percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        // 지금 족보보다 높은 족보(HandRarity 기준)와 같은 족보가 나올 확률을 한 줄로 요약함
        // 경우의 수를 먼저 더한 뒤 퍼센트로 바꿈 (족보마다 반올림한 값을 더하면 오차가 쌓이기 때문)
        public static string Summary(IReadOnlyList<HandOddsEntry> odds, HandCategory current)
        {
            if (odds == null || odds.Count == 0)
            {
                return "남은 덱으로는 계산할 수 없습니다";
            }
            int higher = 0;
            int same = 0;
            int rank = HandRarity.RankOf(current);
            foreach (HandOddsEntry entry in odds)
            {
                if (HandRarity.RankOf(entry.Category) > rank)
                {
                    higher += entry.Count;
                }
                if (entry.Category == current)
                {
                    same += entry.Count;
                }
            }
            return "높은 족보 " + Percent(higher, odds[0].Total) + " · 같은 족보 " + Percent(same, odds[0].Total);
        }
    }
}
