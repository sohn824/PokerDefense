using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    public enum CombatOutcome
    {
        // 진행중
        InProgress,

        // 제한시간 안에 전멸시킴
        Cleared,

        // 제한시간이 끝났는데 적이 남음
        TimedOut,
    }

    /**
     * CombatContext
     *
     * 웨이브 하나의 전투. 스폰, 적 이동, 유닛 공격, 클리어 판정을 전부 여기서 담당
     *
     * 유닛 공격 쿨다운은 여기서 유닛별로 들고 있다
     * 보드(GridBoard)는 배치 상태만 알고 전투 상태는 모름
     */
    public sealed class CombatContext
    {
        struct ScheduledSpawn
        {
            public float Time;
            public EnemyDefinition Enemy;
        }

        readonly GridBoard board;
        readonly List<ScheduledSpawn> waveSchedule = new List<ScheduledSpawn>();
        readonly List<EnemyInstance> enemies = new List<EnemyInstance>();

        // 유닛별 남은 공격 쿨타임
        readonly Dictionary<UnitInstance, float> cooldowns = new Dictionary<UnitInstance, float>();

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

            // CombatContext가 생성될 때 웨이브 스케줄을 만들어 줌
            BuildSchedule(wave);
        }

        public WaveDefinition Wave { get; }

        public CombatOutcome Outcome { get; private set; } = CombatOutcome.InProgress;

        public float ElapsedTime { get; private set; }

        // 살아 있는 적 리스트
        public IReadOnlyList<EnemyInstance> Enemies => enemies;

        public int RemainingEnemies => enemies.Count;

        // 아직 나오지 않은 적까지 포함한 잔여 적 수
        public int UnresolvedEnemies => enemies.Count + (waveSchedule.Count - nextSpawnIndex);

        // 다음 적이 나오기까지 남은 시간 (더 나올 적이 없으면 -1)
        public float SecondsToNextSpawn
            => nextSpawnIndex >= waveSchedule.Count
                ? -1f
                : Mathf.Max(0f, waveSchedule[nextSpawnIndex].Time - ElapsedTime);

        // 잔여 적 중 보스 수
        public int UnresolvedBosses
        {
            get
            {
                int count = 0;

                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].Definition.Type == EnemyType.Boss)
                    {
                        count++;
                    }
                }

                for (int i = nextSpawnIndex; i < waveSchedule.Count; i++)
                {
                    if (waveSchedule[i].Enemy.Type == EnemyType.Boss)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // 전투 진행 프로세스
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime은 음수가 될 수 없음");
            }

            if (Outcome != CombatOutcome.InProgress)
            {
                return;
            }

            ElapsedTime += deltaTime;

            // 웨이브 스케줄에 따라 적 스폰 처리
            Spawn();
            // 적 이동 처리
            Move(deltaTime);
            // 유닛의 공격 처리
            Attack(deltaTime);
            // 죽은 적 제거 처리
            RemoveDead();
            // 전투 결과 판정 업데이트
            UpdateOutcome();
        }

        // WaveDefinition을 기반으로 웨이브 스케줄을 만들어 리스트에 넣고 시간순으로 정렬
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
                    waveSchedule.Add(new ScheduledSpawn
                    {
                        Time = entry.startDelay + entry.interval * n,
                        Enemy = entry.enemy,
                    });
                }
            }

            waveSchedule.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        // 웨이브 스케줄에 따라 적 소환
        void Spawn()
        {
            while (nextSpawnIndex < waveSchedule.Count && waveSchedule[nextSpawnIndex].Time <= ElapsedTime)
            {
                enemies.Add(new EnemyInstance(waveSchedule[nextSpawnIndex].Enemy));
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
                    continue;
                }

                float cooldown;

                if (cooldowns.TryGetValue(unit, out cooldown) == false)
                {
                    cooldown = 0f;
                }

                cooldown -= deltaTime;

                if (cooldown > 0f)
                {
                    cooldowns[unit] = cooldown;
                    continue;
                }

                EnemyInstance target = FindTarget(slot, unit);

                if (target == null)
                {
                    // 사거리에 아무도 없으면 다음에 찾았을 때 바로 쏠 수 있도록 쿨타임 초기화
                    cooldowns[unit] = 0f;
                    continue;
                }

                target.TakeDamage(unit.AttackPower);
                cooldowns[unit] = 1f / unit.AttacksPerSecond;
            }
        }

        // 사거리 안에서 가장 앞선 적을 찾기
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

        // 웨이브 성공/실패 판정
        void UpdateOutcome()
        {
            bool spawnedEverything = nextSpawnIndex >= waveSchedule.Count;

            // 마지막 적이 제한시간과 동시에 죽으면 성공으로 판정
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
