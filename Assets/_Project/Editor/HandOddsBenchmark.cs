using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PokerDefense.Poker;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.EditorTools
{
    /**
     * HandOddsBenchmark
     *
     * 개발 전용 교체 확률 계산 시간 비교
     * 순차 계산·작업 전달·병렬 계산을 측정하며 게임 프레임 시간과 구분
     */
    public static class HandOddsBenchmark
    {
        public static Task<string> Pending { get; private set; }

        [MenuItem("DevMode/교체 확률 벤치마크")]
        public static void Start()
        {
            if (Pending != null && Pending.IsCompleted == false)
            {
                return;
            }

            string environment = "Unity=" + Application.unityVersion + "; platform=" + Application.platform
                + "; CPU=" + SystemInfo.processorType + "; logicalCores=" + Environment.ProcessorCount
                + "; runtime=" + Environment.Version + "; samples=9; warmup=2; Editor, not Player";
            string path = Path.GetFullPath("Artifacts/Threading/category-comparison.csv");
            Pending = Task.Run(() => Run(environment, path));
            // Pending을 아무도 기다리지 않으므로, 실패는 여기서 직접 관찰해 콘솔에 남긴다.
            // (System.Diagnostics.Debug와 이름이 겹쳐 UnityEngine.Debug로 명시)
            Pending.ContinueWith(task => UnityEngine.Debug.LogException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }

        static string Run(string environment, string path)
        {
            StringBuilder report = new StringBuilder(environment + "\nseed,slots,mode,medianMs,p95Ms,medianCallerAllocatedBytes\n");
            foreach (int seed in new[] { 17, 42 })
            {
                Deck deck = new Deck(seed);
                Card[] hand = deck.DrawUpTo(5);
                List<Card> pool = Deck.BuildCards(hand);
                for (int k = 1; k <= 4; k++)
                {
                    int[] indices = new int[k];
                    for (int i = 0; i < k; i++)
                    {
                        indices[i] = i;
                    }

                    foreach (string mode in new[] { "reference-sequential", "inline-sequential", "worker-sequential", "worker-parallel-2", "worker-parallel-4" })
                    {
                        Action compute = () =>
                        {
                            if (mode == "reference-sequential")
                            {
                                ReferenceFind(hand, pool, indices);
                            }
                            else if (mode == "inline-sequential")
                            {
                                HandOdds.Find(hand, pool, indices, maxSlots: 4);
                            }
                            else if (mode == "worker-sequential")
                            {
                                Task.Run(() => HandOdds.Find(hand, pool, indices, maxSlots: 4)).GetAwaiter().GetResult();
                            }
                            else
                            {
                                Task.Run(() => HandOdds.FindParallel(hand, pool, indices, mode.EndsWith("2") ? 2 : 4, maxSlots: 4)).GetAwaiter().GetResult();
                            }
                        };
                        for (int i = 0; i < 2; i++)
                        {
                            compute();
                        }

                        double[] elapsed = new double[9];
                        long[] allocations = new long[9];
                        for (int i = 0; i < elapsed.Length; i++)
                        {
                            long before = GC.GetAllocatedBytesForCurrentThread();
                            Stopwatch watch = Stopwatch.StartNew();
                            compute();
                            elapsed[i] = watch.Elapsed.TotalMilliseconds;
                            allocations[i] = GC.GetAllocatedBytesForCurrentThread() - before;
                        }

                        Array.Sort(elapsed);
                        Array.Sort(allocations);
                        report.Append(seed).Append(',').Append(k).Append(',').Append(mode).Append(',')
                            .Append(elapsed[4].ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                            .Append(elapsed[8].ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                            .Append(mode == "inline-sequential" && allocations[4] > 0 ? allocations[4].ToString() : "NA").AppendLine();
                    }

                    // 장시간 측정 중에도 완료된 구간을 확인할 수 있다.
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, report.ToString());
                }
            }

            return report.ToString();
        }

        // 최적화 전 Evaluate 경로를 재측정하기 위한 Editor 전용 기준 구현.
        static List<HandOddsEntry> ReferenceFind(Card[] hand, IReadOnlyList<Card> pool, int[] indices)
        {
            Card[] copy = (Card[])hand.Clone();
            Dictionary<HandCategory, int> counts = new Dictionary<HandCategory, int>();
            int total = 0;
            Action<int, int> visit = null;
            visit = (start, depth) =>
            {
                if (depth == indices.Length)
                {
                    HandCategory category = HandEvaluator.Evaluate(copy).Category;
                    counts[category] = counts.TryGetValue(category, out int count) ? count + 1 : 1;
                    total++;
                    return;
                }

                for (int i = start; i <= pool.Count - (indices.Length - depth); i++)
                {
                    copy[indices[depth]] = pool[i];
                    visit(i + 1, depth + 1);
                }
            };
            visit(0, 0);
            List<HandOddsEntry> result = new List<HandOddsEntry>();
            foreach (KeyValuePair<HandCategory, int> pair in counts)
            {
                result.Add(new HandOddsEntry(pair.Key, pair.Value, total));
            }

            result.Sort((a, b) => HandRarity.RankOf(b.Category).CompareTo(HandRarity.RankOf(a.Category)));
            return result;
        }
    }
}
