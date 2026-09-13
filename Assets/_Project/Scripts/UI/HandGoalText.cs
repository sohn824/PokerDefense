using System.Collections.Generic;
using System.Linq;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.UI
{
    public static class HandGoalText
    {
        public static string Describe(IReadOnlyList<HandGoal> goals, GridBoard board, HandUnitTable table, HandCategory current)
        {
            bool Merge(HandCategory category)
            {
                UnitDefinition definition = table.GetDefinition(category);
                for (int i = 0; i < GridBoard.SlotCount; i++)
                {
                    if (board[i] != null && board[i].Definition == definition && board[i].Star == 1) return true;
                }
                return false;
            }
            string text = Merge(current) ? "현재 족보로 소환하면 합치기 가능" : "";
            var targets = goals.Where(g => g.Category != current && (Merge(g.Category)
                || HandRarity.RankOf(g.Category) > HandRarity.RankOf(current)))
                .OrderByDescending(g => Merge(g.Category)).ThenByDescending(g => HandRarity.RankOf(g.Category)).Take(Merge(current) ? 1 : 2);
            foreach (HandGoal goal in targets)
            {
                string needed = goal.Category == HandCategory.Flush
                    ? CardText.Of(goal.Cards[0]).Substring(CardText.Of(goal.Cards[0]).Length - 1) + " 무늬"
                    : string.Join("/", goal.Cards.Take(3).Select(CardText.Of)) + (goal.Cards.Count > 3 ? " 등" : "");
                if (text.Length > 0) text += "\n";
                text += HandCategoryNames.Of(goal.Category) + " 노리기 · " + needed + " 1장"
                    + (Merge(goal.Category) ? " · 합치기 가능" : "");
            }
            return text.Length > 0 ? text : "손패를 다듬고 원하는 유닛을 소환하세요";
        }
    }
}
