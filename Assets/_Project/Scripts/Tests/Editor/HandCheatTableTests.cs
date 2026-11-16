using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.EditorTools;
using PokerDefense.Poker;
using PokerDefense.UI;

namespace PokerDefense.Tests
{
    /**
     * HandCheatTableTests
     *
     * HandCheatTable의 카드가 실제로 해당 족보로 판정되는지 검사하는 클래스
     */
    public class HandCheatTableTests
    {
        [Test]
        public void 치트_카드표는_의도한_족보로_판정된다()
        {
            foreach (HandCategory category in HandCheatTable.Order)
            {
                IReadOnlyList<Card> hand = HandCheatTable.HandFor(category);

                Assert.AreEqual(category, HandEvaluator.Evaluate(hand).Category,
                    $"{HandCategoryNames.Of(category)} 자리의 카드가 다른 족보로 판정된다");
            }
        }

        [Test]
        public void 치트_카드표는_족보_전체를_담는다()
        {
            Assert.AreEqual(
                System.Enum.GetValues(typeof(HandCategory)).Length,
                HandCheatTable.Order.Length,
                "족보가 늘었는데 치트 표에 안 들어갔다");
        }

        [Test]
        public void 치트_손패는_5장이고_중복이_없다()
        {
            foreach (HandCategory category in HandCheatTable.Order)
            {
                IReadOnlyList<Card> hand = HandCheatTable.HandFor(category);
                string name = HandCategoryNames.Of(category);

                Assert.AreEqual(RoundContextHandSize, hand.Count, $"{name} 손패 장수가 다르다");

                var seen = new HashSet<Card>();

                for (int i = 0; i < hand.Count; i++)
                {
                    Assert.IsTrue(seen.Add(hand[i]), $"{name} 손패에 {hand[i]}가 두 번 있다");
                }
            }
        }

        const int RoundContextHandSize = 5;
    }
}
