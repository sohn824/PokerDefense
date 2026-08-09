using System;
using System.Collections.Generic;
using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * CombatTests
     *
     * CombatContext.Tick(deltaTime)이 deltaTime을 인자로 받기 때문에
     * 전투 한 판을 씬 없이 통째로 돌릴 수 있다. M4 검증 조건을 여기서 고정한다
     */
    public class CombatTests
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

        UnitDefinition MakeUnit(float attackPower, float attacksPerSecond, float range)
        {
            var unit = Make<UnitDefinition>();
            var so = new UnityEditor.SerializedObject(unit);
            so.FindProperty("id").stringValue = "test_unit";
            so.FindProperty("displayName").stringValue = "Test Unit";
            so.FindProperty("attackPower").floatValue = attackPower;
            so.FindProperty("attacksPerSecond").floatValue = attacksPerSecond;
            so.FindProperty("range").floatValue = range;

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
            var so = new UnityEditor.SerializedObject(enemy);
            so.FindProperty("id").stringValue = "test_enemy";
            so.FindProperty("displayName").stringValue = "Test Enemy";
            so.FindProperty("maxHp").floatValue = maxHp;
            so.FindProperty("moveSpeed").floatValue = moveSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        WaveDefinition MakeWave(EnemyDefinition enemy, int count, float interval, float timeLimit)
        {
            var wave = Make<WaveDefinition>();
            var so = new UnityEditor.SerializedObject(wave);
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

        StageDefinition MakeStage(int startingLife, params WaveDefinition[] waves)
        {
            var stage = Make<StageDefinition>();
            var so = new UnityEditor.SerializedObject(stage);
            so.FindProperty("startingLife").intValue = startingLife;

            var list = so.FindProperty("waves");
            list.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        /// 전투가 끝나거나 한계 시간에 닿을 때까지 돌린다
        static void Run(CombatContext combat, float seconds, float step = 0.1f)
        {
            int steps = Mathf.CeilToInt(seconds / step);

            for (int i = 0; i < steps && combat.Outcome == CombatOutcome.InProgress; i++)
            {
                combat.Tick(step);
            }
        }

        // ---------- 트랙 ----------

        [Test]
        public void 트랙은_보드를_감싼다()
        {
            Assert.Greater(GridBoard.TrackHalfWidth, GridBoard.Columns * 0.5f * GridBoard.CellSize);
            Assert.Greater(GridBoard.TrackHalfHeight, GridBoard.Rows * 0.5f * GridBoard.CellSize);
        }

        [Test]
        public void 트랙_한_바퀴는_제자리로_돌아온다()
        {
            Vector2 start = GridBoard.TrackPosition(0f);
            Vector2 lap = GridBoard.TrackPosition(1f);

            Assert.AreEqual(start.x, lap.x, 0.0001f);
            Assert.AreEqual(start.y, lap.y, 0.0001f);
        }

        [Test]
        public void 트랙_위_점은_전부_경계선에_있다()
        {
            for (int i = 0; i <= 100; i++)
            {
                Vector2 p = GridBoard.TrackPosition(i / 100f);
                bool onVertical = Mathf.Abs(Mathf.Abs(p.x) - GridBoard.TrackHalfWidth) < 0.0001f;
                bool onHorizontal = Mathf.Abs(Mathf.Abs(p.y) - GridBoard.TrackHalfHeight) < 0.0001f;

                Assert.IsTrue(onVertical || onHorizontal, $"진행도 {i / 100f}의 좌표 {p}가 트랙 위가 아니다");
            }
        }

        // ---------- 이동 ----------

        [Test]
        public void 적은_시간이_지나면_트랙을_따라_나아간다()
        {
            var enemy = new EnemyInstance(MakeEnemy(100f, GridBoard.TrackLength));

            enemy.Advance(0.5f);

            Assert.AreEqual(0.5f, enemy.Progress, 0.0001f, "트랙 길이만큼의 속도면 1초에 한 바퀴다");
        }

        [Test]
        public void 적은_한_바퀴를_넘어도_계속_돈다()
        {
            var enemy = new EnemyInstance(MakeEnemy(100f, GridBoard.TrackLength));

            enemy.Advance(2.5f);

            Assert.AreEqual(2.5f, enemy.Progress, 0.0001f, "진행도는 바퀴 수를 포함해 누적된다");
        }

        // ---------- 스폰 ----------

        [Test]
        public void 스폰은_데이터의_수량과_간격을_따른다()
        {
            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 3, interval: 1f, timeLimit: 100f);
            var combat = new CombatContext(new GridBoard(), wave);

            combat.Tick(0.1f);
            Assert.AreEqual(1, combat.RemainingEnemies, "0초에 첫 마리");

            Run(combat, 1f);
            Assert.AreEqual(2, combat.RemainingEnemies, "1초에 둘째");

            Run(combat, 1f);
            Assert.AreEqual(3, combat.RemainingEnemies, "2초에 셋째");

            Run(combat, 5f);
            Assert.AreEqual(3, combat.RemainingEnemies, "예정된 3마리를 넘지 않는다");
        }

        // ---------- 공격 ----------

        [Test]
        public void 사거리_밖의_적은_피해를_받지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(50f, 1f, range: 0.1f)));   // 중앙, 사거리 거의 0

            var wave = MakeWave(MakeEnemy(100f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 5f);

            Assert.AreEqual(1, combat.RemainingEnemies);
            Assert.AreEqual(100f, combat.Enemies[0].Hp, 0.001f, "사거리 밖인데 맞았다");
        }

        [Test]
        public void 사거리_안의_적은_공격속도만큼_맞는다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(10f, attacksPerSecond: 1f, range: 100f)));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            // 0초에 1발, 이후 1초마다 1발 -> 3.05초면 4발
            Run(combat, 3.05f);

            Assert.AreEqual(1000f - 40f, combat.Enemies[0].Hp, 0.001f);
        }

        [Test]
        public void HP가_0이_되면_적이_사라진다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(100f, 10f, range: 100f)));

            var wave = MakeWave(MakeEnemy(100f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            combat.Tick(0.1f);

            Assert.AreEqual(0, combat.RemainingEnemies);
        }

        [Test]
        public void 성급이_오르면_전투가_빨리_끝난다()
        {
            // ★2는 공격력·공격속도가 2배씩이라 같은 적을 훨씬 빨리 녹인다
            var unitDefinition = MakeUnit(10f, 1f, range: 100f);
            var enemyDefinition = MakeEnemy(200f, 0f);

            var star1Board = new GridBoard();
            star1Board.TryPlace(7, new UnitInstance(unitDefinition));

            var star2Board = new GridBoard();
            star2Board.TryPlace(7, new UnitInstance(unitDefinition));
            star2Board.TryPlace(7, new UnitInstance(unitDefinition));
            Assert.AreEqual(2, star2Board[7].Star);

            var star1 = new CombatContext(star1Board, MakeWave(enemyDefinition, 1, 0f, 100f));
            var star2 = new CombatContext(star2Board, MakeWave(enemyDefinition, 1, 0f, 100f));

            Run(star1, 100f);
            Run(star2, 100f);

            Assert.AreEqual(CombatOutcome.Cleared, star1.Outcome);
            Assert.AreEqual(CombatOutcome.Cleared, star2.Outcome);
            Assert.Less(star2.ElapsedTime, star1.ElapsedTime, "★2가 더 느리게 잡았다");
        }

        // ---------- M4 검증 조건 ----------

        [Test]
        public void 제한시간_안에_전멸시키면_클리어다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(100f, 5f, range: 100f)));

            var wave = MakeWave(MakeEnemy(100f, 0f), count: 3, interval: 0.5f, timeLimit: 30f);
            var combat = new CombatContext(board, wave);

            Run(combat, 30f);

            Assert.AreEqual(CombatOutcome.Cleared, combat.Outcome);
            Assert.AreEqual(0, combat.RemainingEnemies);
            Assert.Less(combat.ElapsedTime, 30f);
        }

        [Test]
        public void 전멸시켜도_스폰이_남았으면_다음_스폰까지_기다린다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(1000f, 5f, range: 100f)));

            // 0초에 1기, 10초에 1기. 첫 적을 잡아도 웨이브는 안 끝난다
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 2, interval: 10f, timeLimit: 30f);
            var combat = new CombatContext(board, wave);

            Run(combat, 5f);

            Assert.AreEqual(CombatOutcome.InProgress, combat.Outcome);
            Assert.AreEqual(0, combat.RemainingEnemies, "화면에는 적이 없다");
            Assert.AreEqual(1, combat.UnresolvedEnemies, "아직 안 나온 적이 남았다");
            Assert.AreEqual(10f - combat.ElapsedTime, combat.SecondsToNextSpawn, 0.001f);

            Run(combat, 10f);

            Assert.AreEqual(CombatOutcome.Cleared, combat.Outcome);
            Assert.Less(combat.ElapsedTime, 30f, "제한시간을 기다리지 않는다");
            Assert.AreEqual(-1f, combat.SecondsToNextSpawn, "더 나올 적이 없다");
        }

        [Test]
        public void 제한시간을_넘기면_시간_초과다()
        {
            // 유닛이 하나도 없으니 적이 죽지 않는다
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 3, interval: 0.5f, timeLimit: 5f);
            var combat = new CombatContext(new GridBoard(), wave);

            Run(combat, 10f);

            Assert.AreEqual(CombatOutcome.TimedOut, combat.Outcome);
            Assert.AreEqual(3, combat.RemainingEnemies);
        }

        [Test]
        public void 전투가_끝나면_더_이상_진행되지_않는다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 1, interval: 0f, timeLimit: 2f);
            var combat = new CombatContext(new GridBoard(), wave);

            Run(combat, 5f);
            float frozen = combat.ElapsedTime;

            combat.Tick(1f);

            Assert.AreEqual(CombatOutcome.TimedOut, combat.Outcome);
            Assert.AreEqual(frozen, combat.ElapsedTime, 0.0001f);
        }

        [Test]
        public void 시간_초과하면_잔여_적_수만큼_라이프가_깎인다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 4, interval: 0.5f, timeLimit: 5f);
            var stage = new StageContext(MakeStage(20, wave));
            var combat = new CombatContext(new GridBoard(), wave);

            Run(combat, 10f);
            stage.ApplyResult(combat.Outcome, combat.UnresolvedEnemies, combat.UnresolvedBosses);

            Assert.AreEqual(20 - 4, stage.Life);
        }

        [Test]
        public void 클리어하면_라이프가_깎이지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(100f, 5f, range: 100f)));

            var wave = MakeWave(MakeEnemy(100f, 0f), count: 2, interval: 0.2f, timeLimit: 30f);
            var stage = new StageContext(MakeStage(20, wave));
            var combat = new CombatContext(board, wave);

            Run(combat, 30f);
            stage.ApplyResult(combat.Outcome, combat.UnresolvedEnemies, combat.UnresolvedBosses);

            Assert.AreEqual(20, stage.Life);
        }

        [Test]
        public void 아직_나오지_않은_적도_잔여로_친다()
        {
            // 제한시간이 스폰보다 먼저 끝나면 안 나온 적도 라이프를 깎는다
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 5, interval: 10f, timeLimit: 1f);
            var combat = new CombatContext(new GridBoard(), wave);

            Run(combat, 5f);

            Assert.AreEqual(CombatOutcome.TimedOut, combat.Outcome);
            Assert.AreEqual(1, combat.RemainingEnemies, "1마리만 나왔다");
            Assert.AreEqual(5, combat.UnresolvedEnemies, "안 나온 4마리도 잔여로 친다");
        }

        // ---------- 스테이지 진행 ----------

        [Test]
        public void 라이프가_0이_되면_게임_오버다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 3, interval: 0.1f, timeLimit: 1f);
            var stage = new StageContext(MakeStage(startingLife: 3, wave, wave));

            var combat = new CombatContext(new GridBoard(), wave);
            Run(combat, 5f);
            stage.ApplyResult(combat.Outcome, combat.UnresolvedEnemies, combat.UnresolvedBosses);

            Assert.AreEqual(0, stage.Life);
            Assert.IsTrue(stage.IsGameOver);
        }

        [Test]
        public void 라이프는_음수가_되지_않는다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), count: 10, interval: 0.1f, timeLimit: 2f);
            var stage = new StageContext(MakeStage(startingLife: 3, wave));

            var combat = new CombatContext(new GridBoard(), wave);
            Run(combat, 5f);
            stage.ApplyResult(combat.Outcome, combat.UnresolvedEnemies, combat.UnresolvedBosses);

            Assert.AreEqual(0, stage.Life);
        }

        [Test]
        public void 웨이브를_넘기면_다음_웨이브가_현재가_된다()
        {
            var first = MakeWave(MakeEnemy(100f, 0f), 1, 0f, 10f);
            var second = MakeWave(MakeEnemy(100f, 0f), 2, 0f, 10f);
            var stage = new StageContext(MakeStage(20, first, second));

            Assert.AreSame(first, stage.CurrentWave);

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.AreSame(second, stage.CurrentWave);
            Assert.IsFalse(stage.IsAllWavesCleared);

            stage.ApplyResult(CombatOutcome.Cleared, 0, 0);

            Assert.IsTrue(stage.IsAllWavesCleared);
            Assert.IsNull(stage.CurrentWave);
        }

        [Test]
        public void 끝나지_않은_전투_결과는_반영할_수_없다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), 1, 0f, 10f);
            var stage = new StageContext(MakeStage(20, wave));

            Assert.Throws<InvalidOperationException>(() => stage.ApplyResult(CombatOutcome.InProgress, 0, 0));
        }

        [Test]
        public void 시간은_뒤로_흐르지_않는다()
        {
            var wave = MakeWave(MakeEnemy(100f, 0f), 1, 0f, 10f);
            var combat = new CombatContext(new GridBoard(), wave);

            Assert.Throws<ArgumentOutOfRangeException>(() => combat.Tick(-0.1f));
        }
    }
}
