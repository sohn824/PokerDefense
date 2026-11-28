using System;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * HandEvaluatorTests
     *
     * HandEvaluator가 13개 족보를 바르게 판정하고, 족보를 만든 카드(KeyCards)를 정해진 순서로 고르는지 검사하는 클래스
     */
    public class HandEvaluatorTests
    {
        // 13개 족보 각각의 대표 핸드
        static readonly (string notation, HandCategory expected)[] Representatives =
        {
            ("As Ks Qs Js Ts", HandCategory.RoyalStraightFlush),
            ("As 2s 3s 4s 5s", HandCategory.BackStraightFlush),
            ("9s Ts Js Qs Ks", HandCategory.StraightFlush),
            ("As Ah Ad Ac 5h", HandCategory.FourOfAKind),
            ("As Ah Ad 5c 5h", HandCategory.FullHouse),
            ("2h 5h 9h Jh Kh", HandCategory.Flush),
            ("Ah Kd Qs Jc Th", HandCategory.Mountain),
            ("Ah 2d 3s 4c 5h", HandCategory.BackStraight),
            ("9h Td Js Qc Kh", HandCategory.Straight),
            ("As Ah Ad 5c 9h", HandCategory.ThreeOfAKind),
            ("As Ah 5d 5c 9h", HandCategory.TwoPair),
            ("As Ah 3d 5c 9h", HandCategory.OnePair),
            ("As Kh 3d 5c 9h", HandCategory.HighCard),
        };

        static HandCategory CategoryOf(string notation) => HandEvaluator.Evaluate(Hand.Of(notation)).Category;

        [Test]
        public void 대표_핸드가_해당_족보로_판정된다()
        {
            // HandCategory에 값을 추가하면 위 표도 함께 늘리도록 강제
            Assert.AreEqual(Enum.GetValues(typeof(HandCategory)).Length, Representatives.Length);

            foreach (var (notation, expected) in Representatives)
            {
                Assert.AreEqual(expected, CategoryOf(notation), notation);
            }
        }

        [Test]
        public void 스트레이트는_끊김_없이_이어진_다섯_장만_인정한다()
        {
            Assert.AreEqual(HandCategory.Straight, CategoryOf("2h 3d 4s 5c 6h"), "가장 낮은 일반 스트레이트");
            Assert.AreEqual(HandCategory.Straight, CategoryOf("9h Td Js Qc Kh"), "가장 높은 일반 스트레이트");

            var straights = new[]
            {
                HandCategory.Straight, HandCategory.BackStraight, HandCategory.Mountain,
                HandCategory.StraightFlush, HandCategory.BackStraightFlush, HandCategory.RoyalStraightFlush,
            };

            CollectionAssert.DoesNotContain(straights, CategoryOf("Kh Ad 2s 3c 4h"), "K-A-2-3-4 랩어라운드");
            CollectionAssert.DoesNotContain(straights, CategoryOf("2h 3d 4s 5c 7h"), "한 칸 벌어진 연속");
            CollectionAssert.DoesNotContain(straights, CategoryOf("Ah Jd Qs Kc 9h"), "A-K-Q-J 뒤가 끊긴 경우");
        }

        [Test]
        public void 키카드는_족보를_만든_카드를_정해진_순서로_고른다()
        {
            var pair = HandEvaluator.Evaluate(Hand.Of("As Ah 3d 5c 9h"));
            Assert.AreEqual(2, pair.KeyCards.Count);
            Assert.IsTrue(pair.KeyCards.All(c => c.Rank == Rank.Ace), "페어는 같은 랭크 두 장");

            var twoPair = HandEvaluator.Evaluate(Hand.Of("5s 5h 9d 9c 2h"));
            Assert.AreEqual(4, twoPair.KeyCards.Count);
            Assert.AreEqual(Rank.Nine, twoPair.KeyCards[0].Rank, "투페어는 높은 페어가 먼저");
            Assert.AreEqual(Rank.Five, twoPair.KeyCards[3].Rank);

            var backStraight = HandEvaluator.Evaluate(Hand.Of("Ah 2d 3s 4c 5h"));
            Assert.AreEqual(Rank.Five, backStraight.KeyCards[0].Rank);
            Assert.AreEqual(Rank.Ace, backStraight.KeyCards[4].Rank, "백스트레이트는 A가 맨 뒤");
        }

        [Test]
        public void 다섯_장이_아니면_예외를_던진다()
        {
            Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(Hand.Of("As Ah 3d 5c")));
            Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(Hand.Of("As Ah 3d 5c 9h 2c")));
        }
    }
}
