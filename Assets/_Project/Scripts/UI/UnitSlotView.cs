using System;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /// <summary>
    /// 보드 슬롯 한 칸. 비어 있으면 빈 칸 색, 유닛이 있으면 유닛 색 + 이름 + ★ + 공격력을 보여준다.
    /// 플레이스홀더다 — M6에서 스프라이트로 교체한다.
    /// </summary>
    public sealed class UnitSlotView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.24f, 0.25f, 0.29f);

        [SerializeField] Image background;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text starLabel;
        [SerializeField] TMP_Text powerLabel;
        [SerializeField] Image highlight;
        [SerializeField] Button button;

        public event Action<UnitSlotView> Clicked;

        public int Index { get; private set; }

        void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Bind(int index)
        {
            Index = index;
        }

        public void Show(UnitInstance unit)
        {
            if (unit == null)
            {
                background.color = EmptyColor;
                nameLabel.text = string.Empty;
                starLabel.text = string.Empty;
                powerLabel.text = string.Empty;
                return;
            }

            background.color = unit.Definition.PlaceholderColor;
            nameLabel.text = unit.Definition.DisplayName;
            starLabel.text = new string('*', unit.Star);
            powerLabel.text = "ATK " + unit.AttackPower.ToString("0.#");
        }

        /// <summary>테두리 강조. 머지 상대로 고른 칸은 노랑, 그냥 놓을 수 있는 칸은 흰색.</summary>
        public void SetHighlight(bool on, bool selected)
        {
            highlight.enabled = on;
            highlight.color = selected
                ? new Color(1f, 0.82f, 0.15f, 0.45f)
                : new Color(1f, 1f, 1f, 0.22f);
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
