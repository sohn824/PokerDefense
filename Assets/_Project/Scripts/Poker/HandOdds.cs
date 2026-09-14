using System;
using System.Collections.Generic;

namespace PokerDefense.Poker
{
    // 선택한 자리를 남은 덱 카드로 채우는 모든 조합 중 하나. 특정 조합을 추천하지 않고 세기만 한다
    public readonly struct HandOddsEntry
    {
        public HandOddsEntry(HandCategory category, int count, int total)
        {
            Category = category;
            Count = count;
            Total = total;
        }

        public HandCategory Category { get; }

        // 이 족보가 나오는 조합 수
        public int Count { get; }

        // 전체 조합 수 (자리가 하나면 남은 덱 장수와 같다)
        public int Total { get; }

        public float Probability => Total == 0 ? 0f : (float)Count / Total;
    }

    // 선택한 자리에 남은 덱 카드가 들어올 때 나올 수 있는 족보의 경우의 수. 최적 선택을 추천하지 않는다
    public static class HandOdds
    {
        // 이 이상 자리를 한꺼번에 채우면 조합 수가 너무 커져 정확히 세지 않는다
        public const int MaxSlots = 3;

        public static List<HandOddsEntry> Find(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<int> indices)
        {
            if (indices == null || indices.Count == 0 || indices.Count > MaxSlots || unseen.Count < indices.Count)
            {
                return null;
            }

            Card[] copy = new Card[HandEvaluator.HandSize];
            for (int i = 0; i < copy.Length; i++) copy[i] = hand[i];

            Dictionary<HandCategory, int> tally = new Dictionary<HandCategory, int>();
            int total = 0;
            Card[] combo = new Card[indices.Count];

            Combine(unseen, 0, combo, 0, () =>
            {
                for (int i = 0; i < indices.Count; i++) copy[indices[i]] = combo[i];
                HandCategory category = HandEvaluator.Evaluate(copy).Category;
                tally[category] = tally.TryGetValue(category, out int count) ? count + 1 : 1;
                total++;
            });

            List<HandOddsEntry> result = new List<HandOddsEntry>();
            foreach (KeyValuePair<HandCategory, int> pair in tally)
            {
                result.Add(new HandOddsEntry(pair.Key, pair.Value, total));
            }

            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }

        // 반복되지 않는 조합(순서 무관)을 전부 만들어 onCombo로 넘긴다
        static void Combine(IReadOnlyList<Card> pool, int start, Card[] buffer, int depth, Action onCombo)
        {
            if (depth == buffer.Length)
            {
                onCombo();
                return;
            }

            for (int i = start; i < pool.Count; i++)
            {
                buffer[depth] = pool[i];
                Combine(pool, i + 1, buffer, depth + 1, onCombo);
            }
        }
    }
}
