using System;
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
     * 기존 배선과 조작 영역은 건드리지 않고 씬 UI에 비주얼만 다시 입히는 재실행 가능한 도구
     * 값은 코드로 고정해 두고 메뉴에서 다시 적용할 수 있게 한다
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

            RectTransform[] rects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true)).ToArray();
            Undo.RegisterFullObjectHierarchyUndo(scene.GetRootGameObjects().First(g => g.name == "Canvas"), "Arena UI visuals");

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

            foreach (string name in new[] { "ShopPanel", "ResultPanel" })
            {
                RectTransform rt = rects.Single(r => r.name == name);
                rt.GetComponent<Image>().color = UiStyle.PanelBg;

                ArenaPanelGraphic frame = Decoration(rt, "ArenaModalFrame");
                frame.rectTransform.offsetMin = new Vector2(30f, 48f);
                frame.rectTransform.offsetMax = new Vector2(-30f, -48f);
                frame.Configure(UiStyle.PanelBg, UiStyle.Border, true);
            }

            RectTransform body = rects.Single(r => r.name == "Body" && r.parent.name == "RandomSummonPanel");
            Skin(body, UiStyle.Plate, UiStyle.Border, true);

            RectTransform window = rects.Single(r => r.name == "Window" && r.parent == body);
            window.GetComponent<Image>().color = new Color(0.016f, 0.022f, 0.027f, 1f);
            rects.Single(r => r.name == "Name" && r.parent.name == "RowTemplate").GetComponent<TMP_Text>().color = UiStyle.TextPrimary;

            RectTransform breakPlate = rects.Single(r => r.name == "Plate" && r.parent.name == "RoundBreakPanel");
            breakPlate.sizeDelta = new Vector2(980f, 380f);
            Skin(breakPlate, UiStyle.Plate, UiStyle.Border, true);

            foreach (RectTransform rt in rects)
            {
                TMP_Text text = rt.GetComponent<TMP_Text>();

                if (text == null || text is TextMeshPro)
                {
                    // 월드 좌표 성급 텍스트는 건드리지 않는다
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
                    text.color = new Color(0.92f, 0.78f, 0.53f, 1f);
                    text.characterSpacing = 1f;
                }

                if (name == "Hint" || name == "HeldLabel" || name == "TrayLabel")
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

        static ArenaPanelGraphic Skin(RectTransform target, Color fill, Color rim, bool decorate)
        {
            Image image = target.GetComponent<Image>();

            if (image != null)
            {
                image.color = Color.clear;
                EditorUtility.SetDirty(image);
            }

            ArenaPanelGraphic graphic = Decoration(target, "ArenaSurface");
            graphic.Configure(fill, rim, decorate);
            EditorUtility.SetDirty(graphic);
            return graphic;
        }

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

            child.SetAsFirstSibling();

            RectTransform rt = (RectTransform)child;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

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
