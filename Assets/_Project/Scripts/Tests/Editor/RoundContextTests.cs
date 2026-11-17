using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * RoundContextTests
     *
     * 한 라운드의 손패 뽑기 -> 일반 교체 -> 확정 순서와 교체 규칙을 검사하는 클래스
     */
    public class RoundContextTests
    {
        static RoundContext Drawn(int seed = 1)
        {
            var round = new RoundContext(seed);
            round.Draw();
            return round;
        }

        static readonly int[] None = new int[0];

        [Test]
        public void 교체한_인덱스만_바뀐다()
        {
            var round = Drawn();
            var before = round.Hand.ToArray();

            round.Exchange(new[] { 0, 3 });

            Assert.AreNotEqual(before[0], round.Hand[0]);
            Assert.AreNotEqual(before[3], round.Hand[3]);
            Assert.AreEqual(before[1], round.Hand[1]);
            Assert.AreEqual(before[2], round.Hand[2]);
            Assert.AreEqual(before[4], round.Hand[4]);
        }

        [Test]
        public void 교체한_자리는_잠긴다()
        {
            var round = Drawn();

            round.Exchange(new[] { 1, 4 });

            Assert.IsTrue(round.IsLocked(1));
            Assert.IsTrue(round.IsLocked(4));
            Assert.IsFalse(round.IsLocked(0));
            Assert.AreEqual(3, round.ExchangeableCount);
        }

        [Test]
        public void 교체는_한_라운드에_여러_번_할_수_있다()
        {
            var round = Drawn();

            round.Exchange(new[] { 0 });
            round.Exchange(new[] { 1 });
            round.Exchange(new[] { 2, 3 });

            Assert.AreEqual(RoundPhase.Exchange, round.Phase);
            Assert.AreEqual(1, round.ExchangeableCount);
        }

        [Test]
        public void 나눠서_교체해도_한_번에_교체한_것과_손패가_같다()
        {
            var stepwise = Drawn(seed: 99);
            stepwise.Exchange(new[] { 0 });
            stepwise.Exchange(new[] { 1 });
            stepwise.Exchange(new[] { 2 });

            var atOnce = Drawn(seed: 99);
            atOnce.Exchange(new[] { 0, 1, 2 });

            CollectionAssert.AreEqual(atOnce.Hand, stepwise.Hand);
        }

        [Test]
        public void 이미_교체한_자리는_다시_교체할_수_없다()
        {
            var round = Drawn();
            round.Exchange(new[] { 2 });

            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 2 }));
        }

        [Test]
        public void 잠긴_자리가_섞여_있으면_손패를_한_장도_바꾸지_않는다()
        {
            var round = Drawn();
            round.Exchange(new[] { 0 });
            var before = round.Hand.ToArray();

            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 1, 0 }));

            CollectionAssert.AreEqual(before, round.Hand);
            Assert.IsFalse(round.IsLocked(1), "실패한 교체가 1번 자리를 잠갔다");
        }

        [Test]
        public void 교체는_덱의_다음_카드를_쓴다()
        {
            // 손패 5장 + 교체 5장 = 10장이 전부 서로 달라야 함
            var round = Drawn();
            var before = round.Hand.ToArray();

            round.Exchange(new[] { 0, 1, 2, 3, 4 });

            var all = new List<Card>(before);
            all.AddRange(round.Hand);

            Assert.AreEqual(10, all.Distinct().Count());
        }

        [Test]
        public void 드로우_전에는_교체할_수_없다()
        {
            var round = new RoundContext(1);

            Assert.Throws<InvalidOperationException>(() => round.Exchange(None));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void 범위를_벗어난_인덱스는_예외를_던진다(int index)
        {
            var round = Drawn();

            Assert.Throws<ArgumentOutOfRangeException>(() => round.Exchange(new[] { index }));
        }

        [Test]
        public void 한_번의_교체에_같은_인덱스를_두_번_주면_예외를_던진다()
        {
            var round = Drawn();

            Assert.Throws<ArgumentException>(() => round.Exchange(new[] { 2, 2 }));
        }

        [Test]
        public void 교체를_끝내지_않으면_판정할_수_없다()
        {
            var round = Drawn();

            Assert.Throws<InvalidOperationException>(() => round.Evaluate());
        }

        [Test]
        public void 한_장도_바꾸지_않고_바로_확정할_수_있다()
        {
            var round = Drawn();
            var before = round.Hand.ToArray();

            round.FinishExchange();
            round.Evaluate();

            CollectionAssert.AreEqual(before, round.Hand);
        }

        [Test]
        public void 페이즈는_드로우_교체_판정_순서로_진행한다()
        {
            var round = new RoundContext(1);
            Assert.AreEqual(RoundPhase.Draw, round.Phase);

            round.Draw();
            Assert.AreEqual(RoundPhase.Exchange, round.Phase);

            round.Exchange(new[] { 0 });
            Assert.AreEqual(RoundPhase.Exchange, round.Phase, "교체해도 페이즈는 Exchange에 머문다");

            round.FinishExchange();
            Assert.AreEqual(RoundPhase.Evaluate, round.Phase);

            round.Evaluate();
            Assert.AreEqual(RoundPhase.Place, round.Phase);
        }

    }
}
