using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    public class AssistTests
    {
        [Test]
        public void 일반_교체한_자리만_선택_교체할_수_있다()
        {
            var round = new RoundContext(12);
            round.Draw();
            Assert.IsFalse(round.TryRevealCandidates(0));
            round.Exchange(new[] { 1 });
            Assert.IsFalse(round.TryRevealCandidates(0));
            Assert.IsTrue(round.TryRevealCandidates(1));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void 손패당_한번만_공개하고_한장만_바꾼다(int count)
        {
            var round = new RoundContext(17);
            round.Draw();
            round.Exchange(new[] { 2 });
            Card[] before = round.Hand.ToArray();
            Assert.IsTrue(round.TryRevealCandidates(2, count));
            Assert.IsTrue(round.AssistUsed);
            Card chosen = round.Candidates[0];
            Assert.AreEqual(count, round.Candidates.Count);
            Assert.IsFalse(round.TryRevealCandidates(2, count));
            Assert.Throws<InvalidOperationException>(() => round.FinishExchange());
            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 3 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.ChooseCandidate(count));
            HandResult preview = round.PreviewCandidate(0);
            CollectionAssert.AreEqual(before, round.Hand);
            round.ChooseCandidate(0);
            Assert.AreEqual(chosen, round.Hand[2]);
            Assert.IsFalse(round.TryRevealCandidates(2));
            for (int i = 0; i < 5; i++) if (i != 2) Assert.AreEqual(before[i], round.Hand[i]);
            round.FinishExchange();
            Assert.AreEqual(preview.Category, round.Evaluate().Category);
        }

        [Test]
        public void 매_손패_기회는_독립적이고_누적되지_않는다()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var round = new RoundContext(seed);
                round.Draw();
                Assert.IsFalse(round.AssistUsed);
                round.Exchange(new[] { 0 });
                Assert.IsTrue(round.TryRevealCandidates(0));
                round.ChooseCandidate(0);
                Assert.IsFalse(round.TryRevealCandidates(0));
            }
        }

        [Test]
        public void 후보는_이미_나온_카드와_중복되지_않는다()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var round = new RoundContext(seed);
                round.Draw();
                var seen = new HashSet<Card>(round.Hand);
                round.Exchange(new[] { 0 });
                seen.Add(round.Hand[0]);
                Assert.IsTrue(round.TryRevealCandidates(0));
                foreach (Card card in round.Candidates) Assert.IsTrue(seen.Add(card));
                round.ChooseCandidate(0);
                round.Exchange(new[] { 1, 2, 3, 4 });
                for (int i = 1; i < 5; i++) Assert.IsTrue(seen.Add(round.Hand[i]));
            }
        }
    }
}
