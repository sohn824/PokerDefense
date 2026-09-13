using System;
using System.Linq;
using PokerDefense.Game;
using PokerDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokerDefense.Editor
{
    // 기존 씬 배선을 유지하며 메뉴 씬과 공통 UI를 설치한다. 같은 메뉴를 다시 실행해도 중복되지 않는다.
    public static class GameLoopSetup
    {
        const string Scenes = "Assets/_Project/Scenes/";
        static TMP_FontAsset font;

        [MenuItem("Tools/Poker Defense/Install Game Loop")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }
            Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;
            InstallAssist();
            InstallGuides();
            InstallMenu(false);
            EditorSceneManager.SaveScene(game);

            Scene title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = UiStyle.PanelBg;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InstallMenu(true);
            EditorSceneManager.SaveScene(title, Scenes + "Title.unity");

            Scene boot = EditorSceneManager.OpenScene(Scenes + "Boot.unity");
            if (UnityEngine.Object.FindAnyObjectByType<BootController>() == null)
                new GameObject("BootController", typeof(BootController));
            EditorSceneManager.SaveScene(boot);
            EditorBuildSettings.scenes = new[] { "Boot", "Title", "Game" }
                .Select(n => new EditorBuildSettingsScene(Scenes + n + ".unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Scenes + "Game.unity");
        }

        // 피드백 반영용: 기존 타이틀·메뉴의 배치는 보존하고 해당 UI만 갱신한다.
        [MenuItem("Tools/Poker Defense/Update Guide and Assist UI")]
        public static void UpdateGuideAndAssist()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.SaveOpenScenes();
            Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;
            InstallAssist();
            InstallGuides();
            MenuScreen menu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
            SerializedObject so = new SerializedObject(menu);
            SerializedProperty groups = so.FindProperty("inputGroups");
            groups.arraySize = 3;
            string[] names = { "Canvas", "AssistCanvas", "GuideCanvas" };
            for (int i = 0; i < names.Length; i++)
            {
                groups.GetArrayElementAtIndex(i).objectReferenceValue = GameObject.Find(names[i]).GetComponent<CanvasGroup>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            UpdateHelpText();
            EditorSceneManager.SaveScene(game);
            Scene title = EditorSceneManager.OpenScene(Scenes + "Title.unity");
            UpdateHelpText();
            EditorSceneManager.SaveScene(title);
            EditorSceneManager.OpenScene(Scenes + "Game.unity");
        }

        static void UpdateHelpText()
        {
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (text.text.Contains("승부 교체"))
                {
                    text.text = text.text.Replace("승부 교체", "추가 교체")
                        .Replace("일반 교체한 자리 한 곳에 다시 도전합니다.\n공개한 후보 중 한 장을 반드시 선택합니다.",
                            "교체했던 카드 한 장을 다시 바꿀 수 있습니다.\n카드 선택 → 후보 확인 → 한 장으로 교체\n후보를 보면 기회 1회와 유지 보너스를 씁니다.");
                    EditorUtility.SetDirty(text);
                }
            }
        }

        static void InstallGuides()
        {
            OnboardingGuide guide = UnityEngine.Object.FindAnyObjectByType<OnboardingGuide>();
            SerializedObject so = new SerializedObject(guide);
            foreach (string field in new[] { "cardHintRow", "shopHintRow" })
            {
                GameObject oldRow = so.FindProperty(field).objectReferenceValue as GameObject;
                if (oldRow != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldRow);
                }
            }
            GameObject old = GameObject.Find("GuideCanvas");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }
            RectTransform root = Rect("GuideCanvas", null, Vector2.zero, Vector2.zero);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<CanvasGroup>();
            so.Update();
            SerializedProperty groups = so.FindProperty("inputGroups");
            groups.arraySize = 2;
            groups.GetArrayElementAtIndex(0).objectReferenceValue = GameObject.Find("Canvas").GetComponent<CanvasGroup>();
            groups.GetArrayElementAtIndex(1).objectReferenceValue = GameObject.Find("AssistCanvas").GetComponent<CanvasGroup>();
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (string prefix in new[] { "card", "shop" })
            {
                RectTransform overlay = Rect(prefix + "GuideOverlay", root, Vector2.zero, Vector2.zero);
                Stretch(overlay);
                overlay.gameObject.AddComponent<Image>().color = UiStyle.Scrim;
                RectTransform safe = Rect("SafeArea", overlay, Vector2.zero, Vector2.zero);
                Stretch(safe);
                safe.gameObject.AddComponent<SafeAreaFitter>();
                RectTransform page = Page("GuideDialog", safe);
                page.sizeDelta = new Vector2(940f, 900f);
                Label("Header", page, "플레이 안내", new Vector2(0f, 340f), new Vector2(820f, 70f), UiStyle.BodySize);
                TMP_Text body = Label("Body", page, "", new Vector2(0f, 55f), new Vector2(830f, 450f), UiStyle.BodySize);
                Label("Footer", page, "읽는 동안 게임은 잠시 멈춥니다 · 도움말에서 다시 확인", new Vector2(0f, -205f), new Vector2(830f, 60f), 26f);
                Button close = Button("Continue", page, "직접 해보기", -325f, UiStyle.ButtonPrimary);
                Set(guide, prefix + "HintRow", overlay.gameObject);
                Set(guide, prefix + "HintLabel", body);
                Set(guide, prefix + "HintClose", close);
                overlay.gameObject.SetActive(false);
            }
        }

        static void InstallAssist()
        {
            // 진입 버튼은 공통 행동 바 소속이므로 후보 Canvas와 별도로 정리한다.
            foreach (Transform previous in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(t => t.name == "AssistButton" || t.name == "ExchangeActionHint").ToArray())
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }
            GameObject old = GameObject.Find("AssistCanvas");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }
            RectTransform root = Rect("AssistCanvas", null, Vector2.zero, Vector2.zero);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            root.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform safe = Rect("SafeArea", root, Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            root.gameObject.AddComponent<CanvasGroup>();
            AssistScreen screen = root.gameObject.AddComponent<AssistScreen>();
            GameObject gameCanvas = GameObject.Find("Canvas");
            CanvasGroup gameInput = gameCanvas.GetComponent<CanvasGroup>();
            if (gameInput == null)
            {
                gameInput = gameCanvas.AddComponent<CanvasGroup>();
            }
            Set(screen, "gameInput", gameInput);
            RoundController round = UnityEngine.Object.FindAnyObjectByType<RoundController>();
            StageController stage = UnityEngine.Object.FindAnyObjectByType<StageController>();
            Set(round, "stage", stage);
            RunJournal journal = round.GetComponent<RunJournal>();
            if (journal == null)
            {
                journal = round.gameObject.AddComponent<RunJournal>();
            }
            Set(journal, "round", round);
            Set(journal, "stage", stage);
            Set(journal, "combat", UnityEngine.Object.FindAnyObjectByType<CombatController>());
            Set(journal, "flow", UnityEngine.Object.FindAnyObjectByType<GameFlowController>());
            Set(journal, "placement", UnityEngine.Object.FindAnyObjectByType<PlacementController>());
            Set(screen, "round", round);
            Set(screen, "stage", stage);
            Set(screen, "placement", UnityEngine.Object.FindAnyObjectByType<PlacementController>());
            Set(screen, "flow", UnityEngine.Object.FindAnyObjectByType<GameFlowController>());
            Set(screen, "unitTable", AssetDatabase.LoadAssetAtPath<HandUnitTable>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:HandUnitTable")[0])));
            Transform actionBar = GameObject.Find("BottomActionArea").transform;
            ((RectTransform)actionBar).sizeDelta = new Vector2(1040f, 160f);
            Transform exchange = actionBar.Find("ExchangeButton");
            Transform confirm = actionBar.Find("ConfirmButton");
            Button open = Button("AssistButton", actionBar, "추가 교체", 0f, UiStyle.ButtonSecondary);
            open.transform.SetSiblingIndex(exchange.GetSiblingIndex() + 1);
            foreach (Transform button in new[] { exchange, open.transform, confirm })
            {
                LayoutElement layout = button.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = button.gameObject.AddComponent<LayoutElement>();
                }
                layout.preferredWidth = 300f;
                layout.flexibleWidth = 1f;
                layout.preferredHeight = 140f;
                button.GetComponentInChildren<TMP_Text>().fontSize = UiStyle.ActionSize;
            }
            TMP_Text hint = Label("ExchangeActionHint", actionBar, "", new Vector2(0f, 50f), new Vector2(1000f, 76f), UiStyle.CaptionSize);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 1f);
            hint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Set(screen, "actionHint", hint);
            Set(screen, "openButton", open);
            Set(screen, "openLabel", open.GetComponentInChildren<TMP_Text>());
            RectTransform overlay = Rect("AssistOverlay", safe, Vector2.zero, Vector2.zero);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = UiStyle.Scrim;
            RectTransform page = Page("Candidates", overlay);
            Set(screen, "panel", overlay.gameObject);
            Label("Title", page, "추가 교체", new Vector2(0f, 470f), new Vector2(800f, 100f), UiStyle.TitleSize);
            Set(screen, "instruction", Label("Instruction", page, "", new Vector2(0f, 340f), new Vector2(850f, 130f), UiStyle.BodySize));
            Button[] slots = new Button[5];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = Button("HandSlot" + i, page, "", 170f, UiStyle.ButtonSecondary);
                RectTransform rt = (RectTransform)slots[i].transform;
                rt.anchoredPosition = new Vector2((i - 2) * 168f, 170f);
                rt.sizeDelta = new Vector2(152f, 145f);
                slots[i].GetComponentInChildren<TMP_Text>().fontSize = UiStyle.CaptionSize;
            }
            Button[] candidates = new Button[3];
            for (int i = 0; i < candidates.Length; i++)
            {
                candidates[i] = Button("Candidate" + i, page, "", -35f, UiStyle.ButtonPrimary);
                RectTransform rt = (RectTransform)candidates[i].transform;
                rt.anchoredPosition = new Vector2((i - 1) * 280f, -35f);
                rt.sizeDelta = new Vector2(260f, 205f);
                candidates[i].GetComponentInChildren<TMP_Text>().fontSize = UiStyle.BodySize;
            }
            SerializedObject so = new SerializedObject(screen);
            foreach (string field in new[] { "slots", "candidates" })
            {
                Button[] values = field == "slots" ? slots : candidates;
                SerializedProperty array = so.FindProperty(field);
                array.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(screen, "preview", Label("Preview", page, "", new Vector2(0f, -235f), new Vector2(850f, 190f), UiStyle.CaptionSize));
            Set(screen, "revealButton", Button("Reveal", page, "후보 3장 공개", -410f, UiStyle.ButtonPrimary));
            Set(screen, "chooseButton", Button("Choose", page, "이 카드로 교체", -410f, UiStyle.ButtonPrimary));
            Set(screen, "cancelButton", Button("Cancel", overlay, "취소", -710f, UiStyle.ButtonSecondary));
            overlay.gameObject.SetActive(false);
        }

        static void InstallMenu(bool title)
        {
            GameObject old = GameObject.Find("MenuCanvas");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }
            RectTransform canvas = Rect("MenuCanvas", null, Vector2.zero, Vector2.zero);
            Canvas c = canvas.gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 200;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            MenuScreen menu = canvas.gameObject.AddComponent<MenuScreen>();
            Set(menu, "titleScene", title);
            SerializedObject menuObject = new SerializedObject(menu);
            SerializedProperty inputGroups = menuObject.FindProperty("inputGroups");
            inputGroups.arraySize = title ? 0 : 3;
            if (title == false)
            {
                inputGroups.GetArrayElementAtIndex(0).objectReferenceValue = GameObject.Find("Canvas").GetComponent<CanvasGroup>();
                inputGroups.GetArrayElementAtIndex(1).objectReferenceValue = GameObject.Find("AssistCanvas").GetComponent<CanvasGroup>();
                inputGroups.GetArrayElementAtIndex(2).objectReferenceValue = GameObject.Find("GuideCanvas").GetComponent<CanvasGroup>();
            }
            menuObject.ApplyModifiedPropertiesWithoutUndo();
            Set(menu, "flow", UnityEngine.Object.FindAnyObjectByType<GameFlowController>());

            if (title)
            {
                RectTransform bg = Rect("ArenaBackground", canvas, Vector2.zero, Vector2.zero);
                Stretch(bg);
                Image art = bg.gameObject.AddComponent<Image>();
                art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Stage/Background.png");
                art.color = new Color(.45f, .45f, .45f, 1f);
                art.raycastTarget = false;
            }

            RectTransform safe = Rect("SafeArea", canvas, Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform home = Page("Home", safe);
            Set(menu, "home", title ? home.gameObject : null);
            if (title)
            {
                Label("Title", home, "포커 디펜스", new Vector2(0f, 370f), new Vector2(780f, 130f), 80f);
                Label("Subtitle", home, "운명을 고르고, 투기장을 지켜라", new Vector2(0f, 250f), new Vector2(780f, 80f), UiStyle.BodySize);
                Set(menu, "startButton", Button("Start", home, "게임 시작", 70f, UiStyle.ButtonPrimary));
            }
            else
            {
                home.gameObject.SetActive(false);
                Button open = Button("MenuButton", safe, "메뉴", 0f, UiStyle.ButtonSecondary);
                RectTransform rt = (RectTransform)open.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-95f, -270f);
                rt.sizeDelta = new Vector2(160f, 100f);
                Set(menu, "menuButton", open);
            }

            RectTransform overlay = Rect("MenuOverlay", safe, Vector2.zero, Vector2.zero);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = UiStyle.Scrim;
            Set(menu, "overlay", overlay.gameObject);
            RectTransform pause = Page("Pause", overlay);
            RectTransform options = Page("Options", overlay);
            RectTransform help = Page("Help", overlay);
            RectTransform abandon = Page("Abandon", overlay);
            Set(menu, "pausePage", pause.gameObject);
            Set(menu, "optionsPage", options.gameObject);
            Set(menu, "helpPage", help.gameObject);
            Set(menu, "abandonPage", abandon.gameObject);
            Label("Title", pause, "일시정지", new Vector2(0f, 430f), new Vector2(780f, 110f), UiStyle.TitleSize);
            Set(menu, "resumeButton", Button("Resume", pause, "계속하기", 250f, UiStyle.ButtonPrimary));
            Set(menu, "titleButton", Button("ReturnTitle", pause, "타이틀로", -200f, UiStyle.ButtonDanger));

            RectTransform main = title ? home : pause;
            Set(menu, "optionsButton", Button("OptionsButton", main, "옵션", title ? -80f : 100f, UiStyle.ButtonSecondary));
            Set(menu, "helpButton", Button("HelpButton", main, "도움말", title ? -230f : -50f, UiStyle.ButtonSecondary));
            Button quit = Button("Quit", main, "게임 종료", title ? -380f : -350f, UiStyle.ButtonDanger);
            Set(menu, "quitButton", quit);

            Label("Title", options, "오디오 옵션", new Vector2(0f, 430f), new Vector2(780f, 110f), UiStyle.TitleSize);
            Set(menu, "master", Slider("Master", options, "전체 음량", 280f));
            Set(menu, "music", Slider("Music", options, "배경음악", 120f));
            Set(menu, "effects", Slider("Effects", options, "효과음", -40f));
            RectTransform mute = Rect("Mute", options, new Vector2(0f, -220f), new Vector2(700f, 95f));
            Toggle toggle = mute.gameObject.AddComponent<Toggle>();
            Image box = Rect("Box", mute, new Vector2(-270f, 0f), new Vector2(65f, 65f)).gameObject.AddComponent<Image>();
            box.color = UiStyle.ButtonSecondary;
            Image mark = Rect("Mark", box.transform, Vector2.zero, new Vector2(42f, 42f)).gameObject.AddComponent<Image>();
            mark.color = UiStyle.Currency;
            toggle.targetGraphic = box;
            toggle.graphic = mark;
            Label("Label", mute, "전체 음소거", new Vector2(50f, 0f), new Vector2(500f, 80f), UiStyle.ActionSize);
            Set(menu, "mute", toggle);
            Set(menu, "volumeLabel", Label("Volumes", options, "", new Vector2(0f, -340f), new Vector2(800f, 70f), UiStyle.CaptionSize));
            Set(menu, "resetButton", Button("Reset", options, "기본값 복원", -460f, UiStyle.ButtonSecondary));

            Label("Title", help, "투기장 안내", new Vector2(0f, 450f), new Vector2(780f, 110f), UiStyle.TitleSize);
            TMP_Text body = Label("Body", help,
                "<b>손패를 완성하세요</b>\n5장의 족보가 소환 유닛을 결정합니다.\n각 자리는 한 번 교체할 수 있습니다.\n교체를 아끼면 유지 보너스 Chip을 받습니다.\n\n<b>보드를 강화하세요</b>\n같은 유닛·같은 성급끼리 합치면 승급합니다.\n다른 유닛은 자리를 바꿀 수 있습니다.\n사거리와 이번 적의 특성을 확인하세요.\n\n<b>다음 손패를 준비하세요</b>\n상점에서 산 카드는 최대 3장 보유합니다.\n손패에 놓아도 유지 보너스는 줄지 않습니다.\n\n<b>추가 교체</b>\n교체했던 카드 한 장을 다시 바꿀 수 있습니다.\n카드 선택 → 후보 확인 → 한 장으로 교체\n후보를 보면 기회 1회와 유지 보너스를 씁니다.",
                new Vector2(0f, -40f), new Vector2(760f, 820f), UiStyle.BodySize);
            body.alignment = TextAlignmentOptions.TopLeft;
            Label("Title", abandon, "진행을 포기할까요?", new Vector2(0f, 260f), new Vector2(800f, 130f), 58f);
            Label("Body", abandon, "현재 런은 저장되지 않습니다.\n타이틀로 돌아가면 처음부터 시작합니다.", new Vector2(0f, 80f), new Vector2(780f, 160f), UiStyle.BodySize);
            Set(menu, "abandonButton", Button("AbandonConfirm", abandon, "포기하고 타이틀로", -110f, UiStyle.ButtonDanger));
            Set(menu, "cancelButton", Button("Cancel", abandon, "돌아가기", -270f, UiStyle.ButtonSecondary));
            Button back = Button("Back", overlay, "돌아가기", -700f, UiStyle.ButtonSecondary);
            Set(menu, "backButton", back);
            overlay.gameObject.SetActive(false);

            if (title == false)
            {
                Transform result = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Single(r => r.name == "ResultPanel");
                Transform previous = result.Find("ReturnTitle");
                if (previous != null)
                {
                    UnityEngine.Object.DestroyImmediate(previous.gameObject);
                }
                Button ret = Button("ReturnTitle", result, "타이틀로", -610f, UiStyle.ButtonSecondary);
                UnityEventTools.AddPersistentListener(ret.onClick, menu.RequestTitle);
            }
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static RectTransform Page(string name, Transform parent)
        {
            RectTransform rt = Rect(name, parent, Vector2.zero, new Vector2(940f, 1220f));
            rt.gameObject.AddComponent<CanvasRenderer>();
            rt.gameObject.AddComponent<ArenaPanelGraphic>().Configure(UiStyle.PanelBg, UiStyle.Border, true);
            return rt;
        }

        internal static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            TMP_Text label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = UiStyle.TextPrimary;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        internal static Button Button(string name, Transform parent, string text, float y, Color color)
        {
            RectTransform rt = Rect(name, parent, new Vector2(0f, y), new Vector2(700f, 116f));
            rt.gameObject.AddComponent<Image>().color = color;
            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            ButtonSkin skin = rt.gameObject.AddComponent<ButtonSkin>();
            Set(skin, "role", (int)(color == UiStyle.ButtonPrimary ? ButtonSkin.Role.Primary : color == UiStyle.ButtonDanger ? ButtonSkin.Role.Danger : ButtonSkin.Role.Secondary));
            RectTransform frame = Rect("Frame", rt, Vector2.zero, Vector2.zero);
            Stretch(frame);
            frame.gameObject.AddComponent<CanvasRenderer>();
            ArenaPanelGraphic plate = frame.gameObject.AddComponent<ArenaPanelGraphic>();
            plate.raycastTarget = false;
            skin.SetPlate(plate);
            TMP_Text label = Label("Label", rt, text, Vector2.zero, Vector2.zero, UiStyle.ActionSize);
            Stretch(label.rectTransform);
            return button;
        }

        static Slider Slider(string name, Transform parent, string text, float y)
        {
            Label(name + "Label", parent, text, new Vector2(0f, y + 40f), new Vector2(720f, 70f), UiStyle.BodySize);
            RectTransform rt = Rect(name, parent, new Vector2(0f, y - 35f), new Vector2(700f, 65f));
            Image background = rt.gameObject.AddComponent<Image>();
            background.color = UiStyle.ButtonSecondary;
            Slider slider = rt.gameObject.AddComponent<Slider>();
            RectTransform area = Rect("HandleArea", rt, Vector2.zero, new Vector2(-48f, 0f));
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            Image handle = Rect("Handle", area, Vector2.zero, new Vector2(48f, 65f)).gameObject.AddComponent<Image>();
            handle.color = UiStyle.Currency;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.value = 1f;
            return slider;
        }

        internal static void Set(UnityEngine.Object obj, string field, object value)
        {
            SerializedObject so = new SerializedObject(obj);
            SerializedProperty p = so.FindProperty(field);
            if (value is bool b)
            {
                p.boolValue = b;
            }
            else if (value is int i)
            {
                p.intValue = i;
            }
            else p.objectReferenceValue = value as UnityEngine.Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
