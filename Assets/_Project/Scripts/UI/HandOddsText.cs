using System.Collections.Generic;
using System.Globalization;
using PokerDefense.Poker;

namespace PokerDefense.UI
{
    /** 교체 확률의 요약과 작은 확률 표시를 모든 화면에서 공유한다. */
    public static class HandOddsText
    {
        public static string Percent(int count, int total)
        {
            if (count <= 0 || total <= 0)
            {
                return "0%";
            }
            double percent = 100d * count / total;
            return percent < 0.1d ? "<0.1%" : percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

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
