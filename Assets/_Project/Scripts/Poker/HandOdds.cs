using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        // 4장은 병렬 계산(FindParallel) 도입 후 벤치마크로 확인하고 올린 상한이다
        public const int MaxSlots = 4;

        public static List<HandOddsEntry> Find(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<int> indices,
            CancellationToken cancellation = default, int maxSlots = MaxSlots)
        {
            cancellation.ThrowIfCancellationRequested();
            if (IsValid(hand, unseen, indices, maxSlots) == false)
            {
                return null;
            }

            Card[] copy = new Card[HandEvaluator.HandSize];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = hand[i];
            }

            Dictionary<HandCategory, int> tally = new Dictionary<HandCategory, int>();
            int total = 0;
            Card[] combo = new Card[indices.Count];

            Combine(unseen, 0, combo, 0, () =>
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    copy[indices[i]] = combo[i];
                }

                HandCategory category = HandEvaluator.CategoryOf(copy);
                tally[category] = tally.TryGetValue(category, out int count) ? count + 1 : 1;
                total++;
            }, cancellation);

            List<HandOddsEntry> result = new List<HandOddsEntry>();
            foreach (KeyValuePair<HandCategory, int> pair in tally)
            {
                result.Add(new HandOddsEntry(pair.Key, pair.Value, total));
            }

            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }

        // 첫 카드 인덱스별 조합은 서로 겹치지 않는다. 작업마다 버퍼·집계를 소유한다.
        public static List<HandOddsEntry> FindParallel(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen,
            IReadOnlyList<int> indices, int maxDegree = 2, CancellationToken cancellation = default,
            int maxSlots = MaxSlots)
        {
            if (maxDegree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegree));
            }

            cancellation.ThrowIfCancellationRequested();
            if (IsValid(hand, unseen, indices, maxSlots) == false)
            {
                return null;
            }

            if (maxDegree == 1)
            {
                return Find(hand, unseen, indices, cancellation, maxSlots);
            }

            int partitions = unseen.Count - indices.Count + 1;
            Dictionary<HandCategory, int>[] partials = new Dictionary<HandCategory, int>[partitions];
            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegree,
                CancellationToken = cancellation,
            };

            Parallel.For(0, partitions, options, first =>
            {
                Card[] copy = new Card[HandEvaluator.HandSize];
                for (int i = 0; i < copy.Length; i++)
                {
                    copy[i] = hand[i];
                }

                Card[] combo = new Card[indices.Count];
                Dictionary<HandCategory, int> tally = new Dictionary<HandCategory, int>();
                combo[0] = unseen[first];
                Combine(unseen, first + 1, combo, 1, () =>
                {
                    for (int i = 0; i < indices.Count; i++)
                    {
                        copy[indices[i]] = combo[i];
                    }

                    HandCategory category = HandEvaluator.CategoryOf(copy);
                    tally[category] = tally.TryGetValue(category, out int count) ? count + 1 : 1;
                }, cancellation);
                partials[first] = tally;
            });

            cancellation.ThrowIfCancellationRequested();
            Dictionary<HandCategory, int> merged = new Dictionary<HandCategory, int>();
            int total = 0;
            foreach (Dictionary<HandCategory, int> part in partials)
            {
                foreach (KeyValuePair<HandCategory, int> pair in part)
                {
                    merged[pair.Key] = merged.TryGetValue(pair.Key, out int count) ? count + pair.Value : pair.Value;
                    total += pair.Value;
                }
            }

            List<HandOddsEntry> result = new List<HandOddsEntry>();
            foreach (KeyValuePair<HandCategory, int> pair in merged)
            {
                result.Add(new HandOddsEntry(pair.Key, pair.Value, total));
            }

            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }

        static bool IsValid(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<int> indices, int maxSlots)
        {
            if (maxSlots < 1 || maxSlots > HandEvaluator.HandSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlots));
            }

            if (hand == null || hand.Count != HandEvaluator.HandSize || unseen == null || indices == null
                || indices.Count == 0 || indices.Count > maxSlots || unseen.Count < indices.Count) return false;
            int selected = 0;
            foreach (int index in indices)
            {
                if (index < 0 || index >= HandEvaluator.HandSize || (selected & (1 << index)) != 0)
                {
                    return false;
                }

                selected |= 1 << index;
            }

            return true;
        }

        // 반복되지 않는 조합(순서 무관)을 전부 만들어 onCombo로 넘긴다
        static void Combine(IReadOnlyList<Card> pool, int start, Card[] buffer, int depth, Action onCombo, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (depth == buffer.Length)
            {
                onCombo();
                return;
            }

            for (int i = start; i <= pool.Count - (buffer.Length - depth); i++)
            {
                buffer[depth] = pool[i];
                Combine(pool, i + 1, buffer, depth + 1, onCombo, cancellation);
            }
        }
    }
}
