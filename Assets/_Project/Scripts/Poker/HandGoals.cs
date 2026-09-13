using System.Collections.Generic;

namespace PokerDefense.Poker
{
    public sealed class HandGoal
    {
        public HandCategory Category { get; }
        public List<Card> Cards { get; } = new List<Card>();

        public HandGoal(HandCategory category)
        {
            Category = category;
        }
    }

    // 바꿀 수 있는 한 자리와 아직 나오지 않은 카드만 검사한다. 확률이나 추천 순위를 만들지 않는다.
    public static class HandGoals
    {
        public static List<HandGoal> Find(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<bool> changeable)
        {
            var result = new List<HandGoal>();
            var copy = new Card[HandEvaluator.HandSize];
            for (int i = 0; i < copy.Length; i++) copy[i] = hand[i];
            for (int i = 0; i < copy.Length; i++)
            {
                if (changeable[i] == false) continue;
                foreach (Card card in unseen)
                {
                    copy[i] = card;
                    HandCategory category = HandEvaluator.Evaluate(copy).Category;
                    HandGoal goal = result.Find(g => g.Category == category);
                    if (goal == null)
                    {
                        goal = new HandGoal(category);
                        result.Add(goal);
                    }
                    if (goal.Cards.Contains(card) == false) goal.Cards.Add(card);
                }
                copy[i] = hand[i];
            }
            return result;
        }
    }
}
