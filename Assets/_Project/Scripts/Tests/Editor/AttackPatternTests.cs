using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.Game;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * AttackPatternTests
     *
     * M8 - 공격 패턴 5종과 적 타입
     *
     * 검증 조건인 "같은 총 DPS라도 보드 구성에 따라 클리어 여부가 갈림"을 마지막 절에서 고정한다
     */
    public class AttackPatternTests
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
                Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        UnitDefinition MakeUnit(
            AttackPattern pattern,
            float attackPower,
            float attacksPerSecond,
            float range,
            int multiTargets = 2,
            float multiSpread = 100f,
            float splashRadius = 1f,
            float pierceLength = 2f)
        {
            var unit = Make<UnitDefinition>();
            var so = new SerializedObject(unit);
            so.FindProperty("id").stringValue = "test_unit";
            so.FindProperty("displayName").stringValue = "Test Unit";
            so.FindProperty("attackPower").floatValue = attackPower;
            so.FindProperty("attacksPerSecond").floatValue = attacksPerSecond;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("attackPattern").enumValueIndex = (int)pattern;
            so.FindProperty("multiTargets").intValue = multiTargets;
            so.FindProperty("multiSpread").floatValue = multiSpread;
            so.FindProperty("splashRadius").floatValue = splashRadius;
            so.FindProperty("pierceLength").floatValue = pierceLength;

            var multipliers = so.FindProperty("starMultipliers");
            multipliers.arraySize = 3;
            multipliers.GetArrayElementAtIndex(0).floatValue = 1f;
            multipliers.GetArrayElementAtIndex(1).floatValue = 2f;
            multipliers.GetArrayElementAtIndex(2).floatValue = 4f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        EnemyDefinition MakeEnemy(float maxHp, float moveSpeed)
        {
            var enemy = Make<EnemyDefinition>();
            var so = new SerializedObject(enemy);
            so.FindProperty("id").stringValue = "test_enemy";
            so.FindProperty("displayName").stringValue = "Test Enemy";
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("moveSpeed").floatValue = moveSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        /// interval 0이면 전부 같은 시각에 같은 지점으로 나온다 - 뭉친 무리를 만들 때 쓴다
        WaveDefinition MakeWave(EnemyDefinition enemy, int count, float interval, float timeLimit)
        {
            var wave = Make<WaveDefinition>();
            var so = new SerializedObject(wave);
            so.FindProperty("waveNumber").intValue = 1;
            so.FindProperty("timeLimit").floatValue = timeLimit;

            var entries = so.FindProperty("entries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("enemy").objectReferenceValue = enemy;
            entry.FindPropertyRelative("count").intValue = count;
            entry.FindPropertyRelative("interval").floatValue = interval;
            entry.FindPropertyRelative("startDelay").floatValue = 0f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        static void Run(CombatContext combat, float seconds, float step = 0.05f)
        {
            // 0.05를 누적하면 seconds에 아주 살짝 못 미친다. 한 스텝 더 돌려 제한시간 판정을 확실히 지나게 한다
            int steps = Mathf.CeilToInt(seconds / step) + 1;

            for (int i = 0; i < steps && combat.Outcome == CombatOutcome.InProgress; i++)
            {
                combat.Tick(step);
            }
        }

        /// 슬롯 7(보드 중앙)에 유닛 하나만 세운 보드
        static GridBoard BoardWith(UnitDefinition unit)
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(unit));
            return board;
        }

        /// 맞은 적 수. 적을 죽지 않게 만들어 두고 한 발의 대상 수를 센다
        static int HitCount(CombatContext combat)
        {
            int hit = 0;

            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                if (combat.Enemies[i].Hp < combat.Enemies[i].Definition.MaxHp)
                {
                    hit++;
                }
            }

            return hit;
        }

        // ---------- 단일 (Rapid / Heavy) ----------

        [Test]
        public void 단일_패턴은_한_기만_때린다()
        {
            // Rapid와 Heavy는 대상 선정이 같다. 갈리는 것은 스탯 프로필뿐이다
            foreach (AttackPattern pattern in new[] { AttackPattern.Rapid, AttackPattern.Heavy })
            {
                var board = BoardWith(MakeUnit(pattern, 10f, 1f, range: 100f));
                var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 0f), 5, 0f, 100f));

                combat.Tick(0.05f);

                Assert.AreEqual(5, combat.RemainingEnemies);
                Assert.AreEqual(1, HitCount(combat), $"{pattern}가 두 기 이상 때렸다");
            }
        }

        // ---------- Multi ----------

        [Test]
        public void Multi는_사거리_안에서_최대_타겟_수까지_동시에_때린다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Multi, 10f, 1f, range: 100f, multiTargets: 3));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 0f), 5, 0f, 100f));

            combat.Tick(0.05f);

            Assert.AreEqual(3, HitCount(combat), "타겟 수만큼 동시에 맞아야 한다");
        }

        [Test]
        public void Multi는_적이_타겟_수보다_적으면_있는_만큼만_때린다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Multi, 10f, 1f, range: 100f, multiTargets: 4));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 0f), 2, 0f, 100f));

            combat.Tick(0.05f);

            Assert.AreEqual(2, HitCount(combat));
        }

        [Test]
        public void Multi는_사거리_밖은_때리지_않는다()
        {
            // 사거리를 0에 가깝게 두면 대상이 없어 아무도 맞지 않는다
            var board = BoardWith(MakeUnit(AttackPattern.Multi, 10f, 1f, range: 0.1f, multiTargets: 5));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 0f), 5, 0f, 100f));

            Run(combat, 3f);

            Assert.AreEqual(0, HitCount(combat));
        }

        [Test]
        public void Multi는_앞선_적부터_고른다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Multi, 10f, 1f, range: 100f, multiTargets: 2));

            // 1초 간격으로 3기. 먼저 나온 쪽이 앞서 있다
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 1f), 3, 1f, 100f));

            Run(combat, 2.05f);

            Assert.AreEqual(3, combat.RemainingEnemies);
            Assert.Less(combat.Enemies[0].Hp, combat.Enemies[0].Definition.MaxHp, "가장 앞선 적이 안 맞았다");
            Assert.Less(combat.Enemies[1].Hp, combat.Enemies[1].Definition.MaxHp, "두 번째로 앞선 적이 안 맞았다");
            Assert.AreEqual(1000f, combat.Enemies[2].Hp, 0.001f, "가장 뒤진 적까지 맞았다");
        }

        [Test]
        public void Multi는_사거리_안이라도_첫_타겟에서_MultiSpread보다_먼_적은_제외한다()
        {
            // 트랙 절반(11.6 = TrackLength/2)을 1초에 주파하는 속도. 1초 간격으로 스폰하면
            // 두 번째 발사 시점에 두 적이 트랙 반대편에 있어 사거리 안이라도 서로 아주 멀다
            var board = BoardWith(MakeUnit(
                AttackPattern.Multi, 10f, 1f, range: 100f, multiTargets: 2, multiSpread: 1f));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 11.6f), 2, 1f, 100f));

            Run(combat, 1.2f);

            Assert.AreEqual(1, HitCount(combat), "MultiSpread 밖의 적까지 함께 맞았다");
        }

        [Test]
        public void Multi는_MultiSpread_안이면_사거리_안의_적을_함께_때린다()
        {
            // 위 테스트와 같은 배치인데 MultiSpread만 넉넉하면 둘 다 맞아야 한다
            var board = BoardWith(MakeUnit(
                AttackPattern.Multi, 10f, 1f, range: 100f, multiTargets: 2, multiSpread: 100f));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 11.6f), 2, 1f, 100f));

            Run(combat, 1.2f);

            Assert.AreEqual(2, HitCount(combat));
        }

        // ---------- Splash ----------

        [Test]
        public void Splash는_착탄_지점_반경_안을_함께_때린다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Splash, 10f, 1f, range: 100f, splashRadius: 1f));

            // 정지한 적 4기가 같은 지점에 겹쳐 있다
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 0f), 4, 0f, 100f));

            combat.Tick(0.05f);

            Assert.AreEqual(4, HitCount(combat));
        }

        [Test]
        public void Splash는_반경_밖의_적은_때리지_않는다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Splash, 10f, 1f, range: 100f, splashRadius: 0.5f));

            // 속도 1이면 1초에 1월드유닛 벌어진다. 1초 간격 스폰이라 서로 1만큼 떨어져 있다
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 1f), 3, 1f, 100f));

            Run(combat, 2.05f);

            Assert.AreEqual(1, HitCount(combat), "반경 0.5 안에는 타겟 하나뿐이다");
        }

        [Test]
        public void Splash는_유닛_사거리_밖의_적도_휘말리게_한다()
        {
            // 착탄 지점이 기준이라 사거리는 "누구를 겨냥하는가"만 정한다
            // 사거리 3이면 보드 중앙에서 닿는 구간은 트랙 진행거리 1.6~5.2뿐이다
            var enemy = MakeEnemy(1000000f, 1f);
            var wave = MakeWave(enemy, 4, 1.5f, 100f);

            var splash = new CombatContext(
                BoardWith(MakeUnit(AttackPattern.Splash, 10f, 1f, range: 3f, splashRadius: 100f)), wave);
            var single = new CombatContext(
                BoardWith(MakeUnit(AttackPattern.Rapid, 10f, 1f, range: 3f)), wave);

            // 6초면 선두(진행거리 6.0)는 이미 사거리를 벗어났고 뒤따르는 두 기가 사거리 안이다
            Run(splash, 6f);
            Run(single, 6f);

            float splashAhead = splash.Enemies[0].Hp;
            float singleAhead = single.Enemies[0].Hp;

            Run(splash, 1f);
            Run(single, 1f);

            Assert.AreEqual(singleAhead, single.Enemies[0].Hp, 0.001f, "단일 타겟이 사거리 밖을 때렸다");
            Assert.Less(splash.Enemies[0].Hp, splashAhead, "사거리 밖이라고 폭발에서 빠졌다");
        }

        // ---------- Pierce ----------

        [Test]
        public void Pierce는_타겟_뒤로_관통한다()
        {
            var board = BoardWith(MakeUnit(AttackPattern.Pierce, 10f, 1f, range: 100f, pierceLength: 2.5f));

            // 속도 1 + 1초 간격이라 트랙 위에서 서로 1만큼 뒤진다. 2.5면 뒤의 두 기까지 닿는다
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000f, 1f), 4, 1f, 100f));

            Run(combat, 3.05f);

            Assert.AreEqual(4, combat.RemainingEnemies);
            Assert.AreEqual(3, HitCount(combat), "관통 길이 안의 적이 전부 맞아야 한다");
            Assert.AreEqual(1000f, combat.Enemies[3].Hp, 0.001f, "관통 길이 밖까지 맞았다");
        }

        [Test]
        public void Pierce는_타겟보다_앞선_적은_때리지_않는다()
        {
            // 관통 길이를 트랙 전체보다 길게 줘도 앞으로는 뻗지 않는다
            // 사거리 3이면 닿는 구간이 진행거리 1.6~5.2뿐이라 앞선 두 기는 사거리 밖에 있다
            var board = BoardWith(MakeUnit(AttackPattern.Pierce, 10f, 1f, range: 3f, pierceLength: 100f));
            var combat = new CombatContext(board, MakeWave(MakeEnemy(1000000f, 1f), 3, 3f, 100f));

            // 9초: 진행거리 9.0 / 6.0 / 3.0 - 사거리 안은 맨 뒤 한 기뿐이고 나머지는 그보다 앞서 있다
            Run(combat, 9f);

            float[] before = { combat.Enemies[0].Hp, combat.Enemies[1].Hp, combat.Enemies[2].Hp };

            Run(combat, 2f);

            Assert.Greater(combat.Enemies[0].Progress, combat.Enemies[2].Progress, "앞뒤 관계가 뒤집혔다");
            Assert.AreEqual(before[0], combat.Enemies[0].Hp, 0.001f, "관통이 앞으로 뻗었다");
            Assert.AreEqual(before[1], combat.Enemies[1].Hp, 0.001f, "관통이 앞으로 뻗었다");
            Assert.Less(combat.Enemies[2].Hp, before[2], "타겟이 안 맞았다");
        }

        // ---------- 적 타입 ----------

        [Test]
        public void 적_타입에_Runner와_Tank가_있다()
        {
            // 기존 에셋이 정수로 들고 있어 새 값은 뒤에 붙여야 한다
            Assert.AreEqual(0, (int)EnemyType.Normal);
            Assert.AreEqual(1, (int)EnemyType.Swarm);
            Assert.AreEqual(2, (int)EnemyType.Boss);
            Assert.AreEqual(3, (int)EnemyType.Runner);
            Assert.AreEqual(4, (int)EnemyType.Tank);
        }

        [Test]
        public void 적_에셋이_다섯_타입을_모두_쓴다()
        {
            var used = new HashSet<EnemyType>();

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                used.Add(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path).Type);
            }

            CollectionAssert.AreEquivalent(
                new[] { EnemyType.Normal, EnemyType.Swarm, EnemyType.Boss, EnemyType.Runner, EnemyType.Tank },
                used);
        }

        // ---------- 에셋 가드 ----------

        [Test]
        public void 유닛_에셋에_패턴_수치가_비어_있지_않다()
        {
            // 기존 에셋에 [SerializeField]를 새로 넣으면 C# 초기값이 아니라 0이 들어간다
            string[] guids = AssetDatabase.FindAssets("t:UnitDefinition");

            Assert.AreEqual(13, guids.Length, "족보 13종에 유닛이 1:1로 붙는다");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);

                Assert.GreaterOrEqual(unit.MultiTargets, 1, $"{unit.name}의 multiTargets가 비었다");
                Assert.Greater(unit.MultiSpread, 0f, $"{unit.name}의 multiSpread가 비었다");
                Assert.Greater(unit.SplashRadius, 0f, $"{unit.name}의 splashRadius가 비었다");
                Assert.Greater(unit.PierceLength, 0f, $"{unit.name}의 pierceLength가 비었다");
                Assert.Greater(unit.AttacksPerSecond, 0f, $"{unit.name}의 공격속도가 비었다");
            }
        }

        [Test]
        public void 유닛_에셋이_다섯_패턴을_모두_쓴다()
        {
            var used = new HashSet<AttackPattern>();

            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                used.Add(AssetDatabase.LoadAssetAtPath<UnitDefinition>(path).Pattern);
            }

            Assert.AreEqual(5, used.Count, "쓰이지 않는 패턴이 있다");
        }

        // ---------- M8 검증 조건 ----------

        [Test]
        public void 같은_DPS라도_뭉친_적_앞에서는_Splash가_이긴다()
        {
            // 단일 DPS는 Rapid 20 > Splash 12인데도 결과가 뒤집힌다
            var rapid = MakeUnit(AttackPattern.Rapid, 10f, 2f, range: 100f);
            var splash = MakeUnit(AttackPattern.Splash, 12f, 1f, range: 100f, splashRadius: 1.5f);
            var swarm = MakeEnemy(25f, 0f);

            var rapidRun = new CombatContext(BoardWith(rapid), MakeWave(swarm, 10, 0f, 6f));
            var splashRun = new CombatContext(BoardWith(splash), MakeWave(swarm, 10, 0f, 6f));

            Run(rapidRun, 6f);
            Run(splashRun, 6f);

            Assert.AreEqual(CombatOutcome.TimedOut, rapidRun.Outcome, "단일 화력으로 뭉친 무리를 잡았다");
            Assert.AreEqual(CombatOutcome.Cleared, splashRun.Outcome);
        }

        [Test]
        public void 같은_보드라도_보스_앞에서는_결과가_뒤집힌다()
        {
            // 위 테스트와 같은 두 유닛인데 적이 한 기뿐이면 단일 DPS가 그대로 승패가 된다
            var rapid = MakeUnit(AttackPattern.Rapid, 10f, 2f, range: 100f);
            var splash = MakeUnit(AttackPattern.Splash, 12f, 1f, range: 100f, splashRadius: 1.5f);
            var boss = MakeEnemy(300f, 0f);

            var rapidRun = new CombatContext(BoardWith(rapid), MakeWave(boss, 1, 0f, 20f));
            var splashRun = new CombatContext(BoardWith(splash), MakeWave(boss, 1, 0f, 20f));

            Run(rapidRun, 20f);
            Run(splashRun, 20f);

            Assert.AreEqual(CombatOutcome.Cleared, rapidRun.Outcome);
            Assert.AreEqual(CombatOutcome.TimedOut, splashRun.Outcome, "범위 유닛이 단일 보스를 더 빨리 녹였다");
        }

        [Test]
        public void 총_DPS가_같아도_Heavy는_잔챙이_떼에_비효율이다()
        {
            // 둘 다 20 DPS다. Heavy는 25HP 적에게 100을 꽂아 75를 버린다
            var rapid = MakeUnit(AttackPattern.Rapid, 5f, 4f, range: 100f);
            var heavy = MakeUnit(AttackPattern.Heavy, 100f, 0.2f, range: 100f);
            var swarm = MakeEnemy(25f, 0f);

            var rapidRun = new CombatContext(BoardWith(rapid), MakeWave(swarm, 10, 0f, 20f));
            var heavyRun = new CombatContext(BoardWith(heavy), MakeWave(swarm, 10, 0f, 20f));

            Run(rapidRun, 20f);
            Run(heavyRun, 20f);

            Assert.AreEqual(CombatOutcome.Cleared, rapidRun.Outcome);
            Assert.AreEqual(CombatOutcome.TimedOut, heavyRun.Outcome, "오버킬 낭비가 사라졌다");
        }

        [Test]
        public void 총_DPS가_같아도_Heavy는_단일_고HP에는_손해가_없다()
        {
            // 잔챙이에서 진 만큼 보스에서는 동률이다 - 오버킬이 없기 때문
            var rapid = MakeUnit(AttackPattern.Rapid, 5f, 4f, range: 100f);
            var heavy = MakeUnit(AttackPattern.Heavy, 100f, 0.2f, range: 100f);
            var boss = MakeEnemy(1000f, 0f);

            var rapidRun = new CombatContext(BoardWith(rapid), MakeWave(boss, 1, 0f, 100f));
            var heavyRun = new CombatContext(BoardWith(heavy), MakeWave(boss, 1, 0f, 100f));

            Run(rapidRun, 100f);
            Run(heavyRun, 100f);

            Assert.AreEqual(CombatOutcome.Cleared, rapidRun.Outcome);
            Assert.AreEqual(CombatOutcome.Cleared, heavyRun.Outcome);

            // 오히려 한 사이클 앞선다 - 첫 발에 100을 꽂는 쪽이 같은 DPS를 먼저 몰아 넣는다
            Assert.LessOrEqual(heavyRun.ElapsedTime, rapidRun.ElapsedTime);
        }
    }
}
