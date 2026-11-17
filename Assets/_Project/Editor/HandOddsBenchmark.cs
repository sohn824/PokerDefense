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
     * 교체 확률 계산 방식별 시간을 측정해 CSV로 남기는 클래스 (개발 전용)
     * HandOddsWorker의 병렬 기준(자리 수·스레드 수)을 정하는 근거로 씀
     * 측정값은 에디터 기준이며 게임 프레임 시간과 다름
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

            // Unity API는 메인 스레드에서만 쓸 수 있어 측정 전에 환경 정보를 미리 읽음
            string environment = "Unity=" + Application.unityVersion + "; platform=" + Application.platform
                + "; CPU=" + SystemInfo.processorType + "; logicalCores=" + Environment.ProcessorCount
                + "; runtime=" + Environment.Version + "; samples=9; warmup=2; Editor, not Player";
            string path = Path.GetFullPath("Artifacts/Threading/category-comparison.csv");
            // 측정 중에도 에디터가 멈추지 않게 백그라운드에서 돌림
            Pending = Task.Run(() => Run(environment, path));
            // Pending을 기다리는 곳이 없어 실패를 여기서 콘솔에 남김
            Pending.ContinueWith(task => UnityEngine.Debug.LogException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
            // 끝난 것을 알 수 있게 결과 파일 경로를 남김
            Pending.ContinueWith(task => UnityEngine.Debug.Log("교체 확률 벤치마크 완료: " + path), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        // 시드 2개 × 교체 1~4장 × 계산 방식 5개를 재서 한 줄씩 기록함
        static string Run(string environment, string path)
        {
            StringBuilder report = new StringBuilder(environment + "\nseed,slots,mode,medianMs,p95Ms,medianCallerAllocatedBytes\n");
            // 시드는 손패를 정함, 손패 하나에 치우치지 않도록 2개를 쓰고 다시 재도 비교할 수 있게 고정
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

                    // 최적화 전 기준 / 현재 방식 / Worker 스레드 하나 / Worker 2개·4개로 나눠 계산
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
                        // 첫 실행은 코드를 기계어로 바꾸고 스레드를 준비하는 시간이 섞여 느리므로 2회는 기록하지 않음
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

                        // 9번 잰 값을 정렬해 가운데인 5번째를 보통 걸리는 시간, 가장 느린 9번째를 최악에 가까운 시간으로 기록함
                        Array.Sort(elapsed);
                        Array.Sort(allocations);
                        // 메모리 할당량은 지금 스레드에서 쓴 양만 잴 수 있으므로
                        // 같은 스레드에서 계산하는 inline 방식만 숫자로 적고 나머지는 NA(값 없음)로 적음
                        // 에디터의 Mono는 이 값을 항상 0으로 돌려주므로 0도 잴 수 없는 것으로 보고 NA로 적음
                        report.Append(seed).Append(',').Append(k).Append(',').Append(mode).Append(',')
                            .Append(elapsed[4].ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                            .Append(elapsed[8].ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                            .Append(mode == "inline-sequential" && allocations[4] > 0 ? allocations[4].ToString() : "NA").AppendLine();
                    }

                    // 측정이 길어도 끝난 구간을 확인할 수 있게 중간중간 저장
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, report.ToString());
                }
            }

            return report.ToString();
        }

        // 조합마다 무거운 Evaluate로 족보를 판정하던 최적화 전 방식, 현재 방식과 속도를 비교하려고 남겨 둠
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
