using System;
using System.Linq;
using PokerDefense.Game;
using PokerDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PokerDefense.Editor
{
    public static partial class GameLoopSetup
    {
        // Install Run Saver - Game 씬의 흐름 오브젝트에 이어하기 저장 컴포넌트를 붙이고 컨트롤러들과 연결
        // 이미 붙어 있으면 참조만 다시 연결하므로 여러 번 실행해도 됨
        [MenuItem("Tools/Poker Defense/Install Run Saver")]
        public static void InstallRunSaver()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            EditorSceneManager.SaveOpenScenes();
            UnityEngine.SceneManagement.Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");

            GameFlowController flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            PlacementController placement = UnityEngine.Object.FindAnyObjectByType<PlacementController>();

            RunSaver saver = flow.GetComponent<RunSaver>();
            if (saver == null)
            {
                saver = flow.gameObject.AddComponent<RunSaver>();
            }

            Set(saver, "flow", flow);
            Set(saver, "round", UnityEngine.Object.FindAnyObjectByType<RoundController>());
            Set(saver, "placement", placement);
            Set(saver, "combat", UnityEngine.Object.FindAnyObjectByType<CombatController>());
            Set(saver, "stage", UnityEngine.Object.FindAnyObjectByType<StageController>());
            // 유닛 표는 PlacementController가 쓰는 것과 같은 에셋을 씀
            Set(saver, "unitTable", new SerializedObject(placement).FindProperty("unitTable").objectReferenceValue);
            Set(flow, "saver", saver);

            // 메뉴의 타이틀로 가기 창에 "저장하고 타이틀로"를 넣음 (메뉴 전체는 다시 만들지 않음)
            font = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;
            MenuScreen menu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
            GameObject abandon = (GameObject)new SerializedObject(menu).FindProperty("abandonPage").objectReferenceValue;
            InstallSaveExitChoice(menu, (RectTransform)abandon.transform, saver);
            EditorSceneManager.SaveScene(game);
        }

        // 메뉴의 타이틀로 가기 창을 "저장하고 타이틀로 / 포기하고 타이틀로 / 돌아가기" 선택 창으로 맞춤
        // saver가 없으면(타이틀 씬 등) 저장 버튼은 게임에서 숨겨짐
        static void InstallSaveExitChoice(MenuScreen menu, RectTransform abandon, RunSaver saver)
        {
            abandon.Find("Title").GetComponent<TMPro.TMP_Text>().text = "타이틀로 돌아갈까요?";
            TMPro.TMP_Text body = abandon.Find("Body").GetComponent<TMPro.TMP_Text>();
            body.text = "포기하면 지금 진행이 지워져 이어할 수 없습니다.\n저장하고 나가면 타이틀에서 이어할 수 있습니다.";
            body.rectTransform.sizeDelta = new Vector2(840f, 160f);

            Transform old = abandon.Find("SaveTitle");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            Set(menu, "saveTitleButton", Button("SaveTitle", abandon, "저장하고 타이틀로", -110f, UiStyle.ButtonPrimary));
            ((RectTransform)abandon.Find("AbandonConfirm")).anchoredPosition = new Vector2(0f, -260f);
            ((RectTransform)abandon.Find("Cancel")).anchoredPosition = new Vector2(0f, -410f);
            Set(menu, "saver", saver);
        }

        // Install Title Continue - 이미 있는 Title 씬 메뉴에 이어하기 버튼과 새 게임 확인 창만 추가
        // 타이틀 메뉴 전체를 다시 만들지 않으므로 다른 부분은 바뀌지 않음, 여러 번 실행해도 됨
        [MenuItem("Tools/Poker Defense/Install Title Continue")]
        public static void InstallTitleContinue()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            EditorSceneManager.SaveOpenScenes();
            UnityEngine.SceneManagement.Scene title = EditorSceneManager.OpenScene(Scenes + "Title.unity");
            font = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include).First(t => t.font != null).font;

            MenuScreen menu = UnityEngine.Object.FindAnyObjectByType<MenuScreen>();
            SerializedObject so = new SerializedObject(menu);
            RectTransform home = (RectTransform)((GameObject)so.FindProperty("home").objectReferenceValue).transform;
            RectTransform overlay = (RectTransform)((GameObject)so.FindProperty("overlay").objectReferenceValue).transform;
            InstallContinueMenu(menu, home, overlay);
            EditorSceneManager.SaveScene(title);
        }

        // 타이틀 홈에 이어하기 버튼을, 메뉴 오버레이에 새 게임 확인 창을 만들어 MenuScreen에 연결
        // 이어하기 버튼이 들어갈 자리를 만들려고 제목·부제목을 위로 올림
        static void InstallContinueMenu(MenuScreen menu, RectTransform home, RectTransform overlay)
        {
            ((RectTransform)home.Find("Title")).anchoredPosition = new Vector2(0f, 450f);
            ((RectTransform)home.Find("Subtitle")).anchoredPosition = new Vector2(0f, 330f);

            Transform oldContinue = home.Find("Continue");
            if (oldContinue != null)
            {
                UnityEngine.Object.DestroyImmediate(oldContinue.gameObject);
            }
            Set(menu, "continueButton", Button("Continue", home, "이어하기", 190f, UiStyle.ButtonPrimary));

            Transform oldPage = overlay.Find("NewGame");
            if (oldPage != null)
            {
                UnityEngine.Object.DestroyImmediate(oldPage.gameObject);
            }
            RectTransform page = Page("NewGame", overlay);
            Label("Title", page, "새 게임을 시작할까요?", new Vector2(0f, 260f), new Vector2(800f, 130f), 58f);
            Label("Body", page, "진행 중인 게임이 사라지고\n처음부터 시작합니다.", new Vector2(0f, 80f), new Vector2(780f, 160f), UiStyle.BodySize);
            Set(menu, "newGamePage", page.gameObject);
            Set(menu, "newGameConfirmButton", Button("NewGameConfirm", page, "새 게임 시작", -110f, UiStyle.ButtonDanger));
            Set(menu, "newGameCancelButton", Button("NewGameCancel", page, "돌아가기", -270f, UiStyle.ButtonSecondary));
        }
    }
}
