using System.Collections.Generic;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * CombatScreen
     *
     * 전투 표현
     * 적 스프라이트를 CombatContext의 적 목록에 맞춰 만들고 지운다
     */
    public sealed class CombatScreen : MonoBehaviour
    {
        /**
         * 적 하나의 표현 - 몸통과 체력 바
         */
        sealed class EnemyView
        {
            public Transform Root;
            public SpriteRenderer BarBack;
            public SpriteRenderer BarFill;
        }

        const float BodyScale = 0.5f;

        // 타격 이펙트를 돌릴 풀 사이즈
        const int HitPoolSize = 24;

        const float HitSeconds = 0.12f;
        const float HitScale = 0.55f;

        // Pierce 유닛 전용 찌르기 궤적 풀 사이즈
        const int TrailPoolSize = 8;
        const float TrailSeconds = 0.18f;
        const float TrailThickness = 0.84f;

        // 적 중심에서 조금 더 뻗어야 꿰뚫은 것으로 보임
        const float TrailOvershoot = 0.3f;

        // Splash 폭발 풀 사이즈
        const int SplashPoolSize = 12;
        const float SplashSeconds = 0.20f;

        // Splash 이펙트는처음 25%에서 다 자란 크기까지 커지고
        // 그 뒤로는 크기를 유지한 채 옅어지도록 연출
        const float SplashGrowRatio = 0.25f;
        const float SplashStartScale = 0.6f;

        const float BarWidth = 0.5f;
        const float BarHeight = 0.07f;
        const float BarBorder = 0.04f;
        const float BarOffsetY = 0.36f;

        static readonly Color BarBackColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        static readonly Color BarFillColor = new Color(0.40f, 0.85f, 0.35f);

        [SerializeField] CombatController controller;
        [SerializeField] Transform boardRoot;
        [SerializeField] Sprite enemySprite;

        [Tooltip("타격 이펙트")]
        [SerializeField] Sprite hitSprite;

        [Tooltip("Splash 공용 폭발 이펙트")]
        [SerializeField] Sprite splashSprite;
        [SerializeField] TMP_Text lifeLabel;
        [SerializeField] TMP_Text waveLabel;
        [SerializeField] TMP_Text combatLabel;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startLabel;

        readonly Dictionary<EnemyInstance, EnemyView> views = new Dictionary<EnemyInstance, EnemyView>();
        readonly List<EnemyInstance> gone = new List<EnemyInstance>();

        SpriteRenderer[] hitPool;
        float[] hitBornAt;
        int hitCursor;

        SpriteRenderer[] trailPool;
        float[] trailBornAt;
        int trailCursor;

        SpriteRenderer[] splashPool;
        float[] splashBornAt;

        // Splash 이펙트의 최대 scale (점점 커지도록 연출)
        float[] splashFullScale;
        int splashCursor;

        float lastHitSeen;
        float lastPierceSeen;
        float lastSplashSeen;

        string lastOutcome = string.Empty;

        void Awake()
        {
            startButton.onClick.AddListener(controller.StartCombat);
            controller.CombatStarted += OnCombatStarted;
            controller.CombatFinished += OnCombatFinished;
            BuildHitPool();
            BuildTrailPool();
            BuildSplashPool();
        }

        void BuildSplashPool()
        {
            splashPool = new SpriteRenderer[SplashPoolSize];
            splashBornAt = new float[SplashPoolSize];
            splashFullScale = new float[SplashPoolSize];

            for (int i = 0; i < SplashPoolSize; i++)
            {
                var gameObject = new GameObject("SplashEffect");
                gameObject.transform.SetParent(boardRoot, false);

                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = 9;
                spriteRenderer.enabled = false;

                splashPool[i] = spriteRenderer;
                splashBornAt[i] = float.NegativeInfinity;
            }
        }

        void BuildTrailPool()
        {
            trailPool = new SpriteRenderer[TrailPoolSize];
            trailBornAt = new float[TrailPoolSize];

            for (int i = 0; i < TrailPoolSize; i++)
            {
                var gameObject = new GameObject("PierceTrail");
                gameObject.transform.SetParent(boardRoot, false);

                var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.enabled = false;

                trailPool[i] = spriteRenderer;
                trailBornAt[i] = float.NegativeInfinity;
            }
        }

        void BuildHitPool()
        {
            hitPool = new SpriteRenderer[HitPoolSize];
            hitBornAt = new float[HitPoolSize];

            for (int i = 0; i < HitPoolSize; i++)
            {
                var go = new GameObject("HitEffect");
                go.transform.SetParent(boardRoot, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = hitSprite;
                sr.sortingOrder = 9;
                sr.enabled = false;

                hitPool[i] = sr;
                hitBornAt[i] = float.NegativeInfinity;
            }
        }

        void OnCombatStarted(CombatContext combat)
        {
            lastOutcome = string.Empty;

            // 웨이브가 바뀌면 ElapsedTime이 0부터 다시 흐른다
            lastHitSeen = -1f;
            lastPierceSeen = -1f;
            lastSplashSeen = -1f;

            for (int i = 0; i < hitPool.Length; i++)
            {
                hitPool[i].enabled = false;
            }

            for (int i = 0; i < trailPool.Length; i++)
            {
                trailPool[i].enabled = false;
            }

            for (int i = 0; i < splashPool.Length; i++)
            {
                splashPool[i].enabled = false;
            }
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            if (outcome == CombatOutcome.Cleared)
            {
                lastOutcome = controller.Combat.Wave.JokerReward > 0
                    ? "웨이브 클리어 - 조커 획득"
                    : "웨이브 클리어";
            }
            else
            {
                int damage = controller.Stage.LifeDamageFor(unresolved, controller.Combat.UnresolvedBosses);
                lastOutcome = $"시간 초과 - {unresolved}마리 남음, 라이프 -{damage}";
            }

            ClearViews();
        }

        void Update()
        {
            SyncEnemies();
            SyncPierceTrails();
            SyncHitEffects();
            SyncSplashEffects();
            UpdateLabels();
        }

        void SyncHitEffects()
        {
            CombatContext combat = controller.Combat;

            if (combat != null)
            {
                IReadOnlyList<CombatContext.HitEvent> recent = combat.RecentHits;

                for (int i = 0; i < recent.Count; i++)
                {
                    if (recent[i].Time <= lastHitSeen)
                    {
                        continue;
                    }

                    Spawn(recent[i].Position, recent[i].Source);
                }

                lastHitSeen = combat.ElapsedTime;
            }

            // 풀에서 수명이 지난 히트 이펙트는 끄고
            // 살아 있는 것은 시간에 따라 크기가 커지며 사라지게 함
            for (int i = 0; i < hitPool.Length; i++)
            {
                if (hitPool[i].enabled == false)
                {
                    continue;
                }

                float age = Time.time - hitBornAt[i];

                if (age >= HitSeconds)
                {
                    hitPool[i].enabled = false;
                    continue;
                }

                float t = age / HitSeconds;
                float scale = HitScale * (0.6f + 0.7f * t);
                hitPool[i].transform.localScale = new Vector3(scale, scale, 1f);

                Color c = hitPool[i].color;
                c.a = 1f - t * t;
                hitPool[i].color = c;
            }
        }

        void SyncPierceTrails()
        {
            CombatContext combat = controller.Combat;

            if (combat != null)
            {
                IReadOnlyList<CombatContext.PierceEvent> recent = combat.RecentPierces;

                for (int i = 0; i < recent.Count; i++)
                {
                    if (recent[i].Time <= lastPierceSeen)
                    {
                        continue;
                    }

                    SpawnTrail(recent[i]);
                }

                lastPierceSeen = combat.ElapsedTime;
            }

            for (int i = 0; i < trailPool.Length; i++)
            {
                if (trailPool[i].enabled == false)
                {
                    continue;
                }

                float age = Time.time - trailBornAt[i];

                if (age >= TrailSeconds)
                {
                    trailPool[i].enabled = false;
                    continue;
                }

                float t = age / TrailSeconds;

                Color c = trailPool[i].color;
                c.a = 1f - t * t;
                trailPool[i].color = c;
            }
        }

        // 발사 위치에서 목표 지점까지 한 줄로 trail을 그림
        void SpawnTrail(CombatContext.PierceEvent pierce)
        {
            Sprite sprite = pierce.Source == null ? null : pierce.Source.PierceTrailEffect;

            if (sprite == null)
            {
                return;
            }

            Vector2 from = pierce.Origin + MuzzleOffsetOf(pierce.Source, pierce.Direction);
            Vector2 delta = pierce.Target - from;

            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // 적 중심을 조금 지나쳐야 꿰뚫은 것처럼 보임
            float length = delta.magnitude + TrailOvershoot;
            Vector2 middle = from + delta.normalized * (length * 0.5f);
            Vector2 spriteSize = sprite.bounds.size;

            int index = trailCursor;
            trailCursor = (trailCursor + 1) % trailPool.Length;

            SpriteRenderer sr = trailPool[index];
            sr.sprite = sprite;

            // 등 뒤로 쏘면 몸에 가려야 함
            sr.sortingOrder = pierce.Direction == AimDirection.Up ? 2 : 8;

            sr.transform.localPosition = new Vector3(middle.x, middle.y, 0f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(length / spriteSize.x, TrailThickness / spriteSize.y, 1f);
            sr.color = Color.white;
            sr.enabled = true;
            trailBornAt[index] = Time.time;
        }

        void SyncSplashEffects()
        {
            CombatContext combat = controller.Combat;

            if (combat != null)
            {
                IReadOnlyList<CombatContext.SplashEvent> recent = combat.RecentSplashes;

                for (int i = 0; i < recent.Count; i++)
                {
                    if (recent[i].Time <= lastSplashSeen)
                    {
                        continue;
                    }

                    SpawnSplash(recent[i]);
                }

                lastSplashSeen = combat.ElapsedTime;
            }

            // Splash 이펙트는 처음 SplashGrowRatio 구간에서 실제 공격 반경까지 커지고
            // 그 뒤로는 크기를 유지한 채 옅어짐
            for (int i = 0; i < splashPool.Length; i++)
            {
                if (splashPool[i].enabled == false)
                {
                    continue;
                }

                float age = Time.time - splashBornAt[i];

                if (age >= SplashSeconds)
                {
                    splashPool[i].enabled = false;
                    continue;
                }

                float t = age / SplashSeconds;
                float growth = Mathf.Min(1f, t / SplashGrowRatio);
                float ratio = SplashStartScale + (1f - SplashStartScale) * growth;
                float scale = splashFullScale[i] * ratio;

                splashPool[i].transform.localScale = new Vector3(scale, scale, 1f);

                Color c = splashPool[i].color;
                c.a = 1f - t * t;
                splashPool[i].color = c;
            }
        }

        // 착탄 지점에 splashRadius 크기의 폭발이 나타나도록 함
        // (폭발 이펙트는 splashPool에서 꺼내옴)
        void SpawnSplash(CombatContext.SplashEvent splash)
        {
            Sprite sprite = splash.Source != null && splash.Source.SplashEffect != null
                ? splash.Source.SplashEffect
                : splashSprite;

            if (sprite == null)
            {
                return;
            }

            int index = splashCursor;
            splashCursor = (splashCursor + 1) % splashPool.Length;

            SpriteRenderer sr = splashPool[index];
            sr.sprite = sprite;
            sr.transform.localPosition = new Vector3(splash.Position.x, splash.Position.y, 0f);

            Vector2 spriteSize = sprite.bounds.size;
            float fullScale = splash.Radius * 2f / spriteSize.x;
            splashFullScale[index] = fullScale;
            sr.transform.localScale = new Vector3(fullScale * SplashStartScale, fullScale * SplashStartScale, 1f);

            sr.color = Color.white;
            sr.enabled = true;
            splashBornAt[index] = Time.time;
        }

        static Vector2 MuzzleOffsetOf(UnitDefinition definition, AimDirection direction)
        {
            Vector2[] muzzles = definition.MuzzlesFor(direction);

            if (muzzles == null || muzzles.Length == 0)
            {
                return Vector2.zero;
            }

            Vector2 muzzle = muzzles[0];
            return direction == AimDirection.Left ? new Vector2(-muzzle.x, muzzle.y) : muzzle;
        }

        void Spawn(Vector2 position, UnitDefinition source)
        {
            int index = hitCursor;
            hitCursor = (hitCursor + 1) % hitPool.Length;

            SpriteRenderer sr = hitPool[index];

            sr.sprite = source != null && source.HitEffect != null ? source.HitEffect : hitSprite;

            sr.transform.localPosition = new Vector3(position.x, position.y, 0f);
            sr.transform.localScale = Vector3.zero;
            sr.color = Color.white;
            sr.enabled = true;
            hitBornAt[index] = Time.time;
        }

        void SyncEnemies()
        {
            CombatContext combat = controller.Combat;

            if (combat == null || controller.IsFighting == false)
            {
                return;
            }

            IReadOnlyList<EnemyInstance> enemies = combat.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance enemy = enemies[i];

                if (views.TryGetValue(enemy, out EnemyView view) == false)
                {
                    view = CreateView(enemy);
                    views.Add(enemy, view);
                }

                Vector2 local = enemy.Position;
                view.Root.localPosition = new Vector3(local.x, local.y, 0f);

                float ratio = Mathf.Clamp01(enemy.Hp / enemy.Definition.MaxHp);
                SetBar(view, ratio);
            }

            // 죽어서 목록에서 빠진 적의 스프라이트를 지운다
            gone.Clear();

            foreach (KeyValuePair<EnemyInstance, EnemyView> pair in views)
            {
                if (pair.Key.IsAlive == false || Contains(enemies, pair.Key) == false)
                {
                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; i < gone.Count; i++)
            {
                Destroy(views[gone[i]].Root.gameObject);
                views.Remove(gone[i]);
            }
        }

        static bool Contains(IReadOnlyList<EnemyInstance> list, EnemyInstance target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], target))
                {
                    return true;
                }
            }

            return false;
        }
        
        // 적 체력 바 컨트롤
        static void SetBar(EnemyView view, float ratio)
        {
            bool damaged = ratio < 1f;

            view.BarBack.enabled = damaged;
            view.BarFill.enabled = damaged;

            if (damaged == false)
            {
                return;
            }

            // Square 스프라이트는 pivot이 가운데라 왼쪽 끝을 고정하려면 위치를 함께 밀어야 함
            view.BarFill.transform.localScale = new Vector3(BarWidth * ratio, BarHeight, 1f);
            view.BarFill.transform.localPosition = new Vector3(-BarWidth * (1f - ratio) * 0.5f, BarOffsetY, 0f);
        }

        // Bar도 같은 Square 스프라이트를 늘려서 쓴다
        EnemyView CreateView(EnemyInstance enemy)
        {
            var root = new GameObject("Enemy_" + enemy.Definition.Id);
            root.transform.SetParent(boardRoot, false);

            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(BodyScale, BodyScale, 1f);
            body.sprite = enemySprite;
            body.color = enemy.Definition.PlaceholderColor;
            body.sortingOrder = 5;

            var back = new GameObject("BarBack").AddComponent<SpriteRenderer>();
            back.transform.SetParent(root.transform, false);
            back.transform.localScale = new Vector3(BarWidth + BarBorder, BarHeight + BarBorder, 1f);
            back.transform.localPosition = new Vector3(0f, BarOffsetY, 0f);
            back.sprite = enemySprite;
            back.color = BarBackColor;
            back.sortingOrder = 6;

            var fill = new GameObject("BarFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(root.transform, false);
            fill.sprite = enemySprite;
            fill.color = BarFillColor;
            fill.sortingOrder = 7;

            var view = new EnemyView { Root = root.transform, BarBack = back, BarFill = fill };
            SetBar(view, 1f);
            return view;
        }

        void ClearViews()
        {
            foreach (KeyValuePair<EnemyInstance, EnemyView> pair in views)
            {
                Destroy(pair.Value.Root.gameObject);
            }

            views.Clear();
        }

        void UpdateLabels()
        {
            StageContext stage = controller.Stage;
            lifeLabel.text = stage.Jokers > 0
                ? $"라이프 {stage.Life}   Chip {stage.Chip}   조커 {stage.Jokers}"
                : $"라이프 {stage.Life}   Chip {stage.Chip}";

            if (stage.IsGameOver)
            {
                waveLabel.text = "게임 오버";
            }
            else if (stage.IsAllWavesCleared)
            {
                waveLabel.text = "스테이지 클리어";
            }
            else
            {
                waveLabel.text = $"라운드 {stage.CurrentWave.WaveNumber}";
            }

            startButton.interactable = controller.CanStart;
            startLabel.text = controller.IsFighting ? "전투 중..." : "전투 시작";

            if (controller.IsFighting)
            {
                CombatContext combat = controller.Combat;
                float left = Mathf.Max(0f, combat.Wave.TimeLimit - combat.ElapsedTime);
                combatLabel.text = $"{left:0.0}초  |  {DescribeEnemies(combat)}";
                return;
            }

            combatLabel.text = lastOutcome;
        }

        // 남은 적 표기
        static string DescribeEnemies(CombatContext combat)
        {
            int alive = combat.RemainingEnemies;
            int pending = combat.UnresolvedEnemies - alive;

            if (pending <= 0)
            {
                return $"적 {alive}";
            }

            if (alive == 0)
            {
                return $"다음 적까지 {combat.SecondsToNextSpawn:0.0}초  (남은 {pending})";
            }

            return $"적 {alive}  (남은 {pending})";
        }
    }
}
