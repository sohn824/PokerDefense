using System.Collections.Generic;
using System.Text;
using PokerDefense.Poker;

namespace PokerDefense.UI
{
    // 선택한 자리를 교체했을 때 나올 수 있는 족보 전부와 그 확률을 나열한다. 하나를 추천하지 않는다
    public static class HandOddsText
    {
        public static string Describe(IReadOnlyList<HandOddsEntry> odds)
        {
            if (odds == null || odds.Count == 0)
            {
                return "남은 덱으로는 계산할 수 없습니다";
            }

            StringBuilder text = new StringBuilder();

            for (int i = 0; i < odds.Count; i++)
            {
                if (i > 0) text.Append(" · ");

                HandOddsEntry entry = odds[i];
                text.Append(HandCategoryNames.Of(entry.Category)).Append(' ')
                    .Append((entry.Probability * 100f).ToString("0.#")).Append("% (")
                    .Append(entry.Count).Append('/').Append(entry.Total).Append(')');
            }

            return text.ToString();
        }
    }
}
