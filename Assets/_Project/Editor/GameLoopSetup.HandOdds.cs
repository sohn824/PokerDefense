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
        // 교체 확률 UI만 다시 설치하는 메뉴
        [MenuItem("Tools/Poker Defense/Update Hand Odds UI")]
        public static void UpdateHandOddsUI()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.SaveOpenScenes();
            UnityEngine.SceneManagement.Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");
            // 새로 만드는 글자에 씬에서 쓰는 폰트를 그대로 씀
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;
            InstallHandOdds();

            // OddsCanvas를 새로 만들어 끊어진 메뉴의 입력 차단 목록을 다시 연결함
            SetArray(UnityEngine.Object.FindAnyObjectByType<MenuScreen>(), "inputGroups", MenuInputGroups());
            EditorSceneManager.SaveScene(game);
        }

        // InstallHandOdds
        // 교체 확률 상세 창과, 누르면 그 창이 열리는 요약 줄 두 개를 설치
        // (기존 것은 지우고 새로 만듦)
        static void InstallHandOdds()
        {
            HandOddsScreen screen = InstallOddsScreen();
            InstallExchangeSummary(screen);
            InstallAssistSummary(screen);
        }

        // 확률 상세 창을 새로 만들어 숨겨 두고, 요약 줄을 누르면 창을 열 수 있도록 창을 여닫는 컴포넌트를 반환
        static HandOddsScreen InstallOddsScreen()
        {
            GameObject previous = GameObject.Find("OddsCanvas");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }

            // 캔버스 - 안내창(150)보다 위, 메뉴(200)보다 아래
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

            // 어두운 막 -> SafeArea -> 창 순으로 쌓음
            RectTransform overlay = Rect("Overlay", root, Vector2.zero, Vector2.zero);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = UiStyle.Scrim;
            RectTransform safe = Rect("SafeArea", overlay, Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform page = Page("Odds", safe);
            page.sizeDelta = new Vector2(940f, 1460f);

            // 제목·현재 손패 문구·설명
            Label("Title", page, "교체 확률", new Vector2(0f, 620f), new Vector2(840f, 90f), 56f);
            Set(screen, "heading", Label("Heading", page, "", new Vector2(0f, 535f), new Vector2(840f, 56f), 34f));
            Set(screen, "description", Label("Description", page, "", new Vector2(0f, 420f), new Vector2(840f, 150f), 28f));

            // 세로로만 움직이는 스크롤 영역
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

            // 족보별 한 줄 - 위에 문구, 아래에 확률 막대(회색 트랙 + 초록 채움)
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

            // HandOddsScreen 연결
            SetArray(screen, "rows", rows);
            SetArray(screen, "bars", bars);
            Set(screen, "scroll", scroll);
            Set(screen, "panel", overlay.gameObject);
            Set(screen, "round", UnityEngine.Object.FindAnyObjectByType<RoundController>());
            Set(screen, "closeButton", Button("Close", page, "돌아가기", -620f, UiStyle.ButtonSecondary));
            Label("ScrollHint", page, "위아래로 스크롤 · 경우의 수 / 전체 조합", new Vector2(0f, -500f), new Vector2(840f, 50f), 26f);
            // 창이 열려 있는 동안 막을 입력
            SetArray(screen, "inputGroups", new UnityEngine.Object[]
            {
                GameObject.Find("Canvas").GetComponent<CanvasGroup>(),
                GameObject.Find("AssistCanvas").GetComponent<CanvasGroup>()
            });

            overlay.gameObject.SetActive(false);
            return screen;
        }

        // 일반 교체 쪽 - ExchangeOdds 패널을 비우고 요약 줄로 다시 채움
        static void InstallExchangeSummary(HandOddsScreen screen)
        {
            RoundScreen roundScreen = UnityEngine.Object.FindAnyObjectByType<RoundScreen>();
            RectTransform goals = (RectTransform)roundScreen.transform.Find("ExchangeOdds");
            for (int i = goals.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(goals.GetChild(i).gameObject);
            }
            InstallOddsSummary(roundScreen, goals, "goalLabel", screen);
        }

        // 선택 교체 쪽 - 미리보기를 위로 올리고 그 아래에 요약 줄, 맨 아래에 버튼을 둠
        static void InstallAssistSummary(HandOddsScreen screen)
        {
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
        }

        // InstallOddsSummary - panel에 확률 요약 문구를 넣고, 누르면 확률 창이 열리게 함
        // owner - 이 줄을 쓰는 화면(RoundScreen·AssistScreen)
        // labelField - 요약 문구를 받을 owner의 필드 이름
        static void InstallOddsSummary(UnityEngine.Object owner, RectTransform panel, string labelField, HandOddsScreen screen)
        {
            Button button = panel.GetComponent<Button>();
            if (button == null)
            {
                button = panel.gameObject.AddComponent<Button>();
            }
            button.targetGraphic = panel.GetComponent<Graphic>();
            // ExchangeOdds의 판은 입력 판정이 꺼져 있으므로 눌리도록 다시 켬
            button.targetGraphic.raycastTarget = true;

            // 왼쪽은 한 줄 요약(넘치면 …), 오른쪽 끝은 "자세히 >"
            TMP_Text label = Label("Summary", panel, "", new Vector2(-85f, 0f), new Vector2(panel.sizeDelta.x - 210f, 100f), 28f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text details = Label("Details", panel, "자세히 >", new Vector2(panel.sizeDelta.x * 0.5f - 100f, 0f), new Vector2(170f, 100f), 30f);
            details.color = UiStyle.TextMuted;

            // owner가 요약 문구를 채우고, 눌렸을 때 확률 창을 열 수 있게 연결
            Set(owner, labelField, label);
            Set(owner, "oddsButton", button);
            Set(owner, "oddsScreen", screen);
        }

        // owner의 배열 필드를 values로 채움
        static void SetArray(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
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
