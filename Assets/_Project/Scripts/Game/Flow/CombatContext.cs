using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CombatOutcome
     *
     * 전투 결과 (CombatController.CombatFinished로 전달)
     */
    public enum CombatOutcome
    {
        // 진행 중
        InProgress,

        // 제한시간 안에 모든 적을 처치함
        Cleared,

        // 제한시간이 끝났는데 적이 남음
        TimedOut,
    }

    /**
     * CombatContext
     *
     * 웨이브 하나의 전투를 진행하는 객체 (웨이브마다 CombatController가 새로 만듦)
     * Tick 한 번에 스폰 -> 적 이동 -> 유닛 공격 -> 죽은 적 제거 -> 결과 판정 순서로 진행
     */
    public sealed class CombatContext
    {
        // Time초에 Enemy 1기가 나온다는 예약
        struct ScheduledSpawn
        {
            public float Time;
            public EnemyDefinition Enemy;
        }

        // 유닛 1기의 조준 상태 (화면 연출용)
        struct AimState
        {
            public AimDirection Direction;
            public float LastShotTime;

            // 지금까지 쏜 횟수 (총구가 여럿인 유닛이 이번에 어느 총구로 쏠지 고를 때 씀)
            public int ShotCount;
        }

        /**
         * 한 발이 적 1기를 맞힌 기록 (CombatScreen이 타격 이펙트를 그림)
         */
        public struct HitEvent
        {
            // 맞은 적의 위치
            public Vector2 Position;
            public float Time;

            // 때린 유닛 종류 (유닛마다 이펙트가 다를 수 있음)
            public UnitDefinition Source;
        }

        /**
         * Splash 한 발의 폭발 기록 (맞은 적 수와 상관없이 한 발에 1건)
         * 화면에 그리는 폭발이 실제 피해 범위와 같도록 그때의 SplashRadius를 함께 남김
         */
        public struct SplashEvent
        {
            public Vector2 Position;
            public float Radius;
            public float Time;
            public UnitDefinition Source;
        }

        /**
         * Pierce 한 발의 관통 기록 (CombatScreen이 유닛에서 타겟까지 궤적을 그림)
         */
        public struct PierceEvent
        {
            // 유닛이 선 칸의 중심과 겨눈 적의 위치
            public Vector2 Origin;
            public Vector2 Target;

            // 유닛이 겨눈 방향 (궤적이 시작할 총구 위치와 그리기 순서를 고르는 데 씀)
            public AimDirection Direction;

            public float Time;
            public UnitDefinition Source;
        }

        /**
         * 처치하지 못한 적 한 종류(Type)의 수 (CollectUnresolved가 만들고, 결과 화면이 "남은 적"으로 보여줌)
         * Live - 트랙에 살아 있는 수
         * Pending - 제한시간 안에 나오지 못한 수
         * Total - 둘을 합친 수
         */
        public struct UnresolvedCount
        {
            public EnemyType Type;
            public int Live;
            public int Pending;

            public int Total => Live + Pending;
        }

        // 이펙트 기록을 들고 있는 시간 (화면의 타격 이펙트 수명보다 길어야 함)
        const float EffectMemorySeconds = 0.5f;

        // progress가 큰 적(트랙을 많이 간 적)부터 정렬 (Multi 패턴이 때릴 적을 고를 때 씀)
        static readonly Comparison<EnemyInstance> ByProgressDescending
            = (a, b) => b.Progress.CompareTo(a.Progress);

        readonly GridBoard board;

        // 적이 나올 시각표 (시각순 정렬)
        readonly List<ScheduledSpawn> waveSchedule = new List<ScheduledSpawn>();

        // 트랙에 살아 있는 적
        readonly List<EnemyInstance> enemies = new List<EnemyInstance>();

        // 공격 한 번이 때릴 적 목록 (발사할 때마다 비우고 다시 채움)
        readonly List<EnemyInstance> shotTargets = new List<EnemyInstance>();

        // 유닛별 남은 공격 쿨타임
        readonly Dictionary<UnitInstance, float> cooldowns = new Dictionary<UnitInstance, float>();

        // 유닛별 마지막으로 겨눈 방향·쏜 시각·쏜 횟수 (화면 연출용)
        readonly Dictionary<UnitInstance, AimState> aims = new Dictionary<UnitInstance, AimState>();

        // 최근 피격 기록 (EffectMemorySeconds보다 오래된 것은 Tick마다 버림)
        readonly List<HitEvent> hits = new List<HitEvent>();

        // 최근 관통 기록 (EffectMemorySeconds보다 오래된 것은 Tick마다 버림)
        readonly List<PierceEvent> pierces = new List<PierceEvent>();

        // 최근 Splash 폭발 기록 (EffectMemorySeconds보다 오래된 것은 Tick마다 버림)
        readonly List<SplashEvent> splashes = new List<SplashEvent>();

        // 시각표에서 다음에 나올 적의 인덱스
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

            // 웨이브 데이터로 적이 나올 시각표를 미리 만들어 둠
            BuildSchedule(wave);
        }

        public WaveDefinition Wave { get; }

        public CombatOutcome Outcome { get; private set; } = CombatOutcome.InProgress;

        // 전투 시작 후 지난 시간
        public float ElapsedTime { get; private set; }

        // 트랙에 살아 있는 적 목록
        public IReadOnlyList<EnemyInstance> Enemies => enemies;

        // 트랙에 살아 있는 적 수 (아직 안 나온 적 제외)
        public int RemainingEnemies => enemies.Count;

        // 처치하지 못한 적 수 (살아 있는 적 + 아직 안 나온 적, 시간 초과 시 라이프 피해 계산에 씀)
        public int UnresolvedEnemies => enemies.Count + (waveSchedule.Count - nextSpawnIndex);

        // 다음 적이 나오기까지 남은 시간 (더 나올 적이 없으면 -1)
        public float SecondsToNextSpawn
            => nextSpawnIndex >= waveSchedule.Count
                ? -1f
                : Mathf.Max(0f, waveSchedule[nextSpawnIndex].Time - ElapsedTime);

        // 처치하지 못한 적 중 보스 수
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

        // 처치하지 못한 적을 종류별로 셈
        // 전투가 끝난 뒤 GameFlowController가 결과 화면용 기록을 만들 때 사용
        public List<UnresolvedCount> CollectUnresolved()
        {
            EnemyType[] all = (EnemyType[])Enum.GetValues(typeof(EnemyType));
            int[] live = new int[all.Length];
            int[] pending = new int[all.Length];

            for (int i = 0; i < enemies.Count; i++)
            {
                live[(int)enemies[i].Definition.Type]++;
            }

            for (int i = nextSpawnIndex; i < waveSchedule.Count; i++)
            {
                pending[(int)waveSchedule[i].Enemy.Type]++;
            }

            List<UnresolvedCount> result = new List<UnresolvedCount>();

            for (int i = 0; i < all.Length; i++)
            {
                if (live[i] > 0 || pending[i] > 0)
                {
                    result.Add(new UnresolvedCount { Type = all[i], Live = live[i], Pending = pending[i] });
                }
            }

            return result;
        }

        // deltaTime(초)만큼 전투를 한 단계 진행
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

            // 0.5초(EffectMemorySeconds)가 지난 이펙트 기록(피격·관통·폭발)을 버림
            RemoveOldEffectRecords();

            // 시각표에서 나올 때가 된 적을 내보냄
            Spawn();
            // 적을 트랙을 따라 이동
            Move(deltaTime);
            // 쿨타임이 끝난 유닛이 공격
            Attack(deltaTime);
            // HP가 0이 된 적을 뺌
            RemoveDead();
            // Cleared·TimedOut 판정
            UpdateOutcome();
        }

        // 웨이브 엔트리로 적이 나올 시각표를 만들고 시각순으로 정렬 (enemy가 빈 엔트리는 건너뜀)
        // 예) startDelay 2, interval 0.5, count 3이면 2초·2.5초·3초에 1기씩
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

        // 예정 시각이 지난 적을 모두 트랙 시작점에 내보냄
        void Spawn()
        {
            while (nextSpawnIndex < waveSchedule.Count && waveSchedule[nextSpawnIndex].Time <= ElapsedTime)
            {
                enemies.Add(new EnemyInstance(waveSchedule[nextSpawnIndex].Enemy));
                nextSpawnIndex++;
            }
        }

        // 살아 있는 적을 모두 deltaTime만큼 트랙을 따라 이동
        void Move(float deltaTime)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].Move(deltaTime);
            }
        }

        // 보드 15칸을 돌며 쿨타임이 끝난 유닛마다 한 발씩 쏨 (한 Tick에 유닛당 최대 한 발)
        // 1. 쿨타임을 deltaTime만큼 줄임 (처음 보는 유닛은 0에서 시작해 바로 쏨)
        // 2. 사거리에서 타겟을 찾음 (없으면 쿨타임을 0으로 두고 다음 유닛으로)
        // 3. 조준 상태를 기록하고 Fire로 공격한 뒤, 다음 쿨타임을 채움 (늦게 쏜 만큼은 빼서 평균 간격을 맞춤)
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
                    // cooldowns에 아직 없는 유닛(새로 놓였거나 승급해 새 객체가 됨)은 쿨타임 0에서 시작함 -> 사거리에 적이 있으면 바로 쏨
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
                    // 쿨타임은 끝났는데 사거리에 적이 없는 경우
                    // 적이 다음 Tick에 사거리에 들어오면 바로 쏘도록 쿨타임을 0으로 초기화
                    cooldowns[unit] = 0f;
                    continue;
                }

                AimState previous;
                aims.TryGetValue(unit, out previous);

                aims[unit] = new AimState
                {
                    Direction = GetAimDirection(slotPosition, target.Position),
                    LastShotTime = ElapsedTime,
                    ShotCount = previous.ShotCount + 1,
                };

                Fire(slotPosition, unit, target);
                // 여기까지 온 cooldown은 0 이하이고, 0보다 작은 만큼이 늦게 쏜 시간임 (예: -0.01이면 0.01초 늦음)
                // 다음 쿨타임을 그만큼 짧게 채우면 늦어진 시간이 상쇄되어 평균 발사 간격이 정확히 맞음
                // 단, 프레임이 너무 크게 끊겨 한 간격보다 더 늦었으면 0으로 둠
                // 밀린 발을 몰아 쏘지 않고 다음 Tick에 한 발만 쏘기 위함
                cooldowns[unit] = Mathf.Max(0f, cooldown + 1f / unit.AttacksPerSecond);
            }
        }

        // 최근 0.5초(EffectMemorySeconds) 안의 피격·관통·폭발 기록
        public IReadOnlyList<HitEvent> RecentHits => hits;

        public IReadOnlyList<PierceEvent> RecentPierces => pierces;

        public IReadOnlyList<SplashEvent> RecentSplashes => splashes;

        // 유닛이 마지막으로 겨눈 방향 (아직 안 쐈으면 Down)
        public AimDirection AimOf(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? state.Direction : AimDirection.Down;
        }

        // 마지막 발사 후 지난 시간 (아직 안 쐈으면 -1)
        public float SecondsSinceShot(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? ElapsedTime - state.LastShotTime : -1f;
        }

        // 지금까지 쏜 횟수 (총구가 여럿인 유닛이 어느 총구로 쏠지 고를 때 씀, 아직 안 쐈으면 0)
        public int ShotCountOf(UnitInstance unit)
        {
            AimState state;
            return aims.TryGetValue(unit, out state) ? state.ShotCount : 0;
        }

        // 유닛(from)이 타겟(to)을 겨누는 방향을 상하좌우 중 하나로 고름 (유닛 아트·총구 위치 선택용)
        // from과 to의 가로 거리(|to.x - from.x|)가 세로 거리(|to.y - from.y|)보다 크거나 같으면 Left·Right, 작으면 Up·Down
        // to가 from보다 오른쪽이면 Right, 위쪽이면 Up
        static AimDirection GetAimDirection(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? AimDirection.Right : AimDirection.Left;
            }

            return delta.y >= 0f ? AimDirection.Up : AimDirection.Down;
        }

        // 사거리 안에서 progress가 가장 큰 적(트랙을 가장 많이 간 적)을 찾음 (없으면 null)
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

        // 유닛 1기의 공격 1회 처리
        // 패턴에 따라 때릴 적을 shotTargets에 모으고, 모두에게 같은 피해를 줌
        void Fire(Vector2 slotPosition, UnitInstance unit, EnemyInstance target)
        {
            switch (unit.Definition.Pattern)
            {
                case AttackPattern.Multi:
                    CollectMulti(slotPosition, unit, target);
                    break;

                case AttackPattern.Splash:
                    CollectSplash(unit, target);

                    // 폭발 기록 추가 (맞은 적 수와 상관없이 한 발에 1건)
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

                    // 관통 기록 추가
                    pierces.Add(new PierceEvent
                    {
                        Origin = slotPosition,
                        Target = target.Position,
                        Direction = GetAimDirection(slotPosition, target.Position),
                        Time = ElapsedTime,
                        Source = unit.Definition,
                    });
                    break;

                default:
                    // Rapid·Heavy - 타겟 1기만
                    shotTargets.Clear();
                    shotTargets.Add(target);
                    break;
            }

            float power = unit.AttackPower;

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

        // 0.5초(EffectMemorySeconds)보다 오래된 이펙트 기록(피격·관통·폭발)을 지움
        // 목록마다 남길 기록을 앞쪽으로 모은 뒤, 뒤쪽에 남은 오래된 기록을 RemoveRange로 한 번에 지움
        // RemoveAt으로 하나씩 지우면 지울 때마다 뒤 요소를 앞으로 당겨야 하기 때문
        void RemoveOldEffectRecords()
        {
            // 이 시각 이후에 생긴 기록만 남김
            float minKeepTime = ElapsedTime - EffectMemorySeconds;
            int keepIndex = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                // 남길 기록은 앞쪽(keepIndex 자리)으로 옮김
                if (hits[i].Time >= minKeepTime)
                {
                    hits[keepIndex] = hits[i];
                    keepIndex++;
                }
            }

            // 남길 기록을 옮길 때마다 keepIndex를 늘렸으므로, keepIndex부터 끝까지는 모두 지울 기록임
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

        // Multi - 사거리 안이면서 첫 타겟 근처(MultiSpread 안)에 있는 적을, 트랙을 많이 간 적부터 최대 MultiTargets기 수집
        void CollectMulti(Vector2 slotPosition, UnitInstance unit, EnemyInstance target)
        {
            shotTargets.Clear();

            float spread = unit.Definition.MultiSpread;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance enemy = enemies[i];

                if (Vector2.Distance(slotPosition, enemy.Position) > unit.Range)
                {
                    continue;
                }

                if (Vector2.Distance(target.Position, enemy.Position) > spread)
                {
                    continue;
                }

                shotTargets.Add(enemy);
            }

            shotTargets.Sort(ByProgressDescending);

            int limit = Mathf.Max(1, unit.Definition.MultiTargets);

            if (shotTargets.Count > limit)
            {
                shotTargets.RemoveRange(limit, shotTargets.Count - limit);
            }
        }

        // Splash - 타겟 위치에서 SplashRadius 안의 적을 모두 수집 (사거리 밖이어도 포함)
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

        // Pierce - 타겟과, 그 뒤로 PierceLength 거리 안에서 따라오는 적을 모두 수집
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

        // HP가 0이 된 적을 목록에서 뺌
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

        // 결과 판정 - 다 나왔고 다 처치했으면 Cleared, 아니면 제한시간이 지났을 때 TimedOut
        void UpdateOutcome()
        {
            bool spawnedEverything = nextSpawnIndex >= waveSchedule.Count;

            // Cleared를 먼저 보므로 마지막 적이 제한시간과 같은 Tick에 죽어도 Cleared
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
