using System;
using PokerDefense.Game;
using PokerDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PokerDefense.Editor
{
    public static partial class GameLoopSetup
    {
        // Install Run Reporter - Game 씬의 흐름 오브젝트에 결과 전송 컴포넌트를 붙이고 결과 화면과 연결
        // 이미 붙어 있으면 참조만 다시 연결하므로 여러 번 실행해도 됨
        [MenuItem("Tools/Poker Defense/Install Run Reporter")]
        public static void InstallRunReporter()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            EditorSceneManager.SaveOpenScenes();
            UnityEngine.SceneManagement.Scene game = EditorSceneManager.OpenScene(Scenes + "Game.unity");

            GameFlowController flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
            StageController stage = UnityEngine.Object.FindAnyObjectByType<StageController>();
            ResultScreen result = UnityEngine.Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);

            RunReporter reporter = flow.GetComponent<RunReporter>();
            if (reporter == null)
            {
                reporter = flow.gameObject.AddComponent<RunReporter>();
            }

            Set(reporter, "flow", flow);
            Set(reporter, "stage", stage);
            Set(result, "reporter", reporter);
            EditorSceneManager.SaveScene(game);
        }
    }
}
