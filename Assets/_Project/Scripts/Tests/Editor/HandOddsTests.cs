using System.Linq;
using NUnit.Framework;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    public class HandOddsTests
    {
        [Test]
        public void 한_자리를_바꿀_때_카테고리별_경우의_수를_정확히_센다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h"); // 원페어, 3d 자리를 바꾼다
            var unseen = Hand.Of("Ad Ac 2h 3h"); // 트리플 2장 + 원페어 유지 2장

            var odds = HandOdds.Find(hand, unseen, new[] { 2 });

            Assert.AreEqual(2, odds.Count);
            Assert.AreEqual(2, odds.Single(o => o.Category == HandCategory.ThreeOfAKind).Count);
            Assert.AreEqual(2, odds.Single(o => o.Category == HandCategory.OnePair).Count);
            Assert.IsTrue(odds.All(o => o.Total == 4));
        }

        [Test]
        public void 확률은_경우의_수를_전체로_나눈_값이다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h");
            var unseen = Hand.Of("Ad Ac 2h 3h");

            var odds = HandOdds.Find(hand, unseen, new[] { 2 });

            Assert.AreEqual(0.5f, odds.Single(o => o.Category == HandCategory.ThreeOfAKind).Probability);
        }

        [Test]
        public void 두_자리를_바꾸면_전체_조합_수는_nCk이다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h");
            var unseen = Hand.Of("Ad Ac 2h 3h"); // 4장 중 2장 조합 = 6가지

            var odds = HandOdds.Find(hand, unseen, new[] { 2, 3 });

            Assert.IsTrue(odds.All(o => o.Total == 6));
            Assert.AreEqual(6, odds.Sum(o => o.Count));
        }

        [Test]
        public void 상한을_넘는_자리_수는_계산하지_않는다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h");
            var unseen = Deck.BuildCards();

            Assert.IsNotNull(HandOdds.Find(hand, unseen, new[] { 0, 1, 2, 3 })); // MaxSlots(4)와 같음 - 계산함
            Assert.IsNull(HandOdds.Find(hand, unseen, new[] { 0, 1, 2, 3, 4 })); // MaxSlots(4)보다 한 자리 많음
        }

        [Test]
        public void 남은_덱보다_많은_자리를_바꿀_수는_없다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h");
            var unseen = Hand.Of("Ad Ac");

            var odds = HandOdds.Find(hand, unseen, new[] { 2, 3, 4 });

            Assert.IsNull(odds);
        }

        [Test]
        public void 희귀한_족보부터_정렬한다()
        {
            var hand = Hand.Of("As Ah 3d 5c 9h");
            var unseen = Hand.Of("Ad Ac 2h 3h");

            var odds = HandOdds.Find(hand, unseen, new[] { 2 });

            Assert.AreEqual(HandCategory.ThreeOfAKind, odds[0].Category);
            Assert.AreEqual(HandCategory.OnePair, odds[1].Category);
        }
    }
}
