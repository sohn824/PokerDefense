using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * RoundReplayTests
     *
     * RoundContext의 행동 기록을 같은 시드로 다시 실행하면 손패·덱·후보가 같은 라운드가 되는지 검사하는 클래스
     * 이어하기에서 후보를 다시 뽑을 수 없게 하는 근거가 되는 동작임
     */
    public class RoundReplayTests
    {
        [Test]
        public void 무작위_행동을_다시_실행하면_같은_라운드가_된다()
        {
            for (int testSeed = 0; testSeed < 500; testSeed++)
            {
                var random = new Random(testSeed);
                var original = new RoundContext(random.Next());
                PlayRandomly(original, random);

                var replayed = RoundContext.Replay(original.Seed, original.Actions);

                AssertSameRound(original, replayed, "testSeed=" + testSeed);
                AssertSameDeckOrder(original, replayed, "testSeed=" + testSeed);
            }
        }

        [Test]
        public void 다시_실행해도_같은_후보가_나온다()
        {
            var original = new RoundContext(42);
            original.Draw();
            original.Exchange(new[] { 1, 3 });
            Assert.IsTrue(original.TryRevealCandidates(3));

            var replayed = RoundContext.Replay(original.Seed, original.Actions);

            Assert.IsTrue(replayed.IsChoosingCandidate);
            Assert.IsTrue(replayed.AssistUsed);
            CollectionAssert.AreEqual(original.Candidates, replayed.Candidates);
        }

        [Test]
        public void 실패한_후보_공개는_기록되지_않는다()
        {
            var round = new RoundContext(7);
            round.Draw();

            Assert.IsFalse(round.TryRevealCandidates(2)); // 일반 교체하지 않은 자리
            Assert.AreEqual(1, round.Actions.Count);
            Assert.AreEqual(RoundActionType.Draw, round.Actions[0].Type);
        }

        [Test]
        public void 예외로_끝난_교체는_기록되지_않는다()
        {
            var round = new RoundContext(7);
            round.Draw();
            round.Exchange(new[] { 0 });

            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 0, 1 })); // 0번은 이미 잠김
            Assert.AreEqual(2, round.Actions.Count);
            CollectionAssert.AreEqual(new[] { 0 }, round.Actions[1].Indices);
        }

        [Test]
        public void 교체_기록은_넘긴_목록이_바뀌어도_그대로다()
        {
            var round = new RoundContext(7);
            round.Draw();
            int[] indices = { 0, 2 };
            round.Exchange(indices);

            indices[0] = 4;

            CollectionAssert.AreEqual(new[] { 0, 2 }, round.Actions[1].Indices);
        }

        [Test]
        public void 기록과_맞지_않는_후보_공개는_예외를_던진다()
        {
            var actions = new List<RoundAction>
            {
                RoundAction.Of(RoundActionType.Draw),
                RoundAction.RevealCandidates(2), // 일반 교체하지 않은 자리
            };

            Assert.Throws<InvalidOperationException>(() => RoundContext.Replay(7, actions));
        }

        [Test]
        public void 손패를_강제로_바꾸면_다시_실행할_수_없는_라운드가_된다()
        {
            var round = new RoundContext(7);
            round.Draw();
            Assert.IsTrue(round.IsReplayable);

            round.ForceHand(Hand.Of("As Ks Qs Js Ts"));

            Assert.IsFalse(round.IsReplayable);
        }

        // 가능한 행동 중 하나를 무작위로 골라 실행하기를 반복하다 무작위 시점에 멈춤
        static void PlayRandomly(RoundContext round, Random random)
        {
            round.Draw();

            for (int step = 0; step < 12; step++)
            {
                if (round.Phase != RoundPhase.Exchange)
                {
                    return;
                }

                if (round.IsChoosingCandidate)
                {
                    round.ChooseCandidate(random.Next(round.Candidates.Count));
                    continue;
                }

                switch (random.Next(5))
                {
                    case 0:
                        return;
                    case 1:
                    case 2:
                        List<int> unlocked = Enumerable.Range(0, RoundContext.HandSize).Where(i => round.IsLocked(i) == false).ToList();
                        round.Exchange(unlocked.Where(_ => random.Next(2) == 0).ToList());
                        break;
                    case 3:
                        List<int> targets = Enumerable.Range(0, RoundContext.HandSize).Where(round.CanAssist).ToList();
                        if (targets.Count > 0)
                        {
                            round.TryRevealCandidates(targets[random.Next(targets.Count)]);
                        }
                        break;
                    default:
                        round.FinishExchange();
                        round.Evaluate();
                        break;
                }
            }
        }

        static void AssertSameRound(RoundContext expected, RoundContext actual, string message)
        {
            Assert.AreEqual(expected.Phase, actual.Phase, message);
            CollectionAssert.AreEqual(expected.Hand, actual.Hand, message);
            Assert.AreEqual(expected.AssistUsed, actual.AssistUsed, message);
            Assert.AreEqual(expected.IsChoosingCandidate, actual.IsChoosingCandidate, message);
            CollectionAssert.AreEqual(expected.Candidates, actual.Candidates, message);
            Assert.AreEqual(expected.UsedExchanges, actual.UsedExchanges, message);
            Assert.AreEqual(expected.Actions.Count, actual.Actions.Count, message);

            for (int i = 0; i < RoundContext.HandSize; i++)
            {
                Assert.AreEqual(expected.IsLocked(i), actual.IsLocked(i), message + " locked " + i);
                Assert.AreEqual(expected.CanAssist(i), actual.CanAssist(i), message + " assist " + i);
            }

            if (expected.Phase == RoundPhase.Place)
            {
                Assert.AreEqual(expected.Result.Category, actual.Result.Category, message);
            }
        }

        // 두 라운드에 같은 행동을 더 해서 이후에 뽑히는 카드까지 같은지 확인함 (남은 덱 순서 비교)
        static void AssertSameDeckOrder(RoundContext expected, RoundContext actual, string message)
        {
            if (expected.Phase != RoundPhase.Exchange)
            {
                return;
            }

            if (expected.IsChoosingCandidate)
            {
                expected.ChooseCandidate(0);
                actual.ChooseCandidate(0);
            }

            List<int> unlocked = Enumerable.Range(0, RoundContext.HandSize).Where(i => expected.IsLocked(i) == false).ToList();
            expected.Exchange(unlocked);
            actual.Exchange(unlocked);

            CollectionAssert.AreEqual(expected.Hand, actual.Hand, message + " after extra exchange");
        }
    }
}
