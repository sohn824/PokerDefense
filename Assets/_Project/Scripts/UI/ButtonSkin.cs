using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PokerDefense.UI
{
    /**
     * ButtonSkin
     *
     * 버튼 역할에 맞는 공통 배경색을 입히는 클래스
     * 비활성일 때는 배경만 어둡게 하고 라벨은 그대로 둠
     */
    [RequireComponent(typeof(Image))]
    public sealed class ButtonSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Role { Primary, Secondary, Danger }

        [SerializeField] Role role = Role.Secondary;

        // ArenaPanelGraphic (버튼 배경 프레임 클래스)
        // 있으면 Image는 투명한 클릭 영역으로 두고 이 프레임에 색을 입힘
        [SerializeField] ArenaPanelGraphic plate;

        Image image;
        Selectable selectable;
        bool lastOn;
        bool pressed;

        public void SetPlate(ArenaPanelGraphic value)
        {
            plate = value;
            Apply();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
            {
                pressed = true;
                Apply();
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData e)
        {
            pressed = false;
            Apply();
        }

        void OnDisable()
        {
            pressed = false;
        }

        void Awake()
        {
            image = GetComponent<Image>();
            selectable = GetComponent<Selectable>();
            Apply();
        }

        void OnEnable()
        {
            pressed = false;
            Apply();
        }

        // interactable이 바뀔 때만 색을 다시 적용함
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
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }

            lastOn = selectable == null || selectable.IsInteractable();

            if (lastOn == false)
            {
                pressed = false;
            }

            Color fill = lastOn ? RoleColor() : UiStyle.ButtonDisabled;

            // 누르는 동안 살짝 밝게
            if (pressed && lastOn)
            {
                fill = Color.Lerp(fill, UiStyle.TextPrimary, 0.12f);
            }

            if (plate != null)
            {
                image.color = Color.clear;
                Color edge = lastOn == false
                    ? UiStyle.BorderMuted
                    : role == Role.Primary ? UiStyle.BorderPrimary : UiStyle.Border;
                plate.Configure(fill, edge, false);
            }
            else
            {
                image.color = fill;
            }
        }

        Color RoleColor()
        {
            switch (role)
            {
                case Role.Primary:
                    return UiStyle.ButtonPrimary;
                case Role.Danger:
                    return UiStyle.ButtonDanger;
                default:
                    return UiStyle.ButtonSecondary;
            }
        }
    }
}
