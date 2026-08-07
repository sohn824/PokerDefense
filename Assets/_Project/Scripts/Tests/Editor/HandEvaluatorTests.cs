using System;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    public class HandEvaluatorTests
    {
        // 13개 카테고리 각각의 대표 핸드.
        [TestCase("As Ks Qs Js Ts", HandCategory.RoyalStraightFlush)]
        [TestCase("As 2s 3s 4s 5s", HandCategory.BackStraightFlush)]
        [TestCase("9s Ts Js Qs Ks", HandCategory.StraightFlush)]
        [TestCase("As Ah Ad Ac 5h", HandCategory.FourOfAKind)]
        [TestCase("As Ah Ad 5c 5h", HandCategory.FullHouse)]
        [TestCase("2h 5h 9h Jh Kh", HandCategory.Flush)]
        [TestCase("Ah Kd Qs Jc Th", HandCategory.Mountain)]
        [TestCase("Ah 2d 3s 4c 5h", HandCategory.BackStraight)]
        [TestCase("9h Td Js Qc Kh", HandCategory.Straight)]
        [TestCase("As Ah Ad 5c 9h", HandCategory.ThreeOfAKind)]
        [TestCase("As Ah 5d 5c 9h", HandCategory.TwoPair)]
        [TestCase("As Ah 3d 5c 9h", HandCategory.OnePair)]
        [TestCase("As Kh 3d 5c 9h", HandCategory.HighCard)]
        public void 대표_핸드가_해당_카테고리로_판정된다(string notation, HandCategory expected)
        {
            Assert.AreEqual(expected, HandEvaluator.Evaluate(Hand.Of(notation)).Category);
        }

        [Test]
        public void 모든_카테고리에_대표_핸드_테스트가_존재한다()
        {
            // HandCategory에 값을 추가하면 위 TestCase 목록도 함께 늘려야 한다는 것을 강제한다.
            Assert.AreEqual(13, Enum.GetValues(typeof(HandCategory)).Length);
        }

        [Test]
        public void 마운틴은_일반_스트레이트로_판정되지_않는다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("Ah Kd Qs Jc Th"));
            Assert.AreEqual(HandCategory.Mountain, result.Category);
            Assert.AreNotEqual(HandCategory.Straight, result.Category);
        }

        [Test]
        public void 백스트레이트는_일반_스트레이트로_판정되지_않는다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("Ah 2d 3s 4c 5h"));
            Assert.AreEqual(HandCategory.BackStraight, result.Category);
            Assert.AreNotEqual(HandCategory.Straight, result.Category);
        }

        // 플러시와 스트레이트가 겹칠 때 더 구체적인 특수 카테고리가 이기는지 확인한다.
        [TestCase("As Ks Qs Js Ts", HandCategory.RoyalStraightFlush)]
        [TestCase("As 2s 3s 4s 5s", HandCategory.BackStraightFlush)]
        public void 특수_스트레이트_플러시는_플러시나_스트레이트로_새지_않는다(
            string notation, HandCategory expected)
        {
            Assert.AreEqual(expected, HandEvaluator.Evaluate(Hand.Of(notation)).Category);
        }

        [TestCase("Kh Ad 2s 3c 4h", TestName = "K-A-2-3-4 랩어라운드")]
        [TestCase("2h 3d 4s 5c 7h", TestName = "한 칸 벌어진 연속")]
        [TestCase("Ah Jd Qs Kc 9h", TestName = "A-K-Q-J 뒤가 끊긴 경우")]
        public void 랩어라운드와_비연속은_스트레이트가_아니다(string notation)
        {
            var category = HandEvaluator.Evaluate(Hand.Of(notation)).Category;
            CollectionAssert.DoesNotContain(
                new[]
                {
                    HandCategory.Straight, HandCategory.BackStraight, HandCategory.Mountain,
                    HandCategory.StraightFlush, HandCategory.BackStraightFlush,
                    HandCategory.RoyalStraightFlush,
                },
                category);
        }

        [TestCase("2h 3d 4s 5c 6h", HandCategory.Straight, TestName = "가장 낮은 일반 스트레이트")]
        [TestCase("9h Td Js Qc Kh", HandCategory.Straight, TestName = "가장 높은 일반 스트레이트")]
        public void 스트레이트_경계값(string notation, HandCategory expected)
        {
            Assert.AreEqual(expected, HandEvaluator.Evaluate(Hand.Of(notation)).Category);
        }

        [Test]
        public void 페어의_키카드는_같은_랭크_두_장이다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("As Ah 3d 5c 9h"));
            Assert.AreEqual(2, result.KeyCards.Count);
            Assert.IsTrue(result.KeyCards.All(c => c.Rank == Rank.Ace));
        }

        [Test]
        public void 투페어의_키카드는_높은_페어가_먼저_온다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("5s 5h 9d 9c 2h"));
            Assert.AreEqual(4, result.KeyCards.Count);
            Assert.AreEqual(Rank.Nine, result.KeyCards[0].Rank);
            Assert.AreEqual(Rank.Five, result.KeyCards[3].Rank);
        }

        [Test]
        public void 하이카드의_키카드는_가장_높은_한_장이다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("As Kh 3d 5c 9h"));
            Assert.AreEqual(1, result.KeyCards.Count);
            Assert.AreEqual(Rank.Ace, result.KeyCards[0].Rank);
        }

        [Test]
        public void 백스트레이트의_키카드는_A를_최하위로_정렬한다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("Ah 2d 3s 4c 5h"));
            Assert.AreEqual(Rank.Five, result.KeyCards[0].Rank);
            Assert.AreEqual(Rank.Ace, result.KeyCards[4].Rank);
        }

        [Test]
        public void 풀하우스의_키카드는_트리플이_먼저_온다()
        {
            var result = HandEvaluator.Evaluate(Hand.Of("5s 5h 5d 9c 9h"));
            Assert.AreEqual(5, result.KeyCards.Count);
            Assert.AreEqual(Rank.Five, result.KeyCards[0].Rank);
            Assert.AreEqual(Rank.Nine, result.KeyCards[4].Rank);
        }

        [TestCase("As Ah 3d 5c")]
        [TestCase("As Ah 3d 5c 9h 2c")]
        public void 다섯_장이_아니면_예외를_던진다(string notation)
        {
            Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(Hand.Of(notation)));
        }

        [Test]
        public void 널_핸드는_예외를_던진다()
        {
            Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(null));
        }
    }
}
