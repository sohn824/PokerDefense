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
     * 15웨이브 스테이지의 규칙 (DESIGN §5.4, §5.2.1, §9.6)
     * 라이프 피해 상한 / Joker 획득·사용 / 런 통계
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

        StageDefinition MakeStage(int startingLife, int maxLifeDamage, params WaveDefinition[] waves)
        {
            var stage = Make<StageDefinition>();
            var so = new UnityEditor.SerializedObject(stage);
            so.FindProperty("startingLife").intValue = startingLife;
            so.FindProperty("maxLifeDamagePerWave").intValue = maxLifeDamage;

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

        // ---------- 라이프 피해 상한 (DESIGN §5.4) ----------

        [TestCase(3, 3)]
        [TestCase(5, 5)]
        [TestCase(12, 5)]
        public void 한_웨이브가_깎는_라이프에는_상한이_있다(int unresolved, int expectedDamage)
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, unresolved, 0);

            Assert.AreEqual(15 - expectedDamage, stage.Life);
        }

        [Test]
        public void 상한은_Swarm_웨이브를_놓쳐도_한_번에_끝나지_않게_한다()
        {
            // 적 20기짜리 웨이브를 통째로 놓쳐도 라이프 15가 즉시 0이 되면 안 된다
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 0), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, 20, 0);

            Assert.AreEqual(10, stage.Life);
            Assert.IsFalse(stage.IsGameOver);
        }

        [Test]
        public void 보스가_남으면_한_기여도_상한만큼_깎인다()
        {
            // 보스 웨이브를 실패하고 라이프 1만 잃으면 보스가 벽으로 기능하지 못한다
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 1), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, 1, 1);

            Assert.AreEqual(10, stage.Life);
        }

        [Test]
        public void 보스가_남아도_상한을_넘지는_않는다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 1), MakeWave(2, 0)));

            stage.ApplyResult(CombatOutcome.TimedOut, 9, 1);

            Assert.AreEqual(10, stage.Life);
        }

        [Test]
        public void 클리어하면_상한과_무관하게_라이프가_그대로다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 0)));

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.AreEqual(15, stage.Life);
        }

        // ---------- Joker (DESIGN §5.2.1) ----------

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
        public void Joker는_한_번에_하나씩_줄어든다()
        {
            var stage = new StageContext(MakeStage(15, 5, MakeWave(1, 2), MakeWave(2, 0)));
            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.IsTrue(stage.TryUseJoker());
            Assert.AreEqual(1, stage.Jokers);

            Assert.IsTrue(stage.TryUseJoker());
            Assert.AreEqual(0, stage.Jokers);

            Assert.IsFalse(stage.TryUseJoker());
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

        // ---------- 런 통계 (DESIGN §9.6) ----------

        [Test]
        public void 최고_족보는_enum_순서가_아니라_희귀도로_정해진다()
        {
            var stats = new RunStats();

            stats.RecordHand(HandCategory.Straight);
            stats.RecordHand(HandCategory.BackStraight);

            // BackStraight는 enum에서 Straight보다 뒤에 있지만, 그것과 무관하게 더 희귀하다
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

        [Test]
        public void 사용한_Chip이_쌓인다()
        {
            var stats = new RunStats();

            stats.RecordChipSpent(6);
            stats.RecordChipSpent(6);

            Assert.AreEqual(12, stats.ChipSpent);
        }
    }
}
