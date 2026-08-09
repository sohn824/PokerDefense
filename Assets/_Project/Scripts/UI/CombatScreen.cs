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
     * HP는 별도 게이지 없이 색을 어둡게 해서 보여준다 - 플레이스홀더다
     */
    public sealed class CombatScreen : MonoBehaviour
    {
        [SerializeField] CombatController controller;
        [SerializeField] Transform boardRoot;
        [SerializeField] Sprite enemySprite;
        [SerializeField] TMP_Text lifeLabel;
        [SerializeField] TMP_Text waveLabel;
        [SerializeField] TMP_Text combatLabel;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startLabel;

        readonly Dictionary<EnemyInstance, SpriteRenderer> views = new Dictionary<EnemyInstance, SpriteRenderer>();
        readonly List<EnemyInstance> gone = new List<EnemyInstance>();

        string lastOutcome = string.Empty;

        void Awake()
        {
            startButton.onClick.AddListener(controller.StartCombat);
            controller.CombatStarted += OnCombatStarted;
            controller.CombatFinished += OnCombatFinished;
        }

        void OnCombatStarted(CombatContext combat)
        {
            lastOutcome = string.Empty;
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
            UpdateLabels();
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

                if (views.TryGetValue(enemy, out SpriteRenderer view) == false)
                {
                    view = CreateView(enemy);
                    views.Add(enemy, view);
                }

                Vector2 local = enemy.Position;
                view.transform.localPosition = new Vector3(local.x, local.y, 0f);

                // 남은 HP 비율만큼 밝기를 준다
                float ratio = Mathf.Clamp01(enemy.Hp / enemy.Definition.MaxHp);
                Color full = enemy.Definition.PlaceholderColor;
                view.color = Color.Lerp(full * 0.25f, full, ratio);
            }

            // 죽어서 목록에서 빠진 적의 스프라이트를 지운다
            gone.Clear();

            foreach (KeyValuePair<EnemyInstance, SpriteRenderer> pair in views)
            {
                if (pair.Key.IsAlive == false || Contains(enemies, pair.Key) == false)
                {
                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; i < gone.Count; i++)
            {
                Destroy(views[gone[i]].gameObject);
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

        SpriteRenderer CreateView(EnemyInstance enemy)
        {
            var go = new GameObject("Enemy_" + enemy.Definition.Id);
            go.transform.SetParent(boardRoot, false);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = enemySprite;
            renderer.color = enemy.Definition.PlaceholderColor;
            renderer.sortingOrder = 5;
            return renderer;
        }

        void ClearViews()
        {
            foreach (KeyValuePair<EnemyInstance, SpriteRenderer> pair in views)
            {
                Destroy(pair.Value.gameObject);
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
