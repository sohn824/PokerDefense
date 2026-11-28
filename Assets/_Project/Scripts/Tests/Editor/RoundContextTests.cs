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

        [Test]
        public void 교체한_자리만_덱의_다음_카드로_바뀌고_잠긴다()
        {
            var round = Drawn();
            var before = round.Hand.ToArray();

            round.Exchange(new[] { 0, 3 });

            Assert.AreNotEqual(before[0], round.Hand[0]);
            Assert.AreNotEqual(before[3], round.Hand[3]);
            Assert.AreEqual(before[1], round.Hand[1]);
            Assert.AreEqual(before[2], round.Hand[2]);
            Assert.AreEqual(before[4], round.Hand[4]);

            Assert.IsTrue(round.IsLocked(0));
            Assert.IsTrue(round.IsLocked(3));
            Assert.IsFalse(round.IsLocked(1));
            Assert.AreEqual(3, round.ExchangeableCount);

            // 손패 5장 + 남은 자리 교체 3장 = 7장 + 앞 교체 2장이 전부 서로 달라야 함
            var seen = new List<Card>(before);
            round.Exchange(new[] { 1, 2, 4 });
            seen.AddRange(round.Hand);
            Assert.AreEqual(10, seen.Distinct().Count(), "교체는 덱의 다음 카드를 쓴다");
        }

        [Test]
        public void 나눠서_여러_번_교체해도_한_번에_교체한_것과_손패가_같다()
        {
            var stepwise = Drawn(seed: 99);
            stepwise.Exchange(new[] { 0 });
            stepwise.Exchange(new[] { 1 });
            stepwise.Exchange(new[] { 2 });
            Assert.AreEqual(RoundPhase.Exchange, stepwise.Phase);
            Assert.AreEqual(2, stepwise.ExchangeableCount);

            var atOnce = Drawn(seed: 99);
            atOnce.Exchange(new[] { 0, 1, 2 });

            CollectionAssert.AreEqual(atOnce.Hand, stepwise.Hand);
        }

        [Test]
        public void 잠긴_자리를_다시_교체하면_손패를_한_장도_바꾸지_않는다()
        {
            var round = Drawn();
            round.Exchange(new[] { 0 });
            var before = round.Hand.ToArray();

            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 0 }));
            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 1, 0 }));

            CollectionAssert.AreEqual(before, round.Hand);
            Assert.IsFalse(round.IsLocked(1), "실패한 교체가 1번 자리를 잠갔다");
        }

        [Test]
        public void 잘못된_교체_요청은_예외를_던진다()
        {
            Assert.Throws<InvalidOperationException>(() => new RoundContext(1).Exchange(new int[0]), "드로우 전");

            var round = Drawn();
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Exchange(new[] { -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Exchange(new[] { 5 }));
            Assert.Throws<ArgumentException>(() => round.Exchange(new[] { 2, 2 }), "같은 자리를 두 번");
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
            Assert.Throws<InvalidOperationException>(() => round.Evaluate(), "교체를 끝내지 않으면 판정할 수 없다");

            round.FinishExchange();
            Assert.AreEqual(RoundPhase.Evaluate, round.Phase);

            round.Evaluate();
            Assert.AreEqual(RoundPhase.Place, round.Phase);

            // 한 장도 바꾸지 않고 바로 확정할 수도 있음
            var untouched = Drawn();
            var before = untouched.Hand.ToArray();
            untouched.FinishExchange();
            untouched.Evaluate();
            CollectionAssert.AreEqual(before, untouched.Hand);
        }
    }
}
