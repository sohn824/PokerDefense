using PokerDefense.Game;
using TMPro;
using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * UnitSlotView
     *
     * 보드 슬롯 한 칸의 월드 스페이스 표현
     * 비어 있으면 빈 칸 색, 유닛이 있으면 유닛 색 + 이름 + 성급 + 공격력을 표시
     */
    public sealed class UnitSlotView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.24f, 0.25f, 0.29f);
        static readonly Color PlaceableColor = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.15f, 0.45f);

        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer highlight;
        [SerializeField] TextMeshPro nameLabel;
        [SerializeField] TextMeshPro starLabel;
        [SerializeField] TextMeshPro powerLabel;
        [SerializeField] Collider2D hitbox;

        public int Index { get; private set; }

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
            starLabel.text = new string('★', unit.Star);
            powerLabel.text = "ATK " + unit.AttackPower.ToString("0.#");
        }

        // 머지 상대로 고른 칸은 노랑, 그냥 놓을 수 있는 칸은 흰색
        public void SetHighlight(bool on, bool selected)
        {
            highlight.enabled = on;
            highlight.color = selected ? SelectedColor : PlaceableColor;
        }

        // 누를 수 없는 칸은 hitbox를 꺼서 Physics2D 검사에서 아예 빠지게 함
        public void SetInteractable(bool interactable)
        {
            hitbox.enabled = interactable;
        }
    }
}
