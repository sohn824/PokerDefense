using System;
using System.Collections.Generic;
using System.Linq;
using PokerDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokerDefense.Editor
{
    /**
     * ArenaUiVisualSetup
     *
     * 연결과 조작 영역은 그대로 두고 씬 UI의 외형만 다시 입히는 클래스
     * 메뉴에서 몇 번이든 다시 적용할 수 있음
     */
    public static class ArenaUiVisualSetup
    {
        [MenuItem("Tools/Poker Defense/Apply Arena UI Visuals")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before styling the scene.");
            }

            Scene scene = EditorSceneManager.GetActiveScene();

            if (scene.path != "Assets/_Project/Scenes/Game.unity")
            {
                throw new InvalidOperationException("Open Game.unity first.");
            }

            GameObject[] roots = scene.GetRootGameObjects();
            RectTransform[] rects = roots.SelectMany(g => g.GetComponentsInChildren<RectTransform>(true)).ToArray();

            // 이름마다 씬 전체에서 같은 이름이 몇 개인지 세고, 개수가 1이 아닌 이름들을 invalid에 모음
            List<string> invalid = new[] { "HudBar", "ThreatBar", "DetailPlate", "HandPlate", "BottomBarPlate", "ResultPanel",
                "CategoryLabel", "StatusLabel", "PendingLabel", "CombatLabel", "TitleLabel", "Hint" }
                .Where(n => rects.Count(r => r.name == n) != 1).ToList();

            // Plate는 이름만으로 구분할 수 없어 IsBreakPlate 메소드로 부모까지 확인함
            if (rects.Count(IsBreakPlate) != 1)
            {
                invalid.Add("RoundBreakPanel/Plate");
            }

            if (invalid.Count > 0)
            {
                throw new InvalidOperationException("Missing or duplicated scene objects: " + string.Join(", ", invalid));
            }

            foreach (GameObject root in roots)
            {
                Undo.RegisterFullObjectHierarchyUndo(root, "Arena UI visuals");
            }

            string[] panels = { "HudBar", "ThreatBar", "DetailPlate", "HandPlate", "BottomBarPlate" };

            foreach (string name in panels)
            {
                RectTransform rt = rects.Single(r => r.name == name);
                Skin(rt, UiStyle.Plate, name == "ThreatBar" ? UiStyle.BorderMuted : UiStyle.Border, true);
            }

            foreach (ButtonSkin skin in rects.Select(r => r.GetComponent<ButtonSkin>()).Where(s => s != null))
            {
                ArenaPanelGraphic plate = Skin((RectTransform)skin.transform, UiStyle.ButtonSecondary, UiStyle.Border, false);
                skin.SetPlate(plate);
                Button button = skin.GetComponent<Button>();

                if (button != null)
                {
                    button.transition = Selectable.Transition.None;
                }

                EditorUtility.SetDirty(skin);
            }

            RectTransform resultPanel = rects.Single(r => r.name == "ResultPanel");
            resultPanel.GetComponent<Image>().color = UiStyle.PanelBg;

            ArenaPanelGraphic frame = Decoration(resultPanel, "ArenaModalFrame");
            frame.rectTransform.offsetMin = new Vector2(30f, 48f);
            frame.rectTransform.offsetMax = new Vector2(-30f, -48f);
            frame.Configure(UiStyle.PanelBg, UiStyle.Border, true);

            RectTransform breakPlate = rects.Single(IsBreakPlate);
            breakPlate.sizeDelta = new Vector2(980f, 380f);
            Skin(breakPlate, UiStyle.Plate, UiStyle.Border, true);

            foreach (RectTransform rt in rects)
            {
                TMP_Text text = rt.GetComponent<TMP_Text>();

                if (text == null || text is TextMeshPro)
                {
                    // 월드 공간 텍스트는 건드리지 않음
                    continue;
                }

                string name = rt.name;

                if (name == "Rank" || name == "SuitIcon")
                {
                    continue;
                }

                if (name == "CategoryLabel")
                {
                    text.fontSize = 56f;
                    text.color = UiStyle.TextPrimary;
                }

                if (name == "StatusLabel" || name == "PendingLabel" || name == "CombatLabel")
                {
                    text.color = UiStyle.TextBody;
                }

                if (name == "TitleLabel" || (name == "Title" && rt.parent.name == "RoundBreakPanel"))
                {
                    text.color = UiStyle.TitleGold;
                    text.characterSpacing = 1f;
                }

                if (name == "Hint")
                {
                    text.color = UiStyle.TextMuted;
                }

                if (name == "Label" && rt.parent.GetComponent<ButtonSkin>() != null)
                {
                    text.color = UiStyle.TextPrimary;
                    text.margin = new Vector4(12f, 6f, 12f, 6f);
                }

                EditorUtility.SetDirty(text);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool IsBreakPlate(RectTransform r) => r.name == "Plate" && r.parent != null && r.parent.name == "RoundBreakPanel";

        // 기존 Image를 숨기고 그 자리에 ArenaPanelGraphic 판을 깔아 외형을 입힘
        static ArenaPanelGraphic Skin(RectTransform target, Color fill, Color rim, bool decorate)
        {
            Image image = target.GetComponent<Image>();

            if (image != null)
            {
                // 지우지 않고 투명하게만 해서 입력 판정은 남김
                image.color = Color.clear;
                EditorUtility.SetDirty(image);
            }

            // GameLoopSetup이 만든 버튼은 판 이름이 Frame이므로 있으면 그대로 씀
            ArenaPanelGraphic graphic = Decoration(target, target.Find("Frame") != null ? "Frame" : "ArenaSurface");
            graphic.Configure(fill, rim, decorate);
            EditorUtility.SetDirty(graphic);
            return graphic;
        }

        // 이름이 같은 판이 있으면 다시 쓰고 없으면 만들어 부모를 꽉 채우는 배경으로 맞춤
        static ArenaPanelGraphic Decoration(RectTransform parent, string name)
        {
            Transform child = parent.Find(name);

            if (child == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ArenaPanelGraphic));
                Undo.RegisterCreatedObjectUndo(go, "Arena UI surface");
                go.layer = parent.gameObject.layer;
                child = go.transform;
                child.SetParent(parent, false);
            }

            // 부모 아래 첫 번째로 옮겨 글자보다 먼저 그려지게 함
            child.SetAsFirstSibling();

            RectTransform rt = (RectTransform)child;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 부모의 레이아웃 그룹이 판을 칸으로 세지 않게 함
            LayoutElement layout = child.GetComponent<LayoutElement>();

            if (layout == null)
            {
                layout = child.gameObject.AddComponent<LayoutElement>();
            }

            layout.ignoreLayout = true;
            return child.GetComponent<ArenaPanelGraphic>();
        }
    }
}
