using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PokerDefense.UI
{
    /**
     * ButtonSkin
     *
     * 버튼 역할(주 행동 / 보조 / 위험)에 맞는 공통 배경색을 입힌다
     * 비활성일 때는 배경만 어둡게 죽이고 라벨은 그대로 둬서 실행 불가 사유 문구가 계속 읽히게 한다
     */
    [RequireComponent(typeof(Image))]
    public sealed class ButtonSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Role { Primary, Secondary, Danger }

        [SerializeField] Role role = Role.Secondary;
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
                case Role.Primary: return UiStyle.ButtonPrimary;
                case Role.Danger: return UiStyle.ButtonDanger;
                default: return UiStyle.ButtonSecondary;
            }
        }
    }
}
