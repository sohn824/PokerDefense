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
     * CombatContext.Tick이 시간을 인자로 받으므로 씬 없이 웨이브 전투 한 번을 통째로 돌려 볼 수 있음
     * 다루는 것 - 트랙, 이동, 스폰, 공격, 결과 판정, 스테이지 진행
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

        // step 간격으로 Tick을 반복함, 전투가 끝나면 멈춤
        // 횟수는 seconds / step을 올림하므로 seconds보다 최대 한 step 더 돌 수 있음
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
                float radius = TrackPath.Arena.Radius;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y))
                    - new Vector2(GridBoard.TrackHalfWidth - radius, GridBoard.TrackHalfHeight - radius);
                float signedDistance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                    + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                Assert.AreEqual(0f, signedDistance, .0001f, $"진행도 {i / 100f}: {p}");
            }
        }

        // ---------- 이동 ----------

        [Test]
        public void 적은_시간이_지나면_트랙을_따라_나아간다()
        {
            var enemy = new EnemyInstance(MakeEnemy(100f, GridBoard.TrackLength));

            enemy.Move(0.5f);

            Assert.AreEqual(0.5f, enemy.Progress, 0.0001f, "트랙 길이만큼의 속도면 1초에 한 바퀴다");
        }

        [Test]
        public void 적은_한_바퀴를_넘어도_계속_돈다()
        {
            var enemy = new EnemyInstance(MakeEnemy(100f, GridBoard.TrackLength));

            enemy.Move(2.5f);

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
        public void 공격_간격이_Tick으로_나누어떨어지지_않아도_공격속도가_유지된다()
        {
            // 초당 3.9회(약 0.256초 간격)를 0.05초 Tick으로 9.5초 돌림
            // 0초·0.256초·…·9.487초에 쏴서 38발이어야 함
            // 늦게 쏜 만큼을 다음 쿨타임에서 빼지 않으면 6 Tick(0.3초)마다 쏘게 되어 32발에 그침
            var unit = new UnitInstance(MakeUnit(1f, attacksPerSecond: 3.9f, range: 100f));
            var board = new GridBoard();
            board.TryPlace(7, unit);

            var wave = MakeWave(MakeEnemy(1000000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            for (int i = 0; i < 190; i++)
            {
                combat.Tick(0.05f);
            }

            Assert.AreEqual(38, combat.ShotCountOf(unit));
        }

        [Test]
        public void 발사_횟수는_쏜_만큼_쌓인다()
        {
            // 총구가 여럿인 유닛은 쏜 횟수로 어느 총구를 쏠지 고름
            // 화면이 프레임 단위로 세면 발사와 어긋나므로 CombatContext가 셈
            var unit = new UnitInstance(MakeUnit(10f, attacksPerSecond: 1f, range: 100f));
            var board = new GridBoard();
            board.TryPlace(7, unit);

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Assert.AreEqual(0, combat.ShotCountOf(unit), "쏘기 전인데 세고 있다");

            Run(combat, 3.05f);

            Assert.AreEqual(4, combat.ShotCountOf(unit));
        }

        [Test]
        public void 피격_기록은_때린_유닛을_함께_남긴다()
        {
            // 유닛마다 타격 이펙트가 다를 수 있어 누가 때렸는지가 필요함
            UnitDefinition definition = MakeUnit(10f, attacksPerSecond: 1f, range: 100f);
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(definition));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            IReadOnlyList<CombatContext.HitEvent> hits = combat.RecentHits;
            Assert.Greater(hits.Count, 0, "때린 기록이 없다");

            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreSame(definition, hits[i].Source);
            }
        }

        [Test]
        public void 관통은_찌른_선을_남긴다()
        {
            // 맞은 지점만으로는 궤적을 못 그리므로 시작점(유닛 칸)과 겨눈 지점을 함께 남겨야 함
            UnitDefinition definition = MakeUnit(10f, attacksPerSecond: 1f, range: 100f);
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("attackPattern").enumValueIndex = (int)AttackPattern.Pierce;
            so.FindProperty("pierceLength").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(definition));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            IReadOnlyList<CombatContext.PierceEvent> lines = combat.RecentPierces;
            Assert.Greater(lines.Count, 0, "관통 기록이 없다");

            CombatContext.PierceEvent line = lines[0];
            Assert.AreSame(definition, line.Source);
            Assert.AreEqual(GridBoard.SlotToLocalPosition(7), line.Origin, "유닛이 선 칸이 아니다");
            Assert.AreEqual(combat.Enemies[0].Position, line.Target, "겨눈 적이 아니다");
        }

        [Test]
        public void 찌른_선의_방향은_유닛에서_적을_본_방향이다()
        {
            // 총구 위치가 방향마다 달라 화면이 이 방향으로 궤적 시작점을 고름
            UnitDefinition definition = MakeUnit(10f, attacksPerSecond: 1f, range: 100f);
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("attackPattern").enumValueIndex = (int)AttackPattern.Pierce;
            so.FindProperty("pierceLength").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(definition));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            CombatContext.PierceEvent line = combat.RecentPierces[0];
            Vector2 delta = line.Target - line.Origin;

            AimDirection expected = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? AimDirection.Right : AimDirection.Left)
                : (delta.y >= 0f ? AimDirection.Up : AimDirection.Down);

            Assert.AreEqual(expected, line.Direction);
        }

        [Test]
        public void 관통이_아니면_찌른_선을_안_남긴다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(10f, attacksPerSecond: 1f, range: 100f)));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            Assert.Greater(combat.RecentHits.Count, 0, "때리지도 않았다");
            Assert.AreEqual(0, combat.RecentPierces.Count);
        }

        [Test]
        public void 폭발_기록은_착탄_지점과_반경을_남긴다()
        {
            // 화면에 그리는 폭발이 실제 피해 범위와 같은 크기여야 하므로 반경을 함께 남김
            UnitDefinition definition = MakeUnit(10f, attacksPerSecond: 1f, range: 100f);
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("attackPattern").enumValueIndex = (int)AttackPattern.Splash;
            so.FindProperty("splashRadius").floatValue = 1.6f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(definition));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            IReadOnlyList<CombatContext.SplashEvent> splashes = combat.RecentSplashes;
            Assert.Greater(splashes.Count, 0, "폭발 기록이 없다");

            CombatContext.SplashEvent splash = splashes[0];
            Assert.AreSame(definition, splash.Source);
            Assert.AreEqual(1.6f, splash.Radius, 0.0001f);
            Assert.AreEqual(combat.Enemies[0].Position, splash.Position, "착탄 지점이 아니다");
        }

        [Test]
        public void 폭발은_맞은_적_수와_무관하게_한_발에_한_번만_남는다()
        {
            // Splash 한 발이 여럿을 때려도 폭발 연출은 착탄 지점 하나임
            UnitDefinition definition = MakeUnit(10f, attacksPerSecond: 1f, range: 100f);
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("attackPattern").enumValueIndex = (int)AttackPattern.Splash;
            so.FindProperty("splashRadius").floatValue = 100f; // 스폰된 적이 모두 반경 안에 들게 함
            so.ApplyModifiedPropertiesWithoutUndo();

            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(definition));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 4, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            combat.Tick(0.05f);

            Assert.AreEqual(4, combat.RemainingEnemies, "넷 다 스폰되지 않았다");
            Assert.AreEqual(4, combat.RecentHits.Count, "넷 다 맞지 않았다");
            Assert.AreEqual(1, combat.RecentSplashes.Count, "한 발인데 폭발이 여러 번 남았다");
        }

        [Test]
        public void Splash가_아니면_폭발_기록을_안_남긴다()
        {
            var board = new GridBoard();
            board.TryPlace(7, new UnitInstance(MakeUnit(10f, attacksPerSecond: 1f, range: 100f)));

            var wave = MakeWave(MakeEnemy(1000f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 1.05f);

            Assert.Greater(combat.RecentHits.Count, 0, "때리지도 않았다");
            Assert.AreEqual(0, combat.RecentSplashes.Count);
        }

        [Test]
        public void 사거리_밖이면_발사_횟수가_늘지_않는다()
        {
            var unit = new UnitInstance(MakeUnit(50f, 1f, range: 0.1f));
            var board = new GridBoard();
            board.TryPlace(7, unit);

            var wave = MakeWave(MakeEnemy(100f, 0f), count: 1, interval: 0f, timeLimit: 100f);
            var combat = new CombatContext(board, wave);

            Run(combat, 5f);

            Assert.AreEqual(0, combat.ShotCountOf(unit));
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
            // MakeUnit의 ★2 배수가 2라 공격력·공격속도가 모두 2배가 되어 같은 적을 더 빨리 잡음
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

        [Test]
        public void 전투_중에_유닛을_옮겨도_연사가_되지_않는다()
        {
            // 전투 중 배치 변경은 허용된 전략임
            // 쿨타임을 슬롯으로 들고 있으면 빈 칸으로 옮길 때마다 쿨타임 0을 물려받아 이동만으로 연사가 됨
            var unit = MakeUnit(10f, 1f, range: 100f);
            var enemy = MakeEnemy(1000000f, 0f);
            var wave = MakeWave(enemy, count: 1, interval: 0f, timeLimit: 1000f);

            var still = new GridBoard();
            still.TryPlace(0, new UnitInstance(unit));
            var stillCombat = new CombatContext(still, wave);

            var moving = new GridBoard();
            moving.TryPlace(0, new UnitInstance(unit));
            var movingCombat = new CombatContext(moving, wave);

            int at = 0;

            for (int i = 0; i < 100; i++)
            {
                stillCombat.Tick(0.1f);

                movingCombat.Tick(0.1f);
                int next = (at + 1) % GridBoard.SlotCount;

                if (moving.TryMoveSlot(at, next))
                {
                    at = next;
                }
            }

            float stillDamage = enemy.MaxHp - stillCombat.Enemies[0].Hp;
            float movingDamage = enemy.MaxHp - movingCombat.Enemies[0].Hp;

            Assert.AreEqual(stillDamage, movingDamage, 0.001f, "이동한 쪽이 더 때렸다면 쿨다운이 초기화된 것이다");
        }

        // ---------- 결과 판정 ----------

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

            // 0초에 1기, 10초에 1기라 첫 적을 잡아도 웨이브는 안 끝남
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
            // 유닛이 하나도 없으니 적이 죽지 않음
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
            // 제한시간이 스폰보다 먼저 끝나면 안 나온 적도 라이프를 깎음
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
