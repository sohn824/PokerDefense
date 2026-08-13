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
     * 전투 표현. 적 스프라이트를 CombatContext의 적 목록에 맞춰 만들고 지운다
     * 적 좌표는 보드 로컬이라 boardRoot 아래에 붙인다
     * 남은 HP는 체력 바 길이로 보여준다 (DESIGN §10.5)
     */
    public sealed class CombatScreen : MonoBehaviour
    {
        /**
         * 적 하나의 표현 - 몸통과 체력 바
         *
         * 루트는 스케일 1로 두고 몸통만 줄인다. 그래야 바 수치를 월드 단위 그대로 쓸 수 있다
         */
        sealed class EnemyView
        {
            public Transform Root;
            public SpriteRenderer BarBack;
            public SpriteRenderer BarFill;
        }

        // 적은 슬롯의 절반 크기다 (화면 73px)
        const float BodyScale = 0.5f;

        /**
         * 타격 이펙트는 고정 풀로 돌린다
         *
         * Splash 한 발이 여러 기를 때리므로 매번 만들면 초당 수백 개가 된다.
         * 풀을 다 쓰면 가장 오래된 것부터 덮어쓴다 - 어차피 짧게 사라진다
         */
        const int HitPoolSize = 24;

        // 이펙트는 맞은 지점에 남고 적은 계속 움직인다. 길게 두면 Runner(속도 4.2)가
        // 한 몸 길이만큼 앞서 나가 이펙트가 떨어져 보인다
        const float HitSeconds = 0.12f;
        const float HitScale = 0.55f;

        const float BarWidth = 0.5f;
        const float BarHeight = 0.07f;
        const float BarBorder = 0.04f;
        const float BarOffsetY = 0.36f;

        static readonly Color BarBackColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        static readonly Color BarFillColor = new Color(0.40f, 0.85f, 0.35f);

        [SerializeField] CombatController controller;
        [SerializeField] Transform boardRoot;
        [SerializeField] Sprite enemySprite;

        [Tooltip("타격 이펙트. 방사 대칭이라 회전 없이 쓴다")]
        [SerializeField] Sprite hitSprite;
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

        // 이 시각보다 나중에 생긴 피격만 새로 띄운다. 프레임이 아니라 시간으로 재야
        // CombatController와 CombatScreen의 실행 순서에 안 걸린다
        float lastHitSeen;

        string lastOutcome = string.Empty;

        void Awake()
        {
            startButton.onClick.AddListener(controller.StartCombat);
            controller.CombatStarted += OnCombatStarted;
            controller.CombatFinished += OnCombatFinished;
            BuildHitPool();
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
                sr.sortingOrder = 9;   // 적(5)·체력 바(6,7)보다 위
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

            for (int i = 0; i < hitPool.Length; i++)
            {
                hitPool[i].enabled = false;
            }
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            if (outcome == CombatOutcome.Cleared)
            {
                // 보스를 잡아야만 조커가 나오므로 획득 사실을 짚어 준다 (DESIGN §5.2.1)
                lastOutcome = controller.Combat.Wave.JokerReward > 0
                    ? "웨이브 클리어 - 조커 획득"
                    : "웨이브 클리어";
            }
            else
            {
                // 라이프 피해는 잔여 적 수와 다를 수 있다 - 상한이 있고 보스는 따로 친다 (DESIGN §5.4)
                int damage = controller.Stage.LifeDamageFor(unresolved, controller.Combat.UnresolvedBosses);
                lastOutcome = $"시간 초과 - {unresolved}마리 남음, 라이프 -{damage}";
            }

            ClearViews();
        }

        void Update()
        {
            SyncEnemies();
            SyncHitEffects();
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

                    Spawn(recent[i].Position);
                }

                lastHitSeen = combat.ElapsedTime;
            }

            // 수명이 지난 것을 끄고 살아 있는 것은 커지며 사라지게 한다
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

        void Spawn(Vector2 position)
        {
            int index = hitCursor;
            hitCursor = (hitCursor + 1) % hitPool.Length;

            SpriteRenderer sr = hitPool[index];
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

        // 성한 적까지 바를 띄우면 잔챙이 웨이브에서 화면이 뒤덮인다 - 한 웨이브에 최대 68기다
        static void SetBar(EnemyView view, float ratio)
        {
            bool damaged = ratio < 1f;

            view.BarBack.enabled = damaged;
            view.BarFill.enabled = damaged;

            if (damaged == false)
            {
                return;
            }

            // Square 스프라이트는 피벗이 가운데라 왼쪽 끝을 고정하려면 위치를 함께 밀어야 한다
            view.BarFill.transform.localScale = new Vector3(BarWidth * ratio, BarHeight, 1f);
            view.BarFill.transform.localPosition = new Vector3(-BarWidth * (1f - ratio) * 0.5f, BarOffsetY, 0f);
        }

        // 바도 같은 사각 스프라이트를 늘려서 쓴다 - 별도 에셋을 만들지 않는다
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

            // 조커는 보스에서만 나오므로 들고 있을 때만 자리를 차지한다
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
                // 라운드 N이 웨이브 N을 치른다. 둘은 1:1이라 라운드만 보여준다
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

        /**
         * 남은 적 표기
         *
         * 웨이브는 스폰 스케줄이라, 화면의 적을 다 잡아도 아직 안 나온 적이 남아 있을 수 있다
         * 그때 "적 0"만 띄우면 게임이 멈춘 것처럼 보이므로 무엇을 기다리는지 대신 보여준다
         */
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
