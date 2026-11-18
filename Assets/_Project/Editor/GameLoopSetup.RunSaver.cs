using System;
using PokerDefense.Game;
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
            EditorSceneManager.SaveScene(game);
        }
    }
}
