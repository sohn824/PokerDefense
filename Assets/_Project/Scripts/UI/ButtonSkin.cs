using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ButtonSkin
     *
     * 버튼 역할(주 행동 / 보조 / 위험)에 맞는 공통 배경색을 입힌다
     * 비활성일 때는 배경만 어둡게 죽이고 라벨은 그대로 둬서 실행 불가 사유 문구가 계속 읽히게 한다
     */
    [RequireComponent(typeof(Image))]
    public sealed class ButtonSkin : MonoBehaviour
    {
        public enum Role { Primary, Secondary, Danger }

        [SerializeField] Role role = Role.Secondary;

        Image image;
        Selectable selectable;
        bool lastOn;

        void Awake()
        {
            image = GetComponent<Image>();
            selectable = GetComponent<Selectable>();
            Apply();
        }

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            bool on = selectable == null || selectable.IsInteractable();

            if (on != lastOn)
            {
                Apply();
            }
        }

        void Apply()
        {
            lastOn = selectable == null || selectable.IsInteractable();
            image.color = lastOn ? RoleColor() : UiStyle.ButtonDisabled;
        }

        Color RoleColor()
        {
            switch (role)
            {
                case Role.Primary: return UiStyle.ButtonPrimary;
                case Role.Danger: return UiStyle.ButtonDanger;
                default: return UiStyle.ButtonSecondary;
            }
        }
    }
}
