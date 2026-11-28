using System.Collections;
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
     * 전투 상황을 화면과 소리로 나타내는 클래스
     * CombatController.LatestCombatContext를 매 프레임 읽어 보여주는 역할
     *
     * 보여주는 것
     * 적 - 적 목록에 맞춰 몸통·체력 바·보스 Aura를 만들고 지움
     * 이펙트 - 타격·관통 궤적·Splash 폭발을 Pool에서 꺼내 그림
     * 효과음 - 발사·착탄·처치음
     * 라벨 - 라이프·승급권·라운드·남은 시간·적 수·전투 결과
     * 연출 - 라이프 깎이는 연출, 적 경로 Flow 연출
     */
    public sealed class CombatScreen : MonoBehaviour
    {
        /**
         * 적 하나의 표현 (몸통·체력 바·보스 Aura 등)
         */
        sealed class EnemyView
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer BarBack;
            public SpriteRenderer BarFill;

            // 보스 발밑 Aura를 그릴 렌더러 (실제로 Aura를 그리는건 머티리얼에 붙은 shader고 sprite는 도화지 역할)
            public SpriteRenderer Aura;

            // 몸통 기본 크기 (피격 때 커졌다가 이 크기로 돌아옴)
            public float BaseScale;

            // 지난 프레임 체력 (이보다 줄었으면 맞은 것으로 봄)
            public float LastHp;

            // 마지막으로 맞은 시각
            public float HitAt = float.NegativeInfinity;

            // 죽어서 Dissolve 연출을 시작한 시각 (RemoveView가 기록)
            // SyncDying이 이 시각부터 DissolveSeconds(0.35초) 동안 진행도를 0 -> 1로 올림
            public float DyingAt;
        }

        const float BodyScale = 0.5f;

        // 피격 연출 - 0.1초 동안 하얗게 번쩍이며 1.12배로 커졌다가 돌아옴
        const float FlashSeconds = 0.1f;
        const float FlashPopScale = 1.12f;

        // 처치 연출 - 0.35초 동안 가장자리가 타들어 가며 사라짐
        const float DissolveSeconds = 0.35f;

        // EnemySprite 셰이더 속성 ID (매번 이름으로 찾지 않도록 미리 ID를 캐시)
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        // 타격 이펙트 풀 크기 (다 차면 가장 오래된 것부터 다시 씀)
        const int HitPoolSize = 24;

        const float HitSeconds = 0.12f;
        const float HitScale = 0.55f;

        // Pierce 관통 궤적 풀 크기
        const int TrailPoolSize = 8;
        const float TrailSeconds = 0.18f;
        const float TrailThickness = 0.84f;

        // 궤적이 겨눈 적 너머로 더 뻗는 최소 길이
        const float MinTrailOvershoot = 0.3f;

        // Splash 폭발 풀 크기
        const int SplashPoolSize = 12;
        const float SplashSeconds = 0.20f;

        // Splash 이펙트 연출 상수
        const float SplashGrowRatio = 0.25f;
        const float SplashStartScale = 0.6f;

        const float BarWidth = 0.5f;
        const float BarHeight = 0.07f;
        const float BarBorder = 0.04f;
        const float BarOffsetY = 0.36f;

        static readonly Color BarBackColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        static readonly Color BarFillColor = new Color(0.40f, 0.85f, 0.35f);

        // 시간 초과로 라이프가 깎일 때 연출
        const float FailFlashSeconds = 0.45f;
        const float FailFlashPeakAlpha = 0.4f;
        const float LifePopSeconds = 0.35f;
        const float LifePopScale = 1.4f;

        // 같은 전투음의 재생 최소 간격
        const float CombatSfxMinGap = 0.05f;

        // 적 경로 Flow 연출용 상수
        const int FlowSegments = 160;
        const float FlowWidth = 0.45f;
        static readonly Color FlowColor = new Color(1f, 0.85f, 0.5f, 0.45f);

        // 보스 Aura 연출용 상수
        // AuraWidth - 몸통 크기에 곱하는 가로 배수
        // AuraFlatten - 바닥에 누운 원으로 보이게 세로를 줄이는 비율
        const float AuraWidth = 1.4f;
        const float AuraFlatten = 0.5f;
        const float AuraAlpha = 0.7f;

        // 보스 등장 Warning 연출용 상수
        const float WarningSeconds = 1.2f;

        // BossWarning 셰이더 속성 ID
        static readonly int WarningProgressId = Shader.PropertyToID("_Progress");
        static readonly int WarningCenterId = Shader.PropertyToID("_Center");
        static readonly int WarningAspectId = Shader.PropertyToID("_Aspect");

        // LifeVignette 셰이더 속성 ID
        static readonly int VignetteIntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] CombatController controller;
        [SerializeField] Transform boardRoot;
        [SerializeField] Sprite enemySprite;

        [Tooltip("적 몸통에 쓸 머티리얼 (EnemySprite.mat)\nEnemySprite 셰이더용")]
        [SerializeField] Material enemyMaterial;

        [Tooltip("적 경로 표시선에 쓸 머티리얼 (TrackFlow.mat)\nTrackFlow 셰이더용")]
        [SerializeField] Material trackFlowMaterial;

        [Tooltip("보스 발밑 표시에 쓸 머티리얼 (BossAura.mat)\nBossAura 셰이더용")]
        [SerializeField] Material bossAuraMaterial;

        [Tooltip("보스 등장 화면 연출에 쓸 머티리얼 (BossWarning.mat)\nBossWarning 셰이더용")]
        [SerializeField] Material bossWarningMaterial;

        [Tooltip("라이프 위험 화면 연출에 쓸 머티리얼 (LifeVignette.mat)\nLifeVignette 셰이더용")]
        [SerializeField] Material lifeVignetteMaterial;

        [Tooltip("타격 이펙트")]
        [SerializeField] Sprite hitSprite;

        [Tooltip("Splash 공용 폭발 이펙트")]
        [SerializeField] Sprite splashSprite;

        [Tooltip("시간 초과로 라이프가 깎일 때 붉게 번쩍이는 전체 화면 이미지")]
        [SerializeField] Image failFlash;
        [SerializeField] TMP_Text lifeLabel;
        [SerializeField] TMP_Text jokerLabel;
        [SerializeField] TMP_Text waveLabel;
        [SerializeField] TMP_Text combatLabel;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startLabel;

        readonly Dictionary<EnemyInstance, EnemyView> views = new Dictionary<EnemyInstance, EnemyView>();
        readonly List<EnemyInstance> gone = new List<EnemyInstance>();

        // Dissolve 연출 중인 죽은 적
        // 전투가 끝나 views가 지워져도 연출이 끝까지 재생되도록 따로 둠
        readonly List<EnemyView> dying = new List<EnemyView>();

        // 적마다 다른 Flash, Dissolve 값을 넣기 위한 블록
        // (머티리얼 자체는 모든 적이 하나를 같이 쓰고, 적마다 복제하지 않음)
        MaterialPropertyBlock enemyBlock;

        // 적 경로 표시선
        LineRenderer trackFlow;

        SpriteRenderer[] hitPool;
        float[] hitBornAt;
        int hitCursor;

        SpriteRenderer[] trailPool;
        float[] trailBornAt;
        int trailCursor;

        SpriteRenderer[] splashPool;
        float[] splashBornAt;

        // Splash 이펙트의 최대 scale
        float[] splashFullScale;
        int splashCursor;

        float lastHitSeen;
        float lastPierceSeen;
        float lastSplashSeen;

        string lastOutcome = string.Empty;

        Coroutine failFlashRoutine;
        Coroutine lifePopRoutine;

        // 보스 등장 Warning 연출을 그리는 전체 화면 이미지 (머티리얼이 없으면 null)
        Image bossWarning;
        Coroutine bossWarningRoutine;

        // 라이프 위험 Vignette 연출을 그리는 전체 화면 이미지 (머티리얼이 없으면 null)
        Image lifeVignette;

        // 마지막으로 Vignette 세기를 넣은 라이프 (같으면 다시 넣지 않음)
        int shownVignetteLife = -1;

        // 마지막으로 착탄음 / 처치음을 낸 시각 (CombatSfxMinGap 간격 확인용)
        float lastImpactSfxAt = -1f;
        float lastDeathSfxAt = -1f;

        void Awake()
        {
            startButton.onClick.AddListener(controller.StartCombat);
            controller.CombatStarted += OnCombatStarted;
            controller.CombatFinished += OnCombatFinished;
            BuildHitPool();
            BuildTrailPool();
            BuildSplashPool();
            BuildTrackFlow();
            BuildBossWarning();
            BuildLifeVignette();
            enemyBlock = new MaterialPropertyBlock();
        }

        // 보스 등장 Warning 연출용 이미지를 만들어 둠
        // 머티리얼이 없으면 만들지 않음 (Warning 연출 없음)
        void BuildBossWarning()
        {
            if (bossWarningMaterial != null)
            {
                bossWarning = BuildScreenOverlay("BossWarning", bossWarningMaterial);
            }
        }

        // 라이프 위험 Vignette 연출용 이미지를 만들어 둠
        // 머티리얼이 없으면 만들지 않음 (Vignette 연출 없음)
        void BuildLifeVignette()
        {
            if (lifeVignetteMaterial != null)
            {
                lifeVignette = BuildScreenOverlay("LifeVignette", lifeVignetteMaterial);
            }
        }

        // failFlash와 같은 자리에 화면 전체를 덮는 꺼진 이미지를 만듦
        // 실행 중 값을 바꾸므로 머티리얼 에셋 대신 복제본을 씀
        Image BuildScreenOverlay(string name, Material source)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(failFlash.transform.parent, false);
            rect.SetSiblingIndex(failFlash.transform.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            image.material = new Material(source);
            return image;
        }

        // 라이프가 한 웨이브 최대 손실(MaxLifeDamagePerWave) 이하면 Vignette를 켬
        // 라이프가 낮을수록 진해지고, 0(게임 오버)이면 끔
        void SyncLifeVignette()
        {
            if (lifeVignette == null)
            {
                return;
            }

            int life = controller.Stage.Life;
            int danger = controller.Stage.MaxLifeDamagePerWave;
            bool show = life > 0 && life <= danger;

            // 값이 바뀔 때만 넣어 매 프레임 Canvas를 다시 그리지 않게 함
            if (lifeVignette.enabled != show)
            {
                lifeVignette.enabled = show;
            }

            // 세기 = (danger + 1 - life) / danger (기준 라이프에서 1 / danger, 라이프 1에서 1)
            if (show && life != shownVignetteLife)
            {
                lifeVignette.material.SetFloat(VignetteIntensityId, (danger + 1 - life) / (float)danger);
                shownVignetteLife = life;
            }
        }

        // 트랙을 한 바퀴 도는 선을 만들어 둠 (무늬와 움직임은 TrackFlow 셰이더가 그림)
        void BuildTrackFlow()
        {
            // 머티리얼이 없으면 만들지 않음 (Flow 연출 없음)
            if (trackFlowMaterial == null)
            {
                return;
            }

            GameObject go = new GameObject("TrackFlow");
            go.transform.SetParent(boardRoot, false);

            trackFlow = go.AddComponent<LineRenderer>();
            trackFlow.useWorldSpace = false;
            trackFlow.loop = true;
            trackFlow.alignment = LineAlignment.View;
            trackFlow.textureMode = LineTextureMode.Stretch;
            trackFlow.sharedMaterial = trackFlowMaterial;
            trackFlow.widthMultiplier = FlowWidth;
            trackFlow.startColor = FlowColor;
            trackFlow.endColor = FlowColor;

            // 트랙 그림(-10)보다 위, 유닛·적보다 아래
            trackFlow.sortingOrder = -5;
            trackFlow.positionCount = FlowSegments;

            for (int i = 0; i < FlowSegments; i++)
            {
                trackFlow.SetPosition(i, GridBoard.TrackPosition(i / (float)FlowSegments));
            }
        }

        // 전투 중이 아닐 때만 적 경로를 보여줌 (전투 중에는 적과 이펙트를 가림)
        void SyncTrackFlow()
        {
            if (trackFlow != null)
            {
                trackFlow.enabled = controller.IsFighting == false;
            }
        }

        void BuildSplashPool()
        {
            splashPool = new SpriteRenderer[SplashPoolSize];
            splashBornAt = new float[SplashPoolSize];
            splashFullScale = new float[SplashPoolSize];

            for (int i = 0; i < SplashPoolSize; i++)
            {
                GameObject gameObject = new GameObject("SplashEffect");
                gameObject.transform.SetParent(boardRoot, false);

                SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
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
                GameObject gameObject = new GameObject("PierceTrail");
                gameObject.transform.SetParent(boardRoot, false);

                SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
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
                GameObject go = new GameObject("HitEffect");
                go.transform.SetParent(boardRoot, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
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

            // 새 전투는 ElapsedTime이 0부터 다시 흐르므로 마지막으로 본 기록 시각을 되돌림
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

            if (bossWarning != null && HasBoss(combat.Wave))
            {
                RestartCoroutine(ref bossWarningRoutine, PlayBossWarning());
            }
        }

        // 이 웨이브에 보스가 나오는지
        static bool HasBoss(WaveDefinition wave)
        {
            foreach (WaveDefinition.SpawnEntry entry in wave.Entries)
            {
                if (entry.count > 0 && entry.enemy != null && entry.enemy.Type == EnemyType.Boss)
                {
                    return true;
                }
            }

            return false;
        }

        // 보스 등장 Warning 연출용 코루틴
        IEnumerator PlayBossWarning()
        {
            Material material = bossWarning.material;
            RectTransform rect = bossWarning.rectTransform;

            // 고리 중심 - 적이 나오는 트랙 시작점을 이미지 안의 0~1 좌표로 바꿈
            Vector3 spawn = boardRoot.TransformPoint(GridBoard.TrackPosition(0f));
            Vector2 screen = Camera.main.WorldToScreenPoint(spawn);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local);

            material.SetVector(WarningCenterId, Rect.PointToNormalized(rect.rect, local));
            material.SetFloat(WarningAspectId, rect.rect.width / rect.rect.height);
            bossWarning.enabled = true;

            for (float t = 0f; t < WarningSeconds; t += Time.deltaTime)
            {
                material.SetFloat(WarningProgressId, t / WarningSeconds);
                yield return null;
            }

            bossWarning.enabled = false;
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            if (outcome == CombatOutcome.Cleared)
            {
                lastOutcome = controller.LatestCombatContext.Wave.JokerReward > 0
                    ? "웨이브 클리어 - 승급권 획득"
                    : "웨이브 클리어";
            }
            else
            {
                int damage = controller.Stage.LifeDamageFor(unresolved, controller.LatestCombatContext.UnresolvedBosses);
                lastOutcome = $"시간 초과 - {unresolved}마리 남음, 라이프 -{damage}";

                if (damage > 0)
                {
                    RestartCoroutine(ref failFlashRoutine, PlayFailFlash());
                    RestartCoroutine(ref lifePopRoutine, PlayLifePop());
                }
            }

            ClearViews();
        }

        void RestartCoroutine(ref Coroutine routine, IEnumerator body)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(body);
        }

        // 시간 초과 실패 빨간색 화면 연출용 코루틴
        IEnumerator PlayFailFlash()
        {
            Color color = failFlash.color;

            for (float t = 0f; t < FailFlashSeconds; t += Time.deltaTime)
            {
                color.a = (1f - t / FailFlashSeconds) * FailFlashPeakAlpha;
                failFlash.color = color;
                yield return null;
            }

            color.a = 0f;
            failFlash.color = color;
        }

        // 라이프 감소 연출용 코루틴
        IEnumerator PlayLifePop()
        {
            Transform label = lifeLabel.transform;

            for (float t = 0f; t < LifePopSeconds; t += Time.deltaTime)
            {
                float ratio = t / LifePopSeconds;
                float scale = 1f + (LifePopScale - 1f) * Mathf.Sin(ratio * Mathf.PI);
                label.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            label.localScale = Vector3.one;
        }

        void Update()
        {
            SyncEnemies();
            SyncDying();
            SyncPierceTrails();
            SyncHitEffects();
            SyncSplashEffects();
            SyncTrackFlow();
            SyncLifeVignette();
            UpdateLabels();
        }

        void SyncHitEffects()
        {
            CombatContext combat = controller.LatestCombatContext;

            if (combat != null)
            {
                IReadOnlyList<CombatContext.HitEvent> recent = combat.RecentHits;
                bool anyNewHit = false;

                for (int i = 0; i < recent.Count; i++)
                {
                    if (recent[i].Time <= lastHitSeen)
                    {
                        continue;
                    }

                    Spawn(recent[i].Position, recent[i].Source);
                    anyNewHit = true;

                    // 발사음과 Splash·Pierce 착탄음은 피격마다 요청 (최소 간격은 AudioManager가 cooldown으로 확인)
                    if (recent[i].Source != null)
                    {
                        AudioManager.Instance?.Play(AudioManager.FireCue(recent[i].Source));

                        AttackPattern pattern = recent[i].Source.Pattern;

                        if (pattern == AttackPattern.Splash)
                        {
                            AudioManager.Instance?.Play(AudioManager.Sfx.ImpactSplash);
                        }
                        else if (pattern == AttackPattern.Pierce)
                        {
                            AudioManager.Instance?.Play(AudioManager.Sfx.ImpactPierce);
                        }
                    }
                }

                if (anyNewHit && Time.time - lastImpactSfxAt >= CombatSfxMinGap)
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.ImpactHit);
                    lastImpactSfxAt = Time.time;
                }

                lastHitSeen = combat.ElapsedTime;
            }

            // 풀에서 수명이 지난 타격 이펙트는 끄고
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
            CombatContext combat = controller.LatestCombatContext;

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

        // 총구에서 겨눈 적을 지나 관통 길이만큼 궤적을 그림
        // 유닛에 궤적 스프라이트가 없으면 그리지 않음
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

            float overshoot = Mathf.Max(MinTrailOvershoot, pierce.Source.PierceLength);
            float length = delta.magnitude + overshoot;
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
            CombatContext combat = controller.LatestCombatContext;

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

        // 궤적 시작점으로 쓸 첫 번째 총구 위치
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

        // 타격 이펙트 하나를 풀에서 꺼내 맞은 위치에 띄움
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
            CombatContext combat = controller.LatestCombatContext;

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

                if (enemy.Definition.HasArt)
                {
                    view.Body.sprite = enemy.Definition.ArtFor(GridBoard.TrackDirection(enemy.Progress));
                }

                float ratio = Mathf.Clamp01(enemy.Hp / enemy.Definition.MaxHp);
                SetBar(view, ratio);
                SyncHitFlash(view, enemy.Hp);
            }

            // 죽어서 목록에서 빠진 적의 표시를 지움
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
                if (gone[i].IsAlive == false && gone[i].Definition.Type == EnemyType.Boss)
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.EnemyDeathBig);
                }

                RemoveView(gone[i]);
            }

            // 이번 프레임에 죽은 적이 있으면 일반 처치음을 한 번만 냄
            if (gone.Count > 0 && Time.time - lastDeathSfxAt >= CombatSfxMinGap)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.EnemyDeath);
                lastDeathSfxAt = Time.time;
            }
        }

        // 체력이 줄었으면 맞은 것으로 보고 Flash 연출을 다시 시작함
        // Flash 세기는 맞은 순간 1 -> FlashSeconds 뒤 0, 몸통 크기도 같은 비율로 돌아옴
        void SyncHitFlash(EnemyView view, float hp)
        {
            if (hp < view.LastHp)
            {
                view.HitAt = Time.time;
            }
            view.LastHp = hp;

            float flash = 1f - Mathf.Clamp01((Time.time - view.HitAt) / FlashSeconds);
            float scale = view.BaseScale * (1f + (FlashPopScale - 1f) * flash);
            view.Body.transform.localScale = new Vector3(scale, scale, 1f);
            SetEnemyBlock(view.Body, flash, 0f);
        }

        // 적 표시를 views에서 뺌
        // 죽은 적은 체력 바·Aura를 숨기고 Dissolve 목록으로 넘김
        // 트랙 끝으로 빠져나간 적은 바로 지움
        void RemoveView(EnemyInstance enemy)
        {
            EnemyView view = views[enemy];
            views.Remove(enemy);

            if (enemy.IsAlive)
            {
                Destroy(view.Root.gameObject);
                return;
            }

            view.BarBack.enabled = false;
            view.BarFill.enabled = false;

            if (view.Aura != null)
            {
                view.Aura.enabled = false;
            }

            view.Body.transform.localScale = new Vector3(view.BaseScale, view.BaseScale, 1f);
            view.DyingAt = Time.time;
            dying.Add(view);
        }

        // Dissolve 진행도를 0 -> 1로 올리고, 다 사라진 적은 지움
        void SyncDying()
        {
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                EnemyView view = dying[i];
                float t = (Time.time - view.DyingAt) / DissolveSeconds;

                if (t >= 1f)
                {
                    Destroy(view.Root.gameObject);
                    dying.RemoveAt(i);
                    continue;
                }

                SetEnemyBlock(view.Body, 0f, t);
            }
        }

        // 적 몸통에 Flash·Dissolve 값을 넣음
        void SetEnemyBlock(SpriteRenderer body, float flash, float dissolve)
        {
            // SetPropertyBlock은 렌더러의 블록을 통째로 바꾸므로
            // 이 렌더러에만 따로 넣어 둔 셰이더 값을 먼저 읽어 와 Flash·Dissolve 값만 바꿔 다시 넣음
            body.GetPropertyBlock(enemyBlock);
            enemyBlock.SetFloat(FlashAmountId, flash);
            enemyBlock.SetFloat(DissolveId, dissolve);
            body.SetPropertyBlock(enemyBlock);
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

        // 체력 바 갱신 (체력이 100%면 숨김)
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

        // 적 하나의 몸통·체력 바 생성
        EnemyView CreateView(EnemyInstance enemy)
        {
            GameObject root = new GameObject("Enemy_" + enemy.Definition.Id);
            root.transform.SetParent(boardRoot, false);

            bool hasArt = enemy.Definition.HasArt;
            float scale = BodyScale * enemy.Definition.VisualScale;

            SpriteRenderer body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(scale, scale, 1f);
            body.sprite = hasArt ? enemy.Definition.ArtFor(GridBoard.TrackDirection(enemy.Progress)) : enemySprite;
            body.color = hasArt ? Color.white : enemy.Definition.PlaceholderColor;
            body.sortingOrder = 5;
            // 머티리얼이 없으면 기본 스프라이트로 그림 (Flash·Dissolve 연출 없음)
            if (enemyMaterial != null)
            {
                body.sharedMaterial = enemyMaterial;
            }

            SpriteRenderer back = new GameObject("BarBack").AddComponent<SpriteRenderer>();
            back.transform.SetParent(root.transform, false);
            back.transform.localScale = new Vector3(BarWidth + BarBorder, BarHeight + BarBorder, 1f);
            back.transform.localPosition = new Vector3(0f, BarOffsetY, 0f);
            back.sprite = enemySprite;
            back.color = BarBackColor;
            back.sortingOrder = 6;

            SpriteRenderer fill = new GameObject("BarFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(root.transform, false);
            fill.sprite = enemySprite;
            fill.color = BarFillColor;
            fill.sortingOrder = 7;

            EnemyView view = new EnemyView
            {
                Root = root.transform, Body = body, BarBack = back, BarFill = fill,
                BaseScale = scale, LastHp = enemy.Hp, Aura = CreateAura(enemy, root.transform, scale)
            };
            SetBar(view, 1f);
            return view;
        }

        // 보스 발밑 Aura 생성
        // 보스가 아니거나 머티리얼이 없으면 Aura를 만들지 않음
        SpriteRenderer CreateAura(EnemyInstance enemy, Transform root, float bodyScale)
        {
            if (enemy.Definition.Type != EnemyType.Boss || bossAuraMaterial == null)
            {
                return null;
            }

            SpriteRenderer aura = new GameObject("Aura").AddComponent<SpriteRenderer>();
            aura.transform.SetParent(root, false);
            aura.transform.localScale = new Vector3(bodyScale * AuraWidth, bodyScale * AuraWidth * AuraFlatten, 1f);
            aura.sprite = enemySprite;
            aura.sharedMaterial = bossAuraMaterial;

            Color color = enemy.Definition.PlaceholderColor;
            color.a = AuraAlpha;
            aura.color = color;

            // 적 경로 표시선(-5)보다 위, 유닛·적보다 아래
            aura.sortingOrder = -4;
            return aura;
        }

        // 전투가 끝나면 남은 적 표시를 지움
        // 마지막 적은 이 프레임에 막 죽었을 수 있어 바로 지우지 않고 RemoveView를 호출해 Dissolve 연출을 남김
        void ClearViews()
        {
            gone.Clear();
            gone.AddRange(views.Keys);

            for (int i = 0; i < gone.Count; i++)
            {
                RemoveView(gone[i]);
            }
        }

        void UpdateLabels()
        {
            StageContext stage = controller.Stage;
            lifeLabel.text = $"<color=#FF6B6E>♥</color> {stage.Life}";

            bool hasJoker = stage.Jokers > 0;
            jokerLabel.gameObject.SetActive(hasJoker);

            if (hasJoker)
            {
                jokerLabel.text = $"<color=#C9A3FF>★</color> {stage.Jokers}";
            }

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
                waveLabel.text = $"라운드 {stage.CurrentWave.WaveNumber}/{stage.TotalWaves}";
            }

            startButton.interactable = controller.CanStart;
            startLabel.text = controller.IsFighting ? "전투 중..." : "전투 시작";

            if (controller.IsFighting)
            {
                CombatContext combat = controller.LatestCombatContext;
                float left = Mathf.Max(0f, combat.Wave.TimeLimit - combat.ElapsedTime);
                combatLabel.text = $"{left:0.0}초  |  {DescribeEnemies(combat)}";
                return;
            }

            combatLabel.text = lastOutcome;
        }

        // 전투 중 적 수 표기 (트랙 위 적, 아직 나오지 않은 적)
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
                return $"다음 적까지 {combat.SecondsToNextSpawn:0.0}초  남은 적 {pending}";
            }

            return $"적 {alive}  남은 적 {pending}";
        }
    }
}
