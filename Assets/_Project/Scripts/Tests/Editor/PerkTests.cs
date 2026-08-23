using System;
using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * PerkTests
     *
     * 딜러 특전 (DESIGN §11) — 특전 6종이 바꾸는 값 / 3칸 뽑기 / 에셋 가드
     *
     * 특전이 바꾸는 값은 전부 PerkSet 하나가 답한다. 호출부가 아니라 여기를 검사한다
     */
    public class PerkTests
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

        /// 실제 에셋과 같은 수치로 표를 만든다. 표의 값이 바뀌면 여기도 함께 바꾼다
        PerkTable MakeTable()
        {
            var table = Make<PerkTable>();
            var so = new SerializedObject(table);
            var entries = so.FindProperty("entries");

            (PerkId id, PerkCategory category, float amount, float limit)[] rows =
            {
                (PerkId.Patience, PerkCategory.Poker, 2f, 0f),
                (PerkId.Insurance, PerkCategory.Poker, 3f, 3f),
                (PerkId.Bargain, PerkCategory.Economy, 1f, 0f),
                (PerkId.Interest, PerkCategory.Economy, 10f, 2f),
                (PerkId.RookieTraining, PerkCategory.Merge, 0.2f, 0f),
                (PerkId.Veteran, PerkCategory.Merge, 0.15f, 0f),
            };

            entries.arraySize = rows.Length;

            for (int i = 0; i < rows.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").enumValueIndex = (int)rows[i].id;
                entry.FindPropertyRelative("displayName").stringValue = rows[i].id.ToString();
                entry.FindPropertyRelative("category").enumValueIndex = (int)rows[i].category;
                entry.FindPropertyRelative("amount").floatValue = rows[i].amount;
                entry.FindPropertyRelative("limit").floatValue = rows[i].limit;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        PerkSet MakeSet(params PerkId[] owned)
        {
            var set = new PerkSet(MakeTable());

            for (int i = 0; i < owned.Length; i++)
            {
                set.Add(owned[i]);
            }

            return set;
        }

        UnitDefinition MakeUnit(float attackPower, float range = 2f)
        {
            var unit = Make<UnitDefinition>();
            var so = new SerializedObject(unit);
            so.FindProperty("id").stringValue = "unit";
            so.FindProperty("displayName").stringValue = "unit";
            so.FindProperty("attackPower").floatValue = attackPower;
            so.FindProperty("attacksPerSecond").floatValue = 1f;
            so.FindProperty("range").floatValue = range;

            var multipliers = so.FindProperty("starMultipliers");
            multipliers.arraySize = 3;
            multipliers.GetArrayElementAtIndex(0).floatValue = 1f;
            multipliers.GetArrayElementAtIndex(1).floatValue = 2f;
            multipliers.GetArrayElementAtIndex(2).floatValue = 4f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        // ---------- 집합 자체 ----------

        [Test]
        public void 빈_집합은_어떤_값도_바꾸지_않는다()
        {
            var set = MakeSet();

            Assert.AreEqual(4, set.HoldBonus(4, 0));
            Assert.AreEqual(0, set.ConfirmBonusChip(HandCategory.HighCard, 5));
            Assert.AreEqual(6, set.SupportSummonCost(6));
            Assert.AreEqual(0, set.WaveEndChip(100));
            Assert.AreEqual(10f, set.AttackPowerOf(new UnitInstance(MakeUnit(10f))), 0.001f);
        }

        [Test]
        public void 같은_특전을_두_번_가질_수_없다()
        {
            var set = MakeSet(PerkId.Patience);

            Assert.Throws<InvalidOperationException>(() => set.Add(PerkId.Patience));
        }

        [Test]
        public void Empty_집합에는_특전을_넣을_수_없다()
        {
            Assert.Throws<InvalidOperationException>(() => PerkSet.Empty.Add(PerkId.Patience));
        }

        // ---------- 포커 계열 ----------

        [TestCase(0, 6)]
        [TestCase(1, 3)]
        [TestCase(4, 0)]
        public void Patience는_교체를_한_장도_안_썼을_때만_얹는다(int used, int expected)
        {
            var economy = Make<EconomyDefinition>();
            var set = MakeSet(PerkId.Patience);

            Assert.AreEqual(expected, set.HoldBonus(economy.HoldBonusFor(used), used));
        }

        [TestCase(HandCategory.HighCard, 3, 3)]
        [TestCase(HandCategory.HighCard, 5, 3)]
        [TestCase(HandCategory.HighCard, 2, 0)]
        [TestCase(HandCategory.OnePair, 5, 0)]
        public void Insurance는_많이_교체하고도_하이카드로_끝났을_때만_준다(
            HandCategory category, int used, int expected)
        {
            var set = MakeSet(PerkId.Insurance);

            Assert.AreEqual(expected, set.ConfirmBonusChip(category, used));
        }

        // ---------- 경제 계열 ----------

        [Test]
        public void Bargain은_지원_소환을_한_칩_깎는다()
        {
            Assert.AreEqual(5, MakeSet(PerkId.Bargain).SupportSummonCost(6));
        }

        [TestCase(0, 0)]
        [TestCase(9, 0)]
        [TestCase(10, 1)]
        [TestCase(25, 2)]
        [TestCase(100, 2)]
        public void Interest는_보유_Chip_10당_1이고_상한이_2다(int chip, int expected)
        {
            Assert.AreEqual(expected, MakeSet(PerkId.Interest).WaveEndChip(chip));
        }

        [Test]
        public void Interest는_시간_초과로_끝난_웨이브에서도_들어온다()
        {
            var wave = MakeWave();
            var stage = new StageContext(MakeStageDefinition(wave), MakeTable());
            stage.Perks.Add(PerkId.Interest);

            int before = stage.Chip;
            stage.ApplyResult(CombatOutcome.TimedOut, unresolvedEnemies: 1, unresolvedBosses: 0);

            Assert.AreEqual(before + 2, stage.Chip);
        }

        // ---------- 머지 계열 ----------

        [Test]
        public void RookieTraining은_별_하나만_올린다()
        {
            var set = MakeSet(PerkId.RookieTraining);
            var unit = new UnitInstance(MakeUnit(10f));

            Assert.AreEqual(12f, set.AttackPowerOf(unit), 0.001f);
            Assert.AreEqual(20f, set.AttackPowerOf(unit.Promoted()), 0.001f);
        }

        [Test]
        public void Veteran은_별_둘_이상만_올린다()
        {
            var set = MakeSet(PerkId.Veteran);
            var unit = new UnitInstance(MakeUnit(10f));

            Assert.AreEqual(10f, set.AttackPowerOf(unit), 0.001f);
            Assert.AreEqual(23f, set.AttackPowerOf(unit.Promoted()), 0.001f);
            Assert.AreEqual(46f, set.AttackPowerOf(unit.Promoted().Promoted()), 0.001f);
        }

        [Test]
        public void 공격력_특전은_전투에도_그대로_들어간다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(10f, range: 100f)));

            var wave = MakeWave(MakeEnemy(maxHp: 1000f));

            var plain = new CombatContext(board, wave);
            var boosted = new CombatContext(board, wave, MakeSet(PerkId.RookieTraining));

            // 같은 시간만큼 돌리면 특전이 붙은 쪽이 정확히 20% 더 깎아야 한다
            for (int i = 0; i < 35; i++)
            {
                plain.Tick(0.1f);
                boosted.Tick(0.1f);
            }

            float plainDamage = 1000f - plain.Enemies[0].Hp;
            float boostedDamage = 1000f - boosted.Enemies[0].Hp;

            Assert.Greater(plainDamage, 0f, "특전 없는 쪽이 아예 때리지 못했다");
            Assert.AreEqual(plainDamage * 1.2f, boostedDamage, 0.001f);
        }

        // ---------- 3칸 뽑기 ----------

        [Test]
        public void 선택지는_세_칸이고_서로_다르다()
        {
            var offer = new PerkOffer(MakeTable(), seed: 1);

            for (int seed = 0; seed < 20; seed++)
            {
                IReadOnlyList<PerkId> drawn = new PerkOffer(MakeTable(), seed).DrawPerks(MakeSet());

                Assert.AreEqual(PerkOffer.OfferCount, drawn.Count);
                CollectionAssert.AllItemsAreUnique(drawn);
            }
        }

        [Test]
        public void 선택지는_계열을_섞어_뽑는다()
        {
            PerkTable table = MakeTable();

            for (int seed = 0; seed < 20; seed++)
            {
                IReadOnlyList<PerkId> drawn = new PerkOffer(table, seed).DrawPerks(MakeSet());

                PerkCategory first = table.GetEntry(drawn[0]).category;
                PerkCategory second = table.GetEntry(drawn[1]).category;

                Assert.IsTrue(first == PerkCategory.Poker || first == PerkCategory.Economy,
                    $"첫 칸이 포커/경제가 아니다: {first}");
                Assert.IsTrue(second == PerkCategory.Unit || second == PerkCategory.Merge,
                    $"둘째 칸이 유닛/머지가 아니다: {second}");
            }
        }

        [Test]
        public void 이미_가진_특전은_다시_나오지_않는다()
        {
            var owned = MakeSet(PerkId.Veteran, PerkId.Bargain);

            for (int seed = 0; seed < 20; seed++)
            {
                IReadOnlyList<PerkId> drawn = new PerkOffer(MakeTable(), seed).DrawPerks(owned);

                CollectionAssert.DoesNotContain(drawn, PerkId.Veteran);
                CollectionAssert.DoesNotContain(drawn, PerkId.Bargain);
            }
        }

        [Test]
        public void 같은_시드는_같은_선택지를_만든다()
        {
            PerkTable table = MakeTable();

            CollectionAssert.AreEqual(
                new PerkOffer(table, 7).DrawPerks(MakeSet()),
                new PerkOffer(table, 7).DrawPerks(MakeSet()));
        }

        [Test]
        public void 남은_특전이_모자라면_그만큼만_내놓는다()
        {
            var owned = MakeSet(PerkId.Patience, PerkId.Insurance, PerkId.Bargain, PerkId.Interest);

            IReadOnlyList<PerkId> drawn = new PerkOffer(MakeTable(), 1).DrawPerks(owned);

            Assert.AreEqual(2, drawn.Count);
            CollectionAssert.AllItemsAreUnique(drawn);
        }

        // ---------- 에셋 가드 ----------

        [Test]
        public void 특전_에셋에_여섯_종이_모두_있다()
        {
            PerkTable table = LoadTable();

            foreach (PerkId id in Enum.GetValues(typeof(PerkId)))
            {
                Assert.DoesNotThrow(() => table.GetEntry(id), $"{id} 행이 없다");
            }

            Assert.AreEqual(Enum.GetValues(typeof(PerkId)).Length, table.Entries.Count);
        }

        [Test]
        public void 특전_에셋에_이름과_설명이_비어_있지_않다()
        {
            IReadOnlyList<PerkTable.PerkEntry> entries = LoadTable().Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(entries[i].displayName), $"{entries[i].id} 이름이 비었다");
                Assert.IsFalse(string.IsNullOrEmpty(entries[i].description), $"{entries[i].id} 설명이 비었다");
                Assert.Greater(entries[i].amount, 0f, $"{entries[i].id} amount가 0이다");
            }
        }

        [Test]
        public void 특전_보상은_보스_웨이브에만_있다()
        {
            // 기존 에셋에 [SerializeField]를 새로 넣으면 YAML에 키가 없어 전부 false가 된다
            var rewarded = new List<int>();

            foreach (string guid in AssetDatabase.FindAssets("t:WaveDefinition"))
            {
                var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (wave.PerkReward)
                {
                    rewarded.Add(wave.WaveNumber);
                }
            }

            rewarded.Sort();
            // 최종 보스(50)는 받아도 쓸 라운드가 없어 빠진다
            CollectionAssert.AreEqual(new[] { 5, 10, 15, 20, 30, 40 }, rewarded);
        }

        static PerkTable LoadTable()
        {
            string[] guids = AssetDatabase.FindAssets("t:PerkTable");
            Assert.AreEqual(1, guids.Length, "PerkTable 에셋은 하나여야 한다");
            return AssetDatabase.LoadAssetAtPath<PerkTable>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---------- 테스트용 에셋 ----------

        EnemyDefinition MakeEnemy(float maxHp)
        {
            var enemy = Make<EnemyDefinition>();
            var so = new SerializedObject(enemy);
            so.FindProperty("id").stringValue = "enemy";
            so.FindProperty("displayName").stringValue = "enemy";
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("moveSpeed").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        WaveDefinition MakeWave(EnemyDefinition enemy = null)
        {
            var wave = Make<WaveDefinition>();
            var so = new SerializedObject(wave);
            so.FindProperty("waveNumber").intValue = 1;
            so.FindProperty("timeLimit").floatValue = 999f;

            var entries = so.FindProperty("entries");
            entries.arraySize = enemy == null ? 0 : 1;

            if (enemy != null)
            {
                var entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("enemy").objectReferenceValue = enemy;
                entry.FindPropertyRelative("count").intValue = 1;
                entry.FindPropertyRelative("interval").floatValue = 1f;
                entry.FindPropertyRelative("startDelay").floatValue = 0f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        StageDefinition MakeStageDefinition(WaveDefinition wave)
        {
            var stage = Make<StageDefinition>();
            var so = new SerializedObject(stage);
            so.FindProperty("startingLife").intValue = 15;
            so.FindProperty("startingChip").intValue = 20;
            so.FindProperty("maxLifeDamagePerWave").intValue = 5;

            var waves = so.FindProperty("waves");
            waves.arraySize = 1;
            waves.GetArrayElementAtIndex(0).objectReferenceValue = wave;

            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }
    }
}
