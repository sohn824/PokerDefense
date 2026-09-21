using NUnit.Framework;
using PokerDefense.Poker;
using PokerDefense.UI;

namespace PokerDefense.Tests
{
    public sealed class HandOddsTextTests
    {
        [TestCase(0, 16215, "0%")]
        [TestCase(1, 16215, "<0.1%")]
        [TestCase(1, 1000, "0.1%")]
        [TestCase(10, 10, "100%")]
        public void 작은_확률도_영으로_표시하지_않는다(int count, int total, string expected)
        {
            Assert.That(HandOddsText.Percent(count, total), Is.EqualTo(expected));
        }

        [Test]
        public void 요약은_반올림하기_전에_경우의_수를_합산한다()
        {
            var odds = new[]
            {
                new HandOddsEntry(HandCategory.HighCard, 4990, 10000),
                new HandOddsEntry(HandCategory.OnePair, 4998, 10000),
                new HandOddsEntry(HandCategory.TwoPair, 6, 10000),
                new HandOddsEntry(HandCategory.Straight, 6, 10000)
            };
            Assert.That(HandOddsText.Summary(odds, HandCategory.OnePair),
                Is.EqualTo("높은 족보 0.1% · 같은 족보 50%"));
        }

        [Test]
        public void 희귀도_순서로_높은_족보를_구분한다()
        {
            var odds = new[]
            {
                new HandOddsEntry(HandCategory.Straight, 2, 10),
                new HandOddsEntry(HandCategory.Flush, 3, 10),
                new HandOddsEntry(HandCategory.BackStraight, 5, 10)
            };
            Assert.That(HandOddsText.Summary(odds, HandCategory.Flush),
                Is.EqualTo("높은 족보 50% · 같은 족보 30%"));
        }
    }
}
