using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.Tests
{
    public class AssistTests
    {
        StageDefinition definition;
        WaveDefinition wave;
        StageContext stage;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<StageDefinition>();
            wave = ScriptableObject.CreateInstance<WaveDefinition>();
            SerializedObject so = new SerializedObject(definition);
            SerializedProperty waves = so.FindProperty("waves");
            waves.arraySize = 50;
            for (int i = 0; i < 50; i++)
            {
                waves.GetArrayElementAtIndex(i).objectReferenceValue = wave;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            stage = new StageContext(definition);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(wave);
        }

        [Test]
        public void 일반_교체한_자리만_승부_교체할_수_있다()
        {
            var held = new Card(Rank.Ace, Suit.Heart);
            var round = new RoundContext(12, new[] { held });
            round.Draw();
            round.PlaceHeldCard(0, held);
            Assert.IsFalse(round.TryRevealCandidates(0, stage));
            Assert.IsFalse(round.TryRevealCandidates(1, stage));
            Assert.AreEqual(1, stage.AssistCharges);
            round.Exchange(new[] { 1 });
            Assert.IsTrue(round.TryRevealCandidates(1, stage));
            Assert.AreEqual(0, stage.AssistCharges);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void 공개는_한번만_소비하고_선택은_한장만_교체한다(int count)
        {
            var round = new RoundContext(17);
            round.Draw();
            round.Exchange(new[] { 2 });
            Card[] before = round.Hand.ToArray();
            Assert.IsTrue(round.TryRevealCandidates(2, stage, count));
            Card chosen = round.Candidates[0];
            Assert.AreEqual(count, round.Candidates.Count);
            Assert.IsFalse(round.TryRevealCandidates(2, stage, count));
            Assert.Throws<InvalidOperationException>(() => round.FinishExchange());
            Assert.Throws<InvalidOperationException>(() => round.Exchange(new[] { 3 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.ChooseCandidate(count));
            HandResult preview = round.PreviewCandidate(0);
            round.ChooseCandidate(0);
            Assert.AreEqual(chosen, round.Hand[2]);
            Assert.AreEqual(2, round.UsedExchanges);
            for (int i = 0; i < 5; i++)
            {
                if (i != 2) Assert.AreEqual(before[i], round.Hand[i]);
            }
            Assert.Throws<InvalidOperationException>(() => round.ChooseCandidate(0));
            round.FinishExchange();
            Assert.AreEqual(preview.Category, round.Evaluate().Category);
        }

        [Test]
        public void 후보는_이전에_나온_카드나_보유_카드와_중복되지_않는다()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var held = new Card(Rank.Ace, Suit.Heart);
                var round = new RoundContext(seed, new[] { held });
                var context = new StageContext(definition);
                round.Draw();
                var seen = new HashSet<Card>(round.Hand) { held };
                round.Exchange(new[] { 0 });
                seen.Add(round.Hand[0]);
                Assert.IsTrue(round.TryRevealCandidates(0, context));
                foreach (Card candidate in round.Candidates)
                {
                    Assert.IsTrue(seen.Add(candidate));
                }
                round.ChooseCandidate(0);
                round.Exchange(new[] { 1, 2, 3, 4 });
                for (int i = 1; i < 5; i++)
                {
                    Assert.IsTrue(seen.Add(round.Hand[i]));
                }
            }
        }

        [Test]
        public void 웨이브_진입_보충은_중복되지_않고_상한을_지킨다()
        {
            Assert.IsTrue(stage.TryUseAssist());
            for (int i = 0; i < 5; i++)
            {
                stage.ApplyResult(CombatOutcome.TimedOut, 0, 0);
            }
            stage.EnterWave();
            stage.EnterWave();
            Assert.AreEqual(1, stage.AssistCharges);
            for (int i = 0; i < 15; i++) { stage.ApplyResult(CombatOutcome.Cleared, 0, 0); stage.EnterWave(); }
            Assert.AreEqual(2, stage.AssistCharges);
        }

        [Test]
        public void 후보가_부족하면_실제_남은_수만_꺼낸다()
        {
            var deck = new Deck(1);
            deck.DrawUpTo(50);
            Assert.AreEqual(2, deck.DrawUpTo(3).Length);
            Assert.AreEqual(0, deck.DrawUpTo(3).Length);
            Assert.Throws<ArgumentOutOfRangeException>(() => deck.DrawUpTo(-1));
        }
    }
}
