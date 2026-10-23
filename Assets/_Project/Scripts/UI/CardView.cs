using System;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * CardView
     *
     * 손패 카드 한 장을 그리고, 눌리면 RoundScreen에 알리는 클래스
     * 무늬는 CardVisualSet의 스프라이트로, 숫자(rank)는 텍스트로 그림
     *
     * 상태(CardState)는 색이 아니라 크기·테두리·아래쪽 배지 문구로 구분
     */
    public sealed class CardView : MonoBehaviour
    {
        public enum CardState
        {
            // 평소
            Normal,

            // 교체하려고 고른 손패 카드
            Pick,

            // 이번 라운드에 이미 교체한 자리
            Locked
        }

        static readonly Color RedSuit = new Color(0.80f, 0.15f, 0.15f);
        static readonly Color BlackSuit = new Color(0.12f, 0.12f, 0.14f);

        const float PickScale = 1.08f;
        const float LockedScale = 0.92f;
        const float LockedAlpha = 0.5f;

        [SerializeField] TMP_Text rankLabel;
        [SerializeField] Image suitIcon;
        [SerializeField] Image selectionFrame;
        [SerializeField] Button button;

        public event Action<CardView> Clicked;

        // 교체 대상으로 골라져 있는지 (RoundScreen이 고른 자리를 모을 때 씀)
        public bool Selected => state == CardState.Pick;

        CardVisualSet visuals;
        CardState state = CardState.Normal;
        Vector3 baseScale;
        CanvasGroup group;
        Image badgeBg;
        TMP_Text badgeText;

        void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
            baseScale = transform.localScale;

            group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            BuildBadge();
        }

        // 카드 아래쪽에 상태 문구를 띄우는 작은 배지를 코드로 붙임
        void BuildBadge()
        {
            GameObject bg = new GameObject("Badge", typeof(RectTransform));
            RectTransform bgRT = (RectTransform)bg.transform;
            bgRT.SetParent(transform, false);
            bgRT.anchorMin = new Vector2(0f, 0f);
            bgRT.anchorMax = new Vector2(1f, 0f);
            bgRT.pivot = new Vector2(0.5f, 0f);
            bgRT.anchoredPosition = new Vector2(0f, UiStyle.Unit);
            bgRT.sizeDelta = new Vector2(-12f, 44f);

            badgeBg = bg.AddComponent<Image>();
            badgeBg.color = UiStyle.Plate;
            badgeBg.raycastTarget = false;

            GameObject text = new GameObject("Text", typeof(RectTransform));
            RectTransform textRT = (RectTransform)text.transform;
            textRT.SetParent(bgRT, false);
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            badgeText = text.AddComponent<TextMeshProUGUI>();
            badgeText.font = rankLabel.font;
            badgeText.fontSize = UiStyle.CaptionSize;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = UiStyle.TextPrimary;
            badgeText.raycastTarget = false;

            bg.SetActive(false);
        }

        // 무늬 스프라이트를 꺼낼 CardVisualSet을 연결함 (RoundScreen.Awake가 부름)
        public void Bind(CardVisualSet set)
        {
            visuals = set;
        }

        public void Show(Card card)
        {
            rankLabel.text = CardText.RankOf(card.Rank);

            // 무늬는 이미지에 색이 들어 있지만 숫자는 텍스트라 무늬 색(빨강·검정)에 맞춰 글자색을 바꿈
            rankLabel.color = CardText.IsRed(card.Suit) ? RedSuit : BlackSuit;
            suitIcon.sprite = visuals.SuitOf(card.Suit);
        }

        // 상태를 크기·투명도·테두리·배지 문구로 반영함
        public void SetState(CardState next)
        {
            state = next;

            bool up = next == CardState.Pick;
            bool locked = next == CardState.Locked;

            transform.localScale = baseScale * (up ? PickScale : locked ? LockedScale : 1f);
            group.alpha = locked ? LockedAlpha : 1f;
            selectionFrame.enabled = up;

            string badge = BadgeFor(next);
            badgeBg.gameObject.SetActive(badge.Length > 0);
            badgeText.text = badge;
        }

        static string BadgeFor(CardState s)
        {
            switch (s)
            {
                case CardState.Pick: return "교체";
                case CardState.Locked: return "교체 완료";
                default: return string.Empty;
            }
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
