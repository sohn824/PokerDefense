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
            lastOutcome = outcome == CombatOutcome.Cleared
                ? "웨이브 클리어"
                : $"시간 초과 - {unresolved}마리 남음, 라이프 -{unresolved}";

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

            lifeLabel.text = $"라이프 {stage.Life}   Chip {stage.Chip}";

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
                combatLabel.text = $"{left:0.0}초  |  적 {combat.RemainingEnemies}";
                return;
            }

            combatLabel.text = lastOutcome;
        }
    }
}
