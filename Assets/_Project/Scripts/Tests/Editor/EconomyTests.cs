using System;
using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * EconomyTests
     *
     * Chip 경제 (DESIGN §9) — 유지 보너스 / 지갑 / 판매 가격 / 지원 소환 추첨
     */
    public class EconomyTests
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

        UnitDefinition MakeUnit(string id)
        {
            var unit = Make<UnitDefinition>();
            var so = new UnityEditor.SerializedObject(unit);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        /// 기본값(유지 보너스 4, 소환 6 Chip, 라운드당 2회, 판매 1/2/4)에 지원 풀만 채운다
        EconomyDefinition MakeEconomy(params (HandCategory category, int weight)[] pool)
        {
            var economy = Make<EconomyDefinition>();
            var so = new UnityEditor.SerializedObject(economy);

            var entries = so.FindProperty("supportPool");
            entries.arraySize = pool.Length;

            for (int i = 0; i < pool.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("category").enumValueIndex = (int)pool[i].category;
                entry.FindPropertyRelative("weight").intValue = pool[i].weight;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return economy;
        }

        HandUnitTable MakeTable(params (HandCategory category, UnitDefinition unit)[] rows)
        {
            var table = Make<HandUnitTable>();
            var so = new UnityEditor.SerializedObject(table);

            var entries = so.FindProperty("entries");
            entries.arraySize = rows.Length;

            for (int i = 0; i < rows.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("category").enumValueIndex = (int)rows[i].category;
                entry.FindPropertyRelative("unit").objectReferenceValue = rows[i].unit;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        StageDefinition MakeStage(int life, int chip)
        {
            var stage = Make<StageDefinition>();
            var so = new UnityEditor.SerializedObject(stage);
            so.FindProperty("startingLife").intValue = life;
            so.FindProperty("startingChip").intValue = chip;
            so.FindProperty("waves").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        // ---------- 유지 보너스 ----------

        [TestCase(0, 4)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        [TestCase(3, 1)]
        [TestCase(4, 0)]
        [TestCase(5, 0)]
        public void 유지_보너스는_교체를_덜_쓸수록_커진다(int used, int expected)
        {
            var economy = MakeEconomy((HandCategory.HighCard, 1));

            Assert.AreEqual(expected, economy.HoldBonusFor(used));
        }

        [Test]
        public void 유지_보너스는_음수_교체를_거부한다()
        {
            var economy = MakeEconomy((HandCategory.HighCard, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => economy.HoldBonusFor(-1));
        }

        [Test]
        public void 사용한_교체_장수는_잠긴_자리_수와_같다()
        {
            var round = new RoundContext(1);
            round.Draw();
            Assert.AreEqual(0, round.UsedExchanges);

            round.Exchange(new[] { 0, 3 });
            Assert.AreEqual(2, round.UsedExchanges);

            round.Exchange(new[] { 1 });
            Assert.AreEqual(3, round.UsedExchanges);
        }

        // ---------- 지갑 ----------

        [Test]
        public void 시작_Chip은_스테이지_데이터를_따른다()
        {
            var stage = new StageContext(MakeStage(life: 15, chip: 7));

            Assert.AreEqual(7, stage.Chip);
        }

        [Test]
        public void Chip이_모자라면_소비가_거부되고_잔액이_그대로다()
        {
            var stage = new StageContext(MakeStage(15, 5));

            Assert.IsFalse(stage.TrySpendChip(6));
            Assert.AreEqual(5, stage.Chip);
        }

        [Test]
        public void 잔액과_같은_금액은_소비할_수_있다()
        {
            var stage = new StageContext(MakeStage(15, 6));

            Assert.IsTrue(stage.TrySpendChip(6));
            Assert.AreEqual(0, stage.Chip);
        }

        [Test]
        public void 음수_Chip은_받지도_쓰지도_못한다()
        {
            var stage = new StageContext(MakeStage(15, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => stage.AddChip(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => stage.TrySpendChip(-1));
        }

        // ---------- 판매 ----------

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        public void 판매가는_성급으로만_정해진다(int star, int expected)
        {
            var economy = MakeEconomy((HandCategory.HighCard, 1));

            Assert.AreEqual(expected, economy.SellPriceFor(star));
        }

        // ---------- 지원 소환 ----------

        [Test]
        public void 지원_소환은_풀에_있는_유닛만_뽑는다()
        {
            var high = MakeUnit("high");
            var pair = MakeUnit("pair");
            var straight = MakeUnit("straight");

            var table = MakeTable(
                (HandCategory.HighCard, high),
                (HandCategory.OnePair, pair),
                (HandCategory.Straight, straight));

            var economy = MakeEconomy((HandCategory.HighCard, 40), (HandCategory.OnePair, 30));
            var summon = new SupportSummon(economy, table, seed: 1);

            for (int i = 0; i < 200; i++)
            {
                UnitDefinition drawn = summon.Draw();
                Assert.IsTrue(drawn == high || drawn == pair, $"풀에 없는 유닛이 나왔다: {drawn.Id}");
            }
        }

        [Test]
        public void 지원_소환은_가중치를_따른다()
        {
            var high = MakeUnit("high");
            var pair = MakeUnit("pair");
            var table = MakeTable((HandCategory.HighCard, high), (HandCategory.OnePair, pair));

            // 9 : 1 이면 하이카드가 압도적으로 많이 나와야 한다
            var economy = MakeEconomy((HandCategory.HighCard, 90), (HandCategory.OnePair, 10));
            var summon = new SupportSummon(economy, table, seed: 42);

            int highCount = 0;

            for (int i = 0; i < 1000; i++)
            {
                if (summon.Draw() == high)
                {
                    highCount++;
                }
            }

            Assert.Greater(highCount, 800, "가중치가 반영되지 않았다");
            Assert.Less(highCount, 990, "반대쪽이 아예 안 나온다");
        }

        [Test]
        public void 같은_시드는_같은_소환_결과를_만든다()
        {
            var high = MakeUnit("high");
            var pair = MakeUnit("pair");
            var table = MakeTable((HandCategory.HighCard, high), (HandCategory.OnePair, pair));
            var economy = MakeEconomy((HandCategory.HighCard, 50), (HandCategory.OnePair, 50));

            var a = new SupportSummon(economy, table, seed: 7);
            var b = new SupportSummon(economy, table, seed: 7);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreSame(a.Draw(), b.Draw());
            }
        }

        [Test]
        public void 가중치가_0이면_뽑히지_않는다()
        {
            var high = MakeUnit("high");
            var pair = MakeUnit("pair");
            var table = MakeTable((HandCategory.HighCard, high), (HandCategory.OnePair, pair));
            var economy = MakeEconomy((HandCategory.HighCard, 10), (HandCategory.OnePair, 0));

            var summon = new SupportSummon(economy, table, seed: 3);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreSame(high, summon.Draw());
            }
        }

        [Test]
        public void 빈_풀은_생성_시점에_예외를_던진다()
        {
            var table = MakeTable((HandCategory.HighCard, MakeUnit("high")));
            var economy = MakeEconomy();

            Assert.Throws<InvalidOperationException>(() => new SupportSummon(economy, table, seed: 1));
        }

        // 새 [SerializeField]는 기존 에셋에서 0으로 들어온다 (HISTORY 반복 함정). 실제 에셋에 값이 있는지 가드한다.
        [Test]
        public void 상점_에셋_수치가_비어_있지_않다()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EconomyDefinition");
            Assert.IsNotEmpty(guids, "EconomyDefinition 에셋이 없다");

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var economy = UnityEditor.AssetDatabase.LoadAssetAtPath<EconomyDefinition>(path);

                Assert.Greater(economy.ShopCardPrice, 0, $"{path}: shopCardPrice가 0");
                Assert.Greater(economy.HeldCardCapacity, 0, $"{path}: heldCardCapacity가 0");
            }
        }
    }
}
