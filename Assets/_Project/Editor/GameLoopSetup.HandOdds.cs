using System;
using System.Linq;
using PokerDefense.Game;
using PokerDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.Editor
{
    public static partial class GameLoopSetup
    {
        [MenuItem("Tools/Poker Defense/Update Hand Odds UI")]
        public static void UpdateHandOddsUI()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.SaveOpenScenes();
            UnityEngine.SceneManagement.Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;
            InstallHandOdds();
            MenuScreen menu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
            SetOddsReferences(menu, "inputGroups", new UnityEngine.Object[]
            {
                GameObject.Find("Canvas").GetComponent<CanvasGroup>(),
                GameObject.Find("AssistCanvas").GetComponent<CanvasGroup>(),
                GameObject.Find("GuideCanvas").GetComponent<CanvasGroup>(),
                GameObject.Find("DecisionCanvas").GetComponent<CanvasGroup>(),
                GameObject.Find("OddsCanvas").GetComponent<CanvasGroup>()
            });
            EditorSceneManager.SaveScene(game);
        }

        static void InstallHandOdds()
        {
            GameObject previous = GameObject.Find("OddsCanvas");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }
            RectTransform root = Rect("OddsCanvas", null, Vector2.zero, Vector2.zero);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 175;
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            HandOddsScreen screen = root.gameObject.AddComponent<HandOddsScreen>();
            RectTransform overlay = Rect("Overlay", root, Vector2.zero, Vector2.zero);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = UiStyle.Scrim;
            RectTransform safe = Rect("SafeArea", overlay, Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform page = Page("Odds", safe);
            page.sizeDelta = new Vector2(940f, 1460f);
            Label("Title", page, "교체 확률", new Vector2(0f, 620f), new Vector2(840f, 90f), 56f);
            Set(screen, "heading", Label("Heading", page, "", new Vector2(0f, 535f), new Vector2(840f, 56f), 34f));
            Set(screen, "description", Label("Description", page, "", new Vector2(0f, 420f), new Vector2(840f, 150f), 28f));
            RectTransform viewport = Rect("Viewport", page, new Vector2(0f, -65f), new Vector2(840f, 770f));
            viewport.gameObject.AddComponent<Image>().color = UiStyle.Plate;
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            RectTransform content = Rect("Content", viewport, Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            TMP_Text[] rows = new TMP_Text[13];
            RectTransform[] bars = new RectTransform[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform row = Rect("Row" + i, content, new Vector2(0f, -56f - i * 112f), new Vector2(800f, 112f));
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                rows[i] = Label("Label", row, "", new Vector2(0f, 8f), new Vector2(800f, 88f), 30f);
                rows[i].alignment = TextAlignmentOptions.MidlineLeft;
                RectTransform track = Rect("Track", row, new Vector2(0f, -43f), new Vector2(800f, 6f));
                track.gameObject.AddComponent<Image>().color = UiStyle.BorderMuted;
                bars[i] = Rect("Fill", track, Vector2.zero, Vector2.zero);
                Stretch(bars[i]);
                bars[i].gameObject.AddComponent<Image>().color = UiStyle.BorderPrimary;
            }
            SetOddsReferences(screen, "rows", rows);
            SetOddsReferences(screen, "bars", bars);
            Set(screen, "scroll", scroll);
            Set(screen, "panel", overlay.gameObject);
            Set(screen, "round", UnityEngine.Object.FindAnyObjectByType<RoundController>());
            Set(screen, "closeButton", Button("Close", page, "돌아가기", -620f, UiStyle.ButtonSecondary));
            Label("ScrollHint", page, "위아래로 스크롤 · 경우의 수 / 전체 조합", new Vector2(0f, -500f), new Vector2(840f, 50f), 26f);
            SetOddsReferences(screen, "inputGroups", new UnityEngine.Object[]
            {
                GameObject.Find("Canvas").GetComponent<CanvasGroup>(),
                GameObject.Find("AssistCanvas").GetComponent<CanvasGroup>()
            });
            RoundScreen roundScreen = UnityEngine.Object.FindAnyObjectByType<RoundScreen>();
            RectTransform goals = (RectTransform)roundScreen.transform.Find("HandGoals");
            for (int i = goals.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(goals.GetChild(i).gameObject);
            }
            InstallOddsSummary(roundScreen, goals, "goalLabel", screen);
            AssistScreen assist = UnityEngine.Object.FindAnyObjectByType<AssistScreen>();
            SerializedObject assistObject = new SerializedObject(assist);
            TMP_Text preview = (TMP_Text)assistObject.FindProperty("preview").objectReferenceValue;
            preview.rectTransform.anchoredPosition = new Vector2(0f, -200f);
            preview.rectTransform.sizeDelta = new Vector2(850f, 125f);
            Transform oldSummary = preview.transform.parent.Find("OddsSummary");
            if (oldSummary != null)
            {
                UnityEngine.Object.DestroyImmediate(oldSummary.gameObject);
            }
            RectTransform summary = Rect("OddsSummary", preview.transform.parent, new Vector2(0f, -315f), new Vector2(880f, 104f));
            summary.gameObject.AddComponent<Image>().color = UiStyle.Plate;
            InstallOddsSummary(assist, summary, "oddsLabel", screen);
            foreach (string field in new[] { "revealButton", "chooseButton" })
            {
                Button button = (Button)assistObject.FindProperty(field).objectReferenceValue;
                ((RectTransform)button.transform).anchoredPosition = new Vector2(0f, -445f);
            }
            overlay.gameObject.SetActive(false);
        }

        static void InstallOddsSummary(UnityEngine.Object owner, RectTransform panel, string labelField, HandOddsScreen screen)
        {
            Button button = panel.GetComponent<Button>();
            if (button == null)
            {
                button = panel.gameObject.AddComponent<Button>();
            }
            button.targetGraphic = panel.GetComponent<Graphic>();
            // 장식용 패널을 클릭 영역으로도 쓰므로 입력 판정을 명시적으로 켠다.
            button.targetGraphic.raycastTarget = true;
            TMP_Text label = Label("Summary", panel, "", new Vector2(-85f, 0f), new Vector2(panel.sizeDelta.x - 210f, 100f), 28f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text details = Label("Details", panel, "자세히 >", new Vector2(panel.sizeDelta.x * 0.5f - 100f, 0f), new Vector2(170f, 100f), 30f);
            details.color = UiStyle.TextMuted;
            Set(owner, labelField, label);
            Set(owner, "oddsButton", button);
            Set(owner, "oddsScreen", screen);
        }

        static void SetOddsReferences(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
