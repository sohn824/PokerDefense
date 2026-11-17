using PokerDefense.Game;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.EditorTools
{
    /**
     * WaveSkipWindow
     *
     * 전투 없이 웨이브를 클리어한 것으로 처리하거나 원하는 웨이브로 이동하는 에디터 창 (개발 전용)
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
            // OnGUI는 입력이 없으면 호출되지 않으므로 상태가 바뀌면 직접 다시 그림
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        // 쓸 수 없으면 이유를 보여 주고 버튼을 모두 막음
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
                    // WaveIndex는 0부터 세므로 1을 더해 보여 줌
                    // 마지막 웨이브를 클리어하면 WaveIndex가 총 웨이브 수와 같아지므로 따로 표시함
                    EditorGUILayout.LabelField("진행 중", stage.IsAllWavesCleared
                        ? "모든 웨이브 클리어"
                        : $"{stage.WaveIndex + 1} / {stage.TotalWaves} 웨이브");
                }

                EditorGUILayout.Space();

                // 지금 웨이브를 클리어한 것으로 치고 다음 라운드로 넘김
                if (GUILayout.Button("다음 웨이브로", GUILayout.Height(24f)))
                {
                    flow.DevSkipWave();
                }

                EditorGUILayout.Space();

                // 입력한 웨이브까지 앞으로만 건너뜀
                // 실수로 스테이지가 끝나지 않도록 마지막 웨이브까지만 입력받음
                using (new EditorGUILayout.HorizontalScope())
                {
                    int lastWave = stageController != null ? stageController.Stage.TotalWaves : int.MaxValue;
                    targetWave = Mathf.Clamp(EditorGUILayout.IntField("이동할 웨이브(1부터)", targetWave), 1, lastWave);

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

            if (flow.CanDevSkip == false)
            {
                return "전투 중에는 쓸 수 없습니다. 전투가 끝난 뒤(카드·배치 단계)에 사용해주세요.";
            }

            return null;
        }
    }
}
