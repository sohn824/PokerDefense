using PokerDefense.Game;
using TMPro;
using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * UnitSlotView
     *
     * 보드 슬롯 한 칸의 월드 스페이스 표현
     *
     * 칸에는 **무엇인지와 몇 성인지만** 둔다. 수치는 유닛을 고르면 상세 패널이 보여준다 (DESIGN §10.3)
     * 칸이 1080 기준 145px이라 네 가지를 밀어 넣으면 아트가 들어갈 자리가 없어진다.
     * 칸 자체는 더 못 키운다 - 트랙이 이미 화면 가로를 거의 다 쓴다
     *
     * **이름은 아트가 없는 동안의 대역이다.** M10에서 스프라이트로 교체하며 이름 라벨은 지운다
     */
    public sealed class UnitSlotView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.24f, 0.25f, 0.29f);
        static readonly Color PlaceableColor = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.15f, 0.45f);

        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer highlight;

        [Tooltip("M10에서 유닛 스프라이트로 교체할 이름 대역")]
        [SerializeField] TextMeshPro nameLabel;

        [SerializeField] TextMeshPro starLabel;
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
                return;
            }

            background.color = unit.Definition.PlaceholderColor;
            starLabel.text = new string('★', unit.Star);

            // 띄어쓰기를 개행으로 바꿔 단어 단위로만 끊는다
            nameLabel.text = unit.Definition.DisplayName.Replace(' ', '\n');
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
