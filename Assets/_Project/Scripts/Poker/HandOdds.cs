using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PokerDefense.Poker
{
    // 족보별 경우의 수 - 나오는 조합 수와 전체 조합 수로 확률을 계산
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

        // 전체 조합 수
        public int Total { get; }

        public float Probability => Total == 0 ? 0f : (float)Count / Total;
    }

    // HandOdds - 손패 교체로 나올 수 있는 족보의 경우의 수를 세는 클래스
    public static class HandOdds
    {
        // 한 번에 선택할 수 있는 자리 수 상한
        public const int MaxSlots = 4;

        // Find - 선택한 자리를 채우는 조합을 하나씩 만들어보며 족보별 조합 수를 순차로 셈
        // (교체할 카드가 적으면 병렬로 나누고 합치는 비용이 더 커서 이 메소드를 사용)
        public static List<HandOddsEntry> Find(
            IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<int> indices,
            CancellationToken cancellation = default, int maxSlots = MaxSlots)
        {
            cancellation.ThrowIfCancellationRequested();
            // 계산할 수 없는 입력이면 null 반환
            if (IsValid(hand, unseen, indices, maxSlots) == false)
            {
                return null;
            }

            // 넘겨받은 손패는 읽기 전용이므로 카드를 갈아 끼울 작업용 복사본을 따로 만듦
            Card[] copy = new Card[HandEvaluator.HandSize];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = hand[i];
            }

            // 족보별로 몇 번 나왔는지 세는 표 (tally)
            Dictionary<HandCategory, int> tally = new Dictionary<HandCategory, int>();
            int total = 0;
            Card[] combo = new Card[indices.Count];

            // 조합이 하나 완성될 때마다 호출됨
            // (47장에서 4장을 고르면 C(47,4) = 178,365번)
            // 고른 자리만 새 카드로 갈아 끼우고, 족보를 판정해 표에 더함
            Action onCombo = () =>
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    copy[indices[i]] = combo[i];
                }

                // Evaluate는 족보를 만든 카드 목록(KeyCards)까지 만들어 무거움
                // 확률 계산에는 족보 종류만 필요하므로 가벼운 CategoryOf를 사용
                HandCategory category = HandEvaluator.CategoryOf(copy);
                tally[category] = tally.TryGetValue(category, out int count) ? count + 1 : 1;
                total++;
            };

            Combine(unseen, 0, combo, 0, onCombo, cancellation);

            List<HandOddsEntry> result = new List<HandOddsEntry>();
            foreach (KeyValuePair<HandCategory, int> pair in tally)
            {
                result.Add(new HandOddsEntry(pair.Key, pair.Value, total));
            }

            // 희귀한 족보가 위로 오게 정렬
            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }

        // FindParallel - Find와 같은 계산을 첫 카드 기준으로 나눠 여러 스레드로 병렬 실행
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

            // 스레드를 하나만 쓰면 나눠도 나누고 합치는 비용만 늘어나므로 Find로 처리
            if (maxDegree == 1)
            {
                return Find(hand, unseen, indices, cancellation, maxSlots);
            }

            // 조합을 첫 카드가 같은 것끼리 묶어 병렬로 계산 (47장에서 4장이면 44묶음)
            // 순서만 다른 조합은 한 번만 세므로 첫 카드 뒤에는 덱에서 그보다 뒤에 있는 카드만 올 수 있음
            // 그래서 첫 카드가 덱 앞쪽일수록 묶음이 크고,
            // 스레드마다 일이 고르게 안 나뉘게 되므로 스레드 수에 비례해 빨라지진 않음
            int partitions = unseen.Count - indices.Count + 1;
            Dictionary<HandCategory, int>[] partials = new Dictionary<HandCategory, int>[partitions];
            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegree,
                CancellationToken = cancellation,
            };

            // 위에서 나눈 묶음마다 조합을 만들어 족보별 개수를 세는 작업을 병렬로 실행
            // 묶음마다 작업용 배열과 집계 표를 따로 만들어 쓰므로 별도로 lock을 걸 필요는 없음
            // 여러 스레드가 함께 쓰는 partials도 각자 자기 번호 칸에만 쓰므로 겹치지 않음
            Parallel.For(0, partitions, options, first =>
            {
                Card[] copy = new Card[HandEvaluator.HandSize];
                for (int i = 0; i < copy.Length; i++)
                {
                    copy[i] = hand[i];
                }

                Card[] combo = new Card[indices.Count];
                Dictionary<HandCategory, int> tally = new Dictionary<HandCategory, int>();
                // Find의 onCombo와 같지만 total은 세지 않음 (전체 조합 수는 아래 합산에서 구함)
                Action onCombo = () =>
                {
                    for (int i = 0; i < indices.Count; i++)
                    {
                        copy[indices[i]] = combo[i];
                    }

                    HandCategory category = HandEvaluator.CategoryOf(copy);
                    tally[category] = tally.TryGetValue(category, out int count) ? count + 1 : 1;
                };

                // 첫 칸은 이 묶음의 카드로 고정하고, 나머지 자리만 Combine으로 채움
                combo[0] = unseen[first];
                Combine(unseen, first + 1, combo, 1, onCombo, cancellation);
                partials[first] = tally;
            });

            // Parallel.For는 모든 묶음이 끝나야 돌아오므로 여기부터는 스레드 하나로 실행됨
            // 묶음별 결과를 합치면서 total도 함께 셈
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

            // 희귀한 족보가 위로 오게 정렬
            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }

        // IsValid - 계산할 수 있는 입력인지 확인
        static bool IsValid(IReadOnlyList<Card> hand, IReadOnlyList<Card> unseen, IReadOnlyList<int> indices, int maxSlots)
        {
            if (maxSlots < 1 || maxSlots > HandEvaluator.HandSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlots));
            }

            // 손패 장수가 안 맞거나, 고른 자리가 없거나 상한을 넘거나, 덱이 모자라면 계산하지 않음
            if (hand == null || hand.Count != HandEvaluator.HandSize || unseen == null || indices == null
                || indices.Count == 0 || indices.Count > maxSlots || unseen.Count < indices.Count) return false;

            // 같은 자리를 두 번 고르면 조합 수가 틀어지므로, 고른 자리를 비트로 표시해 중복을 걸러냄
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

        // Combine - 덱에서 뽑을 수 있는 조합을 하나씩 만들어 onCombo에 넘김 (순서만 다른 조합은 한 번만 만듦)
        // 배열(buffer) 하나를 계속 덮어쓰며 재귀하므로 이 안에서는 병렬화하지 않음 (여러 스레드가 같은 배열을 건드리면 조합이 섞임)
        static void Combine(IReadOnlyList<Card> pool, int start, Card[] buffer, int depth, Action onCombo, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            // 버퍼를 다 채웠으면 조합 하나가 완성된 것
            if (depth == buffer.Length)
            {
                onCombo();
                return;
            }

            // start부터 고르므로 앞에서 쓴 카드는 다시 고르지 않음 (순서만 다른 중복 조합이 안 생김)
            // 반복은 남은 자리를 채울 카드가 남아 있는 데까지만
            for (int i = start; i <= pool.Count - (buffer.Length - depth); i++)
            {
                buffer[depth] = pool[i];
                Combine(pool, i + 1, buffer, depth + 1, onCombo, cancellation);
            }
        }
    }
}
