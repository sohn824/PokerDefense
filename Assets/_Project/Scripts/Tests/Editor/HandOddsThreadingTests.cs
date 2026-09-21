using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEngine;
using UnityEngine.TestTools;

namespace PokerDefense.Tests
{
    public class HandOddsThreadingTests
    {
        [Test]
        public void 종류_전용_판정은_기존_판정과_모든_족보에서_일치한다()
        {
            string[] examples = { "As Ks Qs Js Ts", "As 2s 3s 4s 5s", "3s 4s 5s 6s 7s",
                "As Ah Ad Ac 2s", "As Ah Ad Kc Kh", "2s 4s 6s 8s Ts", "As Kh Qd Jc Ts",
                "As 2h 3d 4c 5s", "3s 4h 5d 6c 7s", "As Ah Ad 2c 3s", "As Ah Kd Kc 3s",
                "As Ah 2d 4c 6s", "As Kh 2d 4c 6s" };
            foreach (string example in examples)
            {
                var hand = Hand.Of(example);
                Assert.AreEqual(HandEvaluator.Evaluate(hand).Category, HandEvaluator.CategoryOf(hand), example);
            }

            for (int seed = 0; seed < 10000; seed++)
            {
                var hand = new Deck(seed).DrawUpTo(5);
                Assert.AreEqual(HandEvaluator.Evaluate(hand).Category, HandEvaluator.CategoryOf(hand), "seed=" + seed);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void 순차와_병렬의_모든_집계가_일치한다(int degree)
        {
            for (int seed = 0; seed < 8; seed++)
            {
                var deck = new Deck(seed);
                var hand = deck.DrawUpTo(5);
                var pool = deck.DrawUpTo(18);
                for (int k = 1; k <= 4; k++)
                {
                    var indices = Enumerable.Range(0, k).ToArray();
                    var sequential = HandOdds.Find(hand, pool, indices, maxSlots: 4);
                    var parallel = HandOdds.FindParallel(hand, pool, indices, degree, maxSlots: 4);
                    CollectionAssert.AreEqual(sequential, parallel);
                    int combinations = 1;
                    for (int i = 1; i <= k; i++)
                    {
                        combinations = combinations * (pool.Length - i + 1) / i;
                    }

                    Assert.AreEqual(combinations, parallel.Sum(x => x.Count));
                    Assert.IsTrue(parallel.All(x => x.Total == combinations));
                }
            }
        }

        [Test]
        public void 입력_복사와_기본_상한을_지킨다()
        {
            var hand = new Deck(1).DrawUpTo(5);
            var pool = Deck.BuildCards(hand).ToArray();
            var indices = new[] { 0, 1, 2 };
            var request = new HandOddsRequest(1, HandOddsSource.Exchange, hand, pool, indices);
            var expected = HandOdds.Find(request.Hand, request.Unseen, request.Indices);
            hand[0] = default;
            pool[0] = default;
            indices[0] = 4;
            CollectionAssert.AreEqual(expected, HandOdds.FindParallel(request.Hand, request.Unseen, request.Indices));
            Assert.IsNull(HandOdds.FindParallel(request.Hand, request.Unseen, new[] { 0, 1, 2, 3, 4 })); // 기본 상한(4)보다 한 자리 많음
            Assert.IsNull(HandOdds.FindParallel(request.Hand, request.Unseen, new[] { -1 }));
            Assert.IsNull(HandOdds.FindParallel(request.Hand, request.Unseen, new[] { 0, 0 }));
        }

        [Test]
        public void 취소된_계산은_부분_확률을_반환하지_않는다()
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                var request = Request(1);
                Assert.Throws<OperationCanceledException>(() => HandOdds.Find(request.Hand, request.Unseen, request.Indices, source.Token));
                Assert.Throws<OperationCanceledException>(() => HandOdds.FindParallel(request.Hand, request.Unseen, request.Indices, 2, source.Token));
            }
        }

        [Test]
        public void 스냅샷은_현재_교체_조건을_검사한다()
        {
            var round = new RoundContext(42);
            round.Draw();
            Assert.IsNull(round.CaptureOdds(1, HandOddsSource.Assist, new[] { 0 }));
            Assert.IsNull(round.CaptureOdds(1, HandOddsSource.Exchange, new[] { 5 }));
            round.Exchange(new[] { 0 });
            Assert.IsNull(round.CaptureOdds(1, HandOddsSource.Exchange, new[] { 0 }));
            Assert.IsNotNull(round.CaptureOdds(1, HandOddsSource.Assist, new[] { 0 }));
            round.TryRevealCandidates(0);
            Assert.IsNull(round.CaptureOdds(2, HandOddsSource.Exchange, new[] { 1 }));
            Assert.IsNull(round.CaptureOdds(2, HandOddsSource.Assist, new[] { 0 }));
        }

        [UnityTest]
        public IEnumerator 연타는_최신_요청만_실행하고_이전_결과를_버린다()
        {
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var versions = new List<int>();
                int owner = Thread.CurrentThread.ManagedThreadId;
                int workerThread = owner;
                using (var worker = new HandOddsWorker((request, token) =>
                {
                    workerThread = Thread.CurrentThread.ManagedThreadId;
                    lock (versions)
                    {
                        versions.Add(request.Version);
                    }

                    if (request.Version == 1)
                    {
                        started.Set();
                        if (release.Wait(10000) == false)
                        {
                            throw new TimeoutException();
                        }

                        // 취소를 무시하는 계산도 화면에 오래된 결과를 게시하면 안 된다.
                    }

                    return Result(request.Version);
                }))
                {
                    try
                    {
                        worker.Submit(Request(1));
                        yield return Until(() => started.IsSet);
                        for (int i = 2; i <= 20; i++)
                        {
                            worker.Submit(Request(i));
                        }

                        worker.Submit(Request(20));
                        Assert.IsNull(worker.Result);
                        release.Set();
                        yield return Until(() =>
                        {
                            worker.Poll();
                            return worker.State == HandOddsState.Ready;
                        });
                        CollectionAssert.AreEqual(new[] { 1, 20 }, versions);
                        Assert.AreEqual(20, worker.Result[0].Count);
                        Assert.AreNotEqual(owner, workerThread);
                    }
                    finally
                    {
                        release.Set();
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator 취소와_종료는_대기_요청을_제거한다()
        {
            using (var started = new ManualResetEventSlim())
            using (var stopped = new ManualResetEventSlim())
            {
                int calls = 0;
                var worker = new HandOddsWorker((request, token) =>
                {
                    Interlocked.Increment(ref calls);
                    started.Set();
                    token.WaitHandle.WaitOne(10000);
                    stopped.Set();
                    token.ThrowIfCancellationRequested();
                    return Result(1);
                });
                try
                {
                    worker.Submit(Request(1));
                    yield return Until(() => started.IsSet);
                    worker.Submit(Request(2));
                    worker.Cancel();
                    worker.Dispose();
                    yield return Until(() => stopped.IsSet);
                    Assert.AreEqual(1, calls);
                    Assert.IsFalse(worker.Poll());
                    Assert.AreEqual(HandOddsState.Idle, worker.State);
                    Assert.IsNull(worker.Result);
                    Assert.Throws<ObjectDisposedException>(() => worker.Submit(Request(3)));
                }
                finally
                {
                    worker.Dispose();
                }
            }
        }

        [UnityTest]
        public IEnumerator 오류를_회수하고_다음_요청은_정상_실행한다()
        {
            using (var worker = new HandOddsWorker((request, token) =>
            {
                if (request.Version == 1)
                {
                    throw new InvalidOperationException("test failure");
                }

                return Result(2);
            }))
            {
                worker.Submit(Request(1));
                yield return Until(() =>
                {
                    worker.Poll();
                    return worker.State == HandOddsState.Failed;
                });
                Assert.IsInstanceOf<InvalidOperationException>(worker.Error);
                worker.Submit(Request(2));
                yield return Until(() =>
                {
                    worker.Poll();
                    return worker.State == HandOddsState.Ready;
                });
                Assert.IsNull(worker.Error);
                Assert.AreEqual(2, worker.Result[0].Count);
            }
        }

        [Test]
        public void 실행_도중_취소된_병렬_계산은_집계를_게시하지_않는다()
        {
            using (var source = new CancellationTokenSource())
            {
                var hand = new Deck(8).DrawUpTo(5);
                var pool = new CancellingCards(Deck.BuildCards(hand), source);
                Assert.Throws<OperationCanceledException>(() => HandOdds.FindParallel(hand, pool,
                    new[] { 0, 1, 2 }, 2, source.Token));
                Assert.IsTrue(source.IsCancellationRequested);
            }
        }

        sealed class CancellingCards : IReadOnlyList<Card>
        {
            readonly IReadOnlyList<Card> cards;
            readonly CancellationTokenSource source;
            int reads;

            public CancellingCards(IReadOnlyList<Card> cards, CancellationTokenSource source)
            {
                this.cards = cards;
                this.source = source;
            }

            public int Count => cards.Count;

            public Card this[int index]
            {
                get
                {
                    if (Interlocked.Increment(ref reads) == 1000)
                    {
                        source.Cancel();
                    }

                    return cards[index];
                }
            }

            public IEnumerator<Card> GetEnumerator() => cards.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [UnityTest]
        public IEnumerator 컨트롤러는_새_손패와_비활성화에서_결과를_무효화한다()
        {
            var go = new GameObject("OddsLifecycleTest");
            var controller = go.AddComponent<RoundController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var update = typeof(RoundController).GetMethod("Update", flags);
            // EditMode의 일반 MonoBehaviour는 활성화 콜백을 자동 실행하지 않는다.
            var enable = typeof(RoundController).GetMethod("OnEnable", flags);
            var disable = typeof(RoundController).GetMethod("OnDisable", flags);
            enable.Invoke(controller, null);
            try
            {
                controller.StartRound();
                controller.RequestOdds(HandOddsSource.Exchange, new[] { 0, 1, 2 });
                controller.StartRound();
                Assert.AreEqual(HandOddsState.Idle, controller.OddsState);
                Assert.IsNull(controller.Odds);
                controller.RequestOdds(HandOddsSource.Exchange, new[] { 0 });
                controller.enabled = false;
                disable.Invoke(controller, null);
                Assert.AreEqual(HandOddsState.Idle, controller.OddsState);
                controller.enabled = true;
                enable.Invoke(controller, null);
                controller.RequestOdds(HandOddsSource.Exchange, new[] { 1 });
                yield return Until(() =>
                {
                    update.Invoke(controller, null);
                    return controller.OddsState == HandOddsState.Ready;
                });
                CollectionAssert.AreEqual(controller.FindExchangeOdds(new[] { 1 }), controller.Odds);
                controller.SetAssistOddsActive(true);
                controller.RequestOdds(HandOddsSource.Exchange, new[] { 2 });
                Assert.AreEqual(HandOddsState.Idle, controller.OddsState);
            }
            finally
            {
                typeof(RoundController).GetMethod("OnDestroy", flags).Invoke(controller, null);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static HandOddsRequest Request(int version)
        {
            var hand = new Deck(17).DrawUpTo(5);
            return new HandOddsRequest(version, HandOddsSource.Exchange, hand, Deck.BuildCards(hand), new[] { 0, 1, 2 });
        }

        static List<HandOddsEntry> Result(int count)
        {
            return new List<HandOddsEntry>
            {
                new HandOddsEntry(HandCategory.OnePair, count, count),
            };
        }

        static IEnumerator Until(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (condition() == false)
            {
                Assert.Less(watch.Elapsed.TotalSeconds, 15, "비동기 작업 시간 초과");
                yield return null;
            }
        }
    }
}
