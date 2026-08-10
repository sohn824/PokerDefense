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

        // 앞선 적부터. Multi가 사거리 안에서 몇 기를 고를지 정할 때 쓴다
        static readonly Comparison<EnemyInstance> ByProgressDescending
            = (a, b) => b.Progress.CompareTo(a.Progress);

        readonly GridBoard board;
        readonly List<ScheduledSpawn> waveSchedule = new List<ScheduledSpawn>();
        readonly List<EnemyInstance> enemies = new List<EnemyInstance>();

        // 공격 한 번이 때릴 대상. 매 공격마다 새로 할당하지 않으려고 돌려 쓴다
        readonly List<EnemyInstance> shotTargets = new List<EnemyInstance>();

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

                Vector2 slotPosition = GridBoard.SlotToLocalPosition(slot);
                EnemyInstance target = FindTarget(slotPosition, unit);

                if (target == null)
                {
                    // 사거리에 아무도 없으면 다음에 찾았을 때 바로 쏠 수 있도록 쿨타임 초기화
                    cooldowns[unit] = 0f;
                    continue;
                }

                Fire(slotPosition, unit, target);
                cooldowns[unit] = 1f / unit.AttacksPerSecond;
            }
        }

        // 사거리 안에서 가장 앞선 적을 찾기
        EnemyInstance FindTarget(Vector2 slotPosition, UnitInstance unit)
        {
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

        // 유닛 공격 종류별 공격 1회 처리
        void Fire(Vector2 slotPosition, UnitInstance unit, EnemyInstance target)
        {
            switch (unit.Definition.Pattern)
            {
                case AttackPattern.Multi:
                    CollectMulti(slotPosition, unit);
                    break;

                case AttackPattern.Splash:
                    CollectSplash(unit, target);
                    break;

                case AttackPattern.Pierce:
                    CollectPierce(unit, target);
                    break;

                default:
                    // Rapid / Heavy = 단일 타겟
                    shotTargets.Clear();
                    shotTargets.Add(target);
                    break;
            }

            for (int i = 0; i < shotTargets.Count; i++)
            {
                shotTargets[i].TakeDamage(unit.AttackPower);
            }
        }

        // 사거리 안에서 앞선 순으로 최대 MultiTargets기 탐색
        // 사거리 안에서 앞선 적 우선 탐색 (unitDefinition의 MultiTargets 수만큼)
        void CollectMulti(Vector2 slotPosition, UnitInstance unit)
        {
            shotTargets.Clear();

            for (int i = 0; i < enemies.Count; i++)
            {
                if (Vector2.Distance(slotPosition, enemies[i].Position) <= unit.Range)
                {
                    shotTargets.Add(enemies[i]);
                }
            }

            shotTargets.Sort(ByProgressDescending);

            int limit = Mathf.Max(1, unit.Definition.MultiTargets);

            if (shotTargets.Count > limit)
            {
                shotTargets.RemoveRange(limit, shotTargets.Count - limit);
            }
        }

        // 착탄 지점에서 unitDefinition의 SplashRadius 반경 안에 있는 적을 모두 탐색
        void CollectSplash(UnitInstance unit, EnemyInstance target)
        {
            shotTargets.Clear();

            float radius = unit.Definition.SplashRadius;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (Vector2.Distance(target.Position, enemies[i].Position) <= radius)
                {
                    shotTargets.Add(enemies[i]);
                }
            }
        }

        // 타겟을 지나 뒤쪽으로 관통할 타겟들 탐색
        void CollectPierce(UnitInstance unit, EnemyInstance target)
        {
            shotTargets.Clear();

            float length = unit.Definition.PierceLength;

            for (int i = 0; i < enemies.Count; i++)
            {
                float behind = (target.Progress - enemies[i].Progress) * GridBoard.TrackLength;

                if (behind >= 0f && behind <= length)
                {
                    shotTargets.Add(enemies[i]);
                }
            }
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
