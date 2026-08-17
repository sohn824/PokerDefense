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

        struct AimState
        {
            public AimDirection Direction;
            public float LastShotTime;

            // 쌍무기가 좌우를 번갈아 쏘는 데 쓴다. 뷰가 프레임으로 세면 어긋난다
            public int ShotCount;
        }

        /**
         * 피격 이벤트가 발생했을 때
         * 한 발이 실제로 때린 지점을 타격 이펙트 표시를 위해 기록
         */
        public struct HitEvent
        {
            public Vector2 Position;
            public float Time;

            // 때린 유닛 정보
            public UnitDefinition Source;
        }

        /**
         * Splash 한 발의 폭발 이벤트가 발생했을 때
         * 착탄 지점과 그때의 splashRadius를 함께 기록
         */
        public struct SplashEvent
        {
            public Vector2 Position;
            public float Radius;
            public float Time;
            public UnitDefinition Source;
        }

        /**
         * Pierce 한 발이 적을 관통할 때
         * 궤적 표시를 위해 필요한 정보 기록
         */
        public struct PierceEvent
        {
            // 유닛이 선 칸의 중심과 겨눈 적의 위치
            public Vector2 Origin;
            public Vector2 Target;

            // 그 방향에서 창끝이 어디 붙어 있는지 고르는 데 쓴다
            public AimDirection Direction;

            public float Time;
            public UnitDefinition Source;
        }

        // 피격 기록을 들고 있는 시간 (타격 이펙트 수명보다 길어야 함)
        const float HitMemorySeconds = 0.5f;

        // 앞선 적부터 정렬 (Multi 타입이 사거리 안에서 타격할 적들을 정할 때 쓴다)
        static readonly Comparison<EnemyInstance> ByProgressDescending
            = (a, b) => b.Progress.CompareTo(a.Progress);

        readonly GridBoard board;
        readonly PerkSet perks; // 적용된 특전들
        readonly List<ScheduledSpawn> waveSchedule = new List<ScheduledSpawn>();
        readonly List<EnemyInstance> enemies = new List<EnemyInstance>();

        // 공격 한 번이 때릴 대상 리스트
        readonly List<EnemyInstance> shotTargets = new List<EnemyInstance>();

        // 유닛별 남은 공격 쿨타임
        readonly Dictionary<UnitInstance, float> cooldowns = new Dictionary<UnitInstance, float>();

        // 유닛별 마지막으로 겨눈 방향과 쏜 시각 (이펙트 표시용)
        readonly Dictionary<UnitInstance, AimState> aims = new Dictionary<UnitInstance, AimState>();

        // 최근 피격 지점
        // 오래된 것은 Tick에서 버린다
        readonly List<HitEvent> hits = new List<HitEvent>();

        // 최근 관통 구간. 피격 지점과 같은 주기로 버린다
        readonly List<PierceEvent> pierces = new List<PierceEvent>();

        // 최근 Splash 폭발. 피격 지점과 같은 주기로 버린다
        readonly List<SplashEvent> splashes = new List<SplashEvent>();

        int nextSpawnIndex;

        public CombatContext(GridBoard board, WaveDefinition wave, PerkSet perks = null)
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
            this.perks = perks ?? PerkSet.Empty;
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

            PruneHits();

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

                AimState previous;
                aims.TryGetValue(unit, out previous);

                aims[unit] = new AimState
                {
                    Direction = DirectionTo(slotPosition, target.Position),
                    LastShotTime = ElapsedTime,
                    ShotCount = previous.ShotCount + 1,
                };

                Fire(slotPosition, unit, target);
                cooldowns[unit] = 1f / unit.AttacksPerSecond;
            }
        }

        public IReadOnlyList<HitEvent> RecentHits => hits;

        public IReadOnlyList<PierceEvent> RecentPierces => pierces;

        public IReadOnlyList<SplashEvent> RecentSplashes => splashes;

        // 유닛이 마지막으로 겨눈 방향
        public AimDirection AimOf(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? state.Direction : AimDirection.Down;
        }

        // 마지막 발사로부터 지난 시간
        public float SecondsSinceShot(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? ElapsedTime - state.LastShotTime : -1f;
        }

        // 지금까지 쏜 횟수 (쌍권총류 무기가 어느 쪽 총을 쏠 차례인지 정할 때 사용)
        public int ShotCountOf(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? state.ShotCount : 0;
        }

        // 화면 기준 4분할
        // 가로 성분이 더 크면 좌우, 아니면 상하로 본다
        static AimDirection DirectionTo(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? AimDirection.Right : AimDirection.Left;
            }

            return delta.y >= 0f ? AimDirection.Up : AimDirection.Down;
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
        // 공격 패턴에 따라 때릴 적들을 찾고 해당 적들에게 데미지 적용
        void Fire(Vector2 slotPosition, UnitInstance unit, EnemyInstance target)
        {
            switch (unit.Definition.Pattern)
            {
                case AttackPattern.Multi:
                    CollectMulti(slotPosition, unit);
                    break;

                case AttackPattern.Splash:
                    CollectSplash(unit, target);

                    // Splash 이벤트 기록 (착탄 지점 하나에 한 번)
                    splashes.Add(new SplashEvent
                    {
                        Position = target.Position,
                        Radius = unit.Definition.SplashRadius,
                        Time = ElapsedTime,
                        Source = unit.Definition,
                    });
                    break;

                case AttackPattern.Pierce:
                    CollectPierce(unit, target);

                    // Pierce 이벤트 기록
                    pierces.Add(new PierceEvent
                    {
                        Origin = slotPosition,
                        Target = target.Position,
                        Direction = DirectionTo(slotPosition, target.Position),
                        Time = ElapsedTime,
                        Source = unit.Definition,
                    });
                    break;

                default:
                    // Rapid / Heavy = 단일 타겟
                    shotTargets.Clear();
                    shotTargets.Add(target);
                    break;
            }

            // 특전 반영 (공격력에 관여하는 특전이 있으면 반영된 공격력을 가져오고, 없으면 유닛 공격력 그대로 가져옴)
            float power = perks.AttackPowerOf(unit);

            for (int i = 0; i < shotTargets.Count; i++)
            {
                shotTargets[i].TakeDamage(power);
                hits.Add(new HitEvent
                {
                    Position = shotTargets[i].Position,
                    Time = ElapsedTime,
                    Source = unit.Definition,
                });
            }
        }

        /**
         * 오래된 HitEvent들을 주기적으로 제거하는 Garbage Collector 메소드
         * 투 포인터로 hits를 순회하면서 살아남을 이벤트들만 남겨서
         * 앞쪽부터 덮어씌우고 오래된 이벤트들은 뒤쪽에서 한꺼번에 제거
         */
        void PruneHits()
        {
            // 이펙트 지속 시간보다 조금 길게만 HitEvent를 유지
            float minKeepTime = ElapsedTime - HitMemorySeconds;
            int keepIndex = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                // 살아남을 이벤트들은 앞쪽으로 옮김
                if (hits[i].Time >= minKeepTime)
                {
                    hits[keepIndex] = hits[i];
                    keepIndex++;
                }
            }

            // keepIndex 이후의 오래된 이벤트들은 한꺼번에 제거
            // (위에서 덮어씌울때마다 keepIndex를 증가시켰으므로
            // 이 시점에서 뒤쪽에 남은 이벤트들은 모두 오래된 이벤트)
            hits.RemoveRange(keepIndex, hits.Count - keepIndex);

            keepIndex = 0;

            for (int i = 0; i < pierces.Count; i++)
            {
                if (pierces[i].Time >= minKeepTime)
                {
                    pierces[keepIndex] = pierces[i];
                    keepIndex++;
                }
            }

            pierces.RemoveRange(keepIndex, pierces.Count - keepIndex);

            keepIndex = 0;

            for (int i = 0; i < splashes.Count; i++)
            {
                if (splashes[i].Time >= minKeepTime)
                {
                    splashes[keepIndex] = splashes[i];
                    keepIndex++;
                }
            }

            splashes.RemoveRange(keepIndex, splashes.Count - keepIndex);
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
