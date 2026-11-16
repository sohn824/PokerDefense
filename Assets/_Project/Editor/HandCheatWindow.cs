using PokerDefense.Game;
using PokerDefense.Poker;
using PokerDefense.UI;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.EditorTools
{
    /**
     * HandCheatWindow
     *
     * 원하는 족보로 손패를 바꾸는 에디터 창 (개발 전용)
     * 손패만 바꾸며 라운드는 다시 열지 않음
     * 바꾼 뒤에는 평소대로 손패 확정을 눌러 소환
     */
    public sealed class HandCheatWindow : EditorWindow
    {
        HandUnitTable unitTable;
        Vector2 scroll;

        [MenuItem("DevMode/족보 소환")]
        static void Open()
        {
            GetWindow<HandCheatWindow>("족보 소환").minSize = new Vector2(300f, 320f);
        }

        void Update()
        {
            // OnGUI는 입력이 없으면 호출되지 않으므로 단계가 바뀌면 직접 다시 그림
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        void OnGUI()
        {
            RoundController round = FindAnyObjectByType<RoundController>();
            string blocked = BlockedReason(round);

            EditorGUILayout.LabelField("족보를 고르면 손패가 그 5장으로 바뀜", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            if (blocked != null)
            {
                EditorGUILayout.HelpBox(blocked, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(blocked != null))
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;

                foreach (HandCategory category in HandCheatTable.Order)
                {
                    if (GUILayout.Button(Label(category), GUILayout.Height(24f)))
                    {
                        round.DevForceHand(HandCheatTable.HandFor(category));
                    }
                }
            }
        }

        // 버튼을 누를 수 없는 이유를 문자열로 반환
        // 누를 수 있는 상태면 null 반환
        static string BlockedReason(RoundController round)
        {
            if (Application.isPlaying == false)
            {
                return "플레이 중에만 쓸 수 있습니다.";
            }

            if (round == null)
            {
                return "씬에서 RoundController를 찾지 못했습니다. Game 씬인지 확인해주세요.";
            }

            if (round.CanForceHand == false)
            {
                return $"교체 단계에서만 바꿀 수 있습니다. 지금은 {round.Phase}입니다.";
            }

            return null;
        }

        // 어떤 유닛이 소환되는지 같이 보여줌 (유닛 이름은 HandUnitTable에서 읽어 데이터와 어긋나지 않도록 함)
        string Label(HandCategory category)
        {
            string hand = HandCategoryNames.Of(category);
            UnitDefinition unit = UnitFor(category);

            return unit == null ? hand : $"{hand}  →  {unit.DisplayName}";
        }

        UnitDefinition UnitFor(HandCategory category)
        {
            if (unitTable == null)
            {
                string[] found = AssetDatabase.FindAssets($"t:{nameof(HandUnitTable)}");

                if (found.Length == 0)
                {
                    return null;
                }

                unitTable = AssetDatabase.LoadAssetAtPath<HandUnitTable>(
                    AssetDatabase.GUIDToAssetPath(found[0]));
            }

            try
            {
                return unitTable.GetDefinition(category);
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }
        }
    }
}
