using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    public enum CombatOutcome
    {
        InProgress,

        // 제한시간 안에 전멸시킴
        Cleared,

        // 제한시간이 끝났는데 적이 남음
        TimedOut,
    }

    /**
     * CombatContext
     *
     * 웨이브 하나의 전투. 스폰, 적 이동, 유닛 공격, 클리어 판정을 전부 여기서 돌린다
     *
     * MonoBehaviour가 아니고 deltaTime을 인자로 받는다 (DESIGN §5.6)
     * 고정 틱을 강제하지 않으므로 Update + deltaTime 방침과 충돌하지 않으면서,
     * 테스트에서 Tick을 원하는 만큼 돌려 전투 한 판을 통째로 시뮬레이션할 수 있다
     *
     * 유닛 공격 쿨다운은 여기서 슬롯별로 들고 있다
     * 보드(GridBoard)는 배치 상태만 알고 전투 상태는 모른다
     */
    public sealed class CombatContext
    {
        struct ScheduledSpawn
        {
            public float Time;
            public EnemyDefinition Enemy;
        }

        readonly GridBoard board;
        readonly List<ScheduledSpawn> schedule = new List<ScheduledSpawn>();
        readonly List<EnemyInstance> enemies = new List<EnemyInstance>();
        readonly float[] cooldowns = new float[GridBoard.SlotCount];

        int nextSpawnIndex;

        public CombatContext(GridBoard board, WaveDefinition wave)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (wave == null)
            {
                throw new ArgumentNullException(nameof(wave));
            }

            this.board = board;
            Wave = wave;

            BuildSchedule(wave);
        }

        public WaveDefinition Wave { get; }

        public CombatOutcome Outcome { get; private set; } = CombatOutcome.InProgress;

        public float ElapsedTime { get; private set; }

        // 살아 있는 적만 들어 있다
        public IReadOnlyList<EnemyInstance> Enemies => enemies;

        public int RemainingEnemies => enemies.Count;

        // 아직 나오지 않은 적까지 포함한 잔여 수. 시간 초과 시 라이프를 깎는 기준이다
        public int UnresolvedEnemies => enemies.Count + (schedule.Count - nextSpawnIndex);

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "시간은 뒤로 흐르지 않습니다");
            }

            if (Outcome != CombatOutcome.InProgress)
            {
                return;
            }

            ElapsedTime += deltaTime;

            Spawn();
            Move(deltaTime);
            Attack(deltaTime);
            RemoveDead();
            UpdateOutcome();
        }

        void BuildSchedule(WaveDefinition wave)
        {
            IReadOnlyList<WaveDefinition.SpawnEntry> entries = wave.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                WaveDefinition.SpawnEntry entry = entries[i];

                if (entry.enemy == null)
                {
                    continue;
                }

                for (int n = 0; n < entry.count; n++)
                {
                    schedule.Add(new ScheduledSpawn
                    {
                        Time = entry.startDelay + entry.interval * n,
                        Enemy = entry.enemy,
                    });
                }
            }

            schedule.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        void Spawn()
        {
            while (nextSpawnIndex < schedule.Count && schedule[nextSpawnIndex].Time <= ElapsedTime)
            {
                enemies.Add(new EnemyInstance(schedule[nextSpawnIndex].Enemy));
                nextSpawnIndex++;
            }
        }

        void Move(float deltaTime)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].Advance(deltaTime);
            }
        }

        void Attack(float deltaTime)
        {
            for (int slot = 0; slot < GridBoard.SlotCount; slot++)
            {
                UnitInstance unit = board[slot];

                if (unit == null)
                {
                    cooldowns[slot] = 0f;
                    continue;
                }

                cooldowns[slot] -= deltaTime;

                if (cooldowns[slot] > 0f)
                {
                    continue;
                }

                EnemyInstance target = FindTarget(slot, unit);

                if (target == null)
                {
                    // 사거리에 아무도 없으면 쿨다운을 0에 붙여둔다. 적이 들어오는 즉시 쏜다
                    cooldowns[slot] = 0f;
                    continue;
                }

                target.TakeDamage(unit.AttackPower);
                cooldowns[slot] = 1f / unit.AttacksPerSecond;
            }
        }

        // 사거리 안에서 가장 앞선 적 하나 (DESIGN §5.5)
        EnemyInstance FindTarget(int slot, UnitInstance unit)
        {
            Vector2 slotPosition = GridBoard.SlotToLocalPosition(slot);
            EnemyInstance best = null;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance enemy = enemies[i];

                if (Vector2.Distance(slotPosition, enemy.Position) > unit.Range)
                {
                    continue;
                }

                if (best == null || enemy.Progress > best.Progress)
                {
                    best = enemy;
                }
            }

            return best;
        }

        void RemoveDead()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].IsAlive == false)
                {
                    enemies.RemoveAt(i);
                }
            }
        }

        void UpdateOutcome()
        {
            bool spawnedEverything = nextSpawnIndex >= schedule.Count;

            // 마지막 적이 제한시간과 동시에 죽으면 클리어로 친다
            if (spawnedEverything && enemies.Count == 0)
            {
                Outcome = CombatOutcome.Cleared;
                return;
            }

            if (ElapsedTime >= Wave.TimeLimit)
            {
                Outcome = CombatOutcome.TimedOut;
            }
        }
    }
}
