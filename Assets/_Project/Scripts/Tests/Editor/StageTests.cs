using System;
using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * StageTests
     *
     * 스테이지 규칙
     * 라이프 피해 / 승급권(Joker) 획득·사용 / Run 통계
     */
    public class StageTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        WaveDefinition MakeWave(int number, int jokerReward)
        {
            var wave = Make<WaveDefinition>();
            var so = new UnityEditor.SerializedObject(wave);
            so.FindProperty("waveNumber").intValue = number;
            so.FindProperty("jokerReward").intValue = jokerReward;
            so.FindProperty("entries").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        StageDefinition MakeStage(int startingLife, int dangerLife, params WaveDefinition[] waves)
        {
            var stage = Make<StageDefinition>();
            var so = new UnityEditor.SerializedObject(stage);
            so.FindProperty("startingLife").intValue = startingLife;
            so.FindProperty("dangerLife").intValue = dangerLife;

            var list = so.FindProperty("waves");
            list.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        UnitDefinition MakeUnit(string id, float attackPower, float attacksPerSecond)
        {
            var unit = Make<UnitDefinition>();
            var so = new UnityEditor.SerializedObject(unit);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("attackPower").floatValue = attackPower;
            so.FindProperty("attacksPerSecond").floatValue = attacksPerSecond;

            var multipliers = so.FindProperty("starMultipliers");
            multipliers.arraySize = 3;
            multipliers.GetArrayElementAtIndex(0).floatValue = 1f;
            multipliers.GetArrayElementAtIndex(1).floatValue = 2f;
            multipliers.GetArrayElementAtIndex(2).floatValue = 4f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        // ---------- 라이프 피해 ----------

        [TestCase(3, 12)]
        [TestCase(12, 3)]
        [TestCase(20, 0)]
        public void 시간_초과하면_남은_적_수만큼_상한_없이_깎인다(int unresolved, int expectedLife)
        {
            var stage = new StageContext(MakeStage(15, 3, MakeWave(1, 0), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, unresolved, 0);

            Assert.AreEqual(expectedLife, stage.Life);
            Assert.AreEqual(15 - expectedLife, stage.LastLifeLost, "실제로 깎인 라이프만 기록한다");
        }

        [Test]
        public void 보스가_남으면_라이프와_관계없이_패배한다()
        {
            var stage = new StageContext(MakeStage(15, 3, MakeWave(1, 1), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, 1, 1);

            Assert.IsTrue(stage.IsGameOver);
            Assert.AreEqual(0, stage.Life);
            Assert.AreEqual(15, stage.LastLifeLost);
        }

        [Test]
        public void 클리어하면_라이프가_그대로다()
        {
            var stage = new StageContext(MakeStage(15, 3, MakeWave(1, 0)));

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.AreEqual(15, stage.Life);
            Assert.AreEqual(0, stage.LastLifeLost);
        }

        // ---------- 승급권(Joker) ----------

        [Test]
        public void 보스_웨이브를_클리어하면_Joker를_받는다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 0), MakeWave(2, 1), MakeWave(3, 0)));

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);
            Assert.AreEqual(0, stage.Jokers, "일반 웨이브는 주지 않는다");

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);
            Assert.AreEqual(1, stage.Jokers);
        }

        [Test]
        public void 보스_웨이브를_놓치면_Joker를_받지_못한다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 1), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, 3, 0);

            Assert.AreEqual(0, stage.Jokers);
        }

        [Test]
        public void Joker가_없으면_사용이_거부된다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 0)));

            Assert.IsFalse(stage.TryUseJoker());
            Assert.AreEqual(0, stage.Jokers);
        }

        [Test]
        public void Joker는_짝_없이_성급을_올린다()
        {
            var board = new GridBoard();
            board.TryPlace(4, new UnitInstance(MakeUnit("scout", 10f, 1f)));

            Assert.IsTrue(board.TryPromoteAt(4));

            Assert.AreEqual(2, board[4].Star);
            Assert.AreEqual(20f, board[4].AttackPower);
            Assert.AreEqual(1, board.OccupiedCount, "짝을 소모하지 않는다");
        }

        [Test]
        public void Joker는_최대_성급과_빈_칸에_쓸_수_없다()
        {
            var board = new GridBoard();
            var scout = MakeUnit("scout", 10f, 1f);

            board.TryPlace(0, new UnitInstance(scout));
            board.TryPromoteAt(0);
            board.TryPromoteAt(0);
            Assert.AreEqual(UnitDefinition.MaxStar, board[0].Star);

            Assert.IsFalse(board.TryPromoteAt(0), "★3에는 쓸 수 없다");
            Assert.IsFalse(board.TryPromoteAt(1), "빈 칸에는 쓸 수 없다");
        }

        // ---------- Run 통계 ----------

        [Test]
        public void 최고_족보는_enum_순서가_아니라_희귀도로_정해진다()
        {
            var stats = new RunStats();

            stats.RecordHand(HandCategory.Straight);
            stats.RecordHand(HandCategory.BackStraight);

            // BackStraight는 enum에서 Straight보다 뒤에 있지만, 그것과 무관하게 더 희귀함
            Assert.AreEqual(HandCategory.BackStraight, stats.BestHand);

            stats.RecordHand(HandCategory.OnePair);
            Assert.AreEqual(HandCategory.BackStraight, stats.BestHand, "더 흔한 족보로 덮이면 안 된다");
        }

        [Test]
        public void 희귀도_표는_모든_족보를_담는다()
        {
            foreach (HandCategory category in Enum.GetValues(typeof(HandCategory)))
            {
                Assert.DoesNotThrow(() => HandRarity.RankOf(category), $"{category}가 희귀도 표에 없다");
            }
        }

        [Test]
        public void 소환_수는_늘고_머지는_소환으로_치지_않는다()
        {
            var stats = new RunStats();
            var scout = MakeUnit("scout", 10f, 1f);
            var unit = new UnitInstance(scout);

            stats.RecordSummon(unit);
            stats.RecordSummon(new UnitInstance(scout));
            stats.RecordUnit(unit.Promoted());

            Assert.AreEqual(2, stats.Summons);
            Assert.AreEqual(2, stats.BestUnit.Star, "머지 결과가 최고 유닛이 된다");
        }

        [Test]
        public void 최고_유닛은_약한_유닛으로_덮이지_않는다()
        {
            var stats = new RunStats();
            var strong = new UnitInstance(MakeUnit("cannon", 100f, 0.5f));
            var weak = new UnitInstance(MakeUnit("scout", 10f, 1f));

            stats.RecordSummon(strong);
            stats.RecordSummon(weak);

            Assert.AreSame(strong, stats.BestUnit);
        }

        // ---------- 이어하기 복원 ----------

        [Test]
        public void 복원한_스테이지는_그_웨이브부터_규칙대로_이어진다()
        {
            var context = new StageContext(MakeStage(20, 3, MakeWave(1, 0), MakeWave(2, 0), MakeWave(3, 1)));

            context.Restore(12, 2, 2);

            Assert.AreEqual(12, context.Life);
            Assert.AreEqual(2, context.Jokers);
            Assert.AreEqual(2, context.WaveIndex);

            context.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.AreEqual(3, context.Jokers, "복원한 웨이브(보스)를 클리어하면 보상을 받는다");
            Assert.IsTrue(context.IsAllWavesCleared);
        }

        [TestCase(0, 0, 0, TestName = "라이프 0")]
        [TestCase(21, 0, 0, TestName = "시작 라이프보다 많은 라이프")]
        [TestCase(10, -1, 0, TestName = "음수 승급권")]
        [TestCase(10, 0, 3, TestName = "모든 웨이브를 넘긴 번호")]
        public void 끝났거나_범위_밖인_스테이지는_복원하지_않는다(int life, int jokers, int waveIndex)
        {
            var context = new StageContext(MakeStage(20, 3, MakeWave(1, 0), MakeWave(2, 0), MakeWave(3, 1)));

            Assert.Throws<ArgumentOutOfRangeException>(() => context.Restore(life, jokers, waveIndex));
            Assert.AreEqual(20, context.Life, "실패하면 값을 바꾸지 않는다");
        }

        [Test]
        public void 복원한_통계는_이후_기록과_이어진다()
        {
            var stats = new RunStats();
            var best = UnitInstance.Restore(MakeUnit("cannon", 100f, 0.5f), 2);

            stats.Restore(7, HandCategory.FullHouse, best);
            stats.RecordHand(HandCategory.OnePair);
            stats.RecordSummon(new UnitInstance(MakeUnit("scout", 10f, 1f)));

            Assert.AreEqual(8, stats.Summons);
            Assert.AreEqual(HandCategory.FullHouse, stats.BestHand, "더 흔한 족보로 덮이지 않는다");
            Assert.AreSame(best, stats.BestUnit);
        }

        [Test]
        public void 유닛은_저장한_성급으로_복원되고_범위_밖_성급은_거부한다()
        {
            var cannon = MakeUnit("cannon", 100f, 0.5f);

            var restored = UnitInstance.Restore(cannon, 2);

            Assert.AreEqual(2, restored.Star);
            Assert.AreEqual(3, restored.Promoted().Star);
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitInstance.Restore(cannon, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitInstance.Restore(cannon, UnitDefinition.MaxStar + 1));
        }

        [Test]
        public void 복원한_유닛을_빈_보드에_놓으면_성급이_그대로다()
        {
            var cannon = MakeUnit("cannon", 100f, 0.5f);
            var board = new GridBoard();

            Assert.AreEqual(PlacementResult.Placed, board.TryPlace(4, UnitInstance.Restore(cannon, 3)));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlace(5, UnitInstance.Restore(cannon, 3)));

            Assert.AreEqual(3, board[4].Star);
            Assert.AreEqual(3, board[5].Star, "옆 칸에 같은 ★3이 있어도 빈 칸 배치는 합치지 않는다");
        }
    }
}
