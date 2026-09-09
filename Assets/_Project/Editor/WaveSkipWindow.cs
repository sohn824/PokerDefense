using PokerDefense.Game;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.EditorTools
{
    /**
     * WaveSkipWindow
     *
     * 개발 전용
     * 전투 없이 웨이브를 클리어한 것으로 치고 다음 라운드로 넘긴다
     */
    public sealed class WaveSkipWindow : EditorWindow
    {
        int targetWave = 1;

        [MenuItem("DevMode/웨이브 스킵")]
        static void Open()
        {
            GetWindow<WaveSkipWindow>("웨이브 스킵").minSize = new Vector2(280f, 160f);
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        void OnGUI()
        {
            GameFlowController flow = FindAnyObjectByType<GameFlowController>();
            StageController stageController = FindAnyObjectByType<StageController>();
            string blocked = BlockedReason(flow, stageController);

            if (blocked != null)
            {
                EditorGUILayout.HelpBox(blocked, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(blocked != null))
            {
                if (stageController != null)
                {
                    StageContext stage = stageController.Stage;
                    EditorGUILayout.LabelField("진행 중",
                        $"{stage.WaveIndex + 1} / {stage.TotalWaves} 웨이브");
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("다음 웨이브로", GUILayout.Height(24f)))
                {
                    flow.DevSkipWave();
                }

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    targetWave = Mathf.Max(1, EditorGUILayout.IntField("이동할 웨이브(1부터)", targetWave));

                    if (GUILayout.Button("이동", GUILayout.Width(60f)))
                    {
                        flow.DevJumpToWave(targetWave - 1);
                    }
                }
            }
        }

        // 버튼을 누를 수 없는 이유를 문자열로 반환
        // 누를 수 있는 상태면 null 반환
        static string BlockedReason(GameFlowController flow, StageController stageController)
        {
            if (Application.isPlaying == false)
            {
                return "플레이 중에만 쓸 수 있습니다.";
            }

            if (flow == null || stageController == null)
            {
                return "씬에서 GameFlowController를 찾지 못했습니다. Game 씬인지 확인해주세요.";
            }

            if (flow.IsFinished)
            {
                return "이미 게임이 끝났습니다.";
            }

            if (flow.ShopCards != null)
            {
                return "카드 상점이 열려 있습니다. 상점을 먼저 닫아주세요.";
            }

            if (flow.CanDevSkip == false)
            {
                return "전투 중에는 쓸 수 없습니다. 전투가 끝난 뒤(카드·배치 단계)에 사용해주세요.";
            }

            return null;
        }
    }
}
