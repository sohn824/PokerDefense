using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.EditorTools;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * HandCheatTableTests
     *
     * 개발용 족보 소환(HandCheatTable)이 실제로 그 족보로 판정되는지 고정한다
     *
     * 표를 손으로 적었으므로 틀려도 조용히 다른 유닛이 소환된다.
     * 특히 특수 승격 넷은 정통 스트레이트/스트레이트 플러시로 떨어지기 쉽다
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
                    $"{HandCheatTable.NameOf(category)} 자리의 카드가 다른 족보로 판정된다");
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
                string name = HandCheatTable.NameOf(category);

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
