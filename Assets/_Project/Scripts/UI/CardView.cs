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
     * 손패 / 트레이 카드 한 장의 클래스
     * 프레임·하이라이트·무늬는 CardVisualSet 스프라이트 묶음으로 사용, rank는 텍스트로 씀
     *
     * 상태(CardState)는 색이 아니라 크기·테두리·아래쪽 배지 문구로 구분
     */
    public sealed class CardView : MonoBehaviour
    {
        public enum CardState
        {
            Normal,      // 평소
            Pick,        // 교체하려고 고른 손패 카드
            Locked,      // 이번 라운드에 이미 교체한 자리
            Placed,      // 트레이 카드를 놓은 자리
            Target,      // 트레이 카드를 고른 동안 놓을 수 있는 손패 자리
            TrayPick,    // 배치하려고 고른 트레이 카드 (상점에서 고른 카드에도 재사용)
            Bought       // 상점에서 이미 산 카드
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

        // OnExchange가 교체 대상만 모을 때 참조
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

        // 카드 아래쪽에 상태 문구를 띄우는 작은 배지를 코드로 붙인다
        void BuildBadge()
        {
            GameObject bg = new GameObject("Badge", typeof(RectTransform));
            RectTransform bgRT = (RectTransform)bg.transform;
            bgRT.SetParent(transform, false);
            bgRT.anchorMin = new Vector2(0f, 0f);
            bgRT.anchorMax = new Vector2(1f, 0f);
            bgRT.pivot = new Vector2(0.5f, 0f);
            bgRT.anchoredPosition = new Vector2(0f, 6f);
            bgRT.sizeDelta = new Vector2(-12f, 36f);

            badgeBg = bg.AddComponent<Image>();
            badgeBg.color = new Color(0.10f, 0.09f, 0.12f, 0.92f);
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
            badgeText.fontSize = 22f;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            badgeText.raycastTarget = false;

            bg.SetActive(false);
        }

        // RoundScreen의 Awake에서 호출 (CardVisualSet 연결)
        public void Bind(CardVisualSet set)
        {
            visuals = set;
        }

        public void Show(Card card)
        {
            rankLabel.text = CardText.RankOf(card.Rank);

            // 무늬 색은 이미지지만 rank는 텍스트이므로 무늬에 맞게 color 변경 처리
            rankLabel.color = CardText.IsRed(card.Suit) ? RedSuit : BlackSuit;
            suitIcon.sprite = visuals.SuitOf(card.Suit);
        }

        // 상태를 크기·투명도·테두리·배지 문구로 반영한다
        public void SetState(CardState next)
        {
            state = next;

            bool up = next == CardState.Pick || next == CardState.TrayPick;
            bool locked = next == CardState.Locked || next == CardState.Placed || next == CardState.Bought;

            transform.localScale = baseScale * (up ? PickScale : locked ? LockedScale : 1f);
            group.alpha = locked ? LockedAlpha : 1f;
            selectionFrame.enabled = up || next == CardState.Target;

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
                case CardState.Placed: return "배치 완료";
                case CardState.Target: return "여기 놓기";
                case CardState.TrayPick: return "선택";
                case CardState.Bought: return "구매 완료";
                default: return string.Empty;
            }
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
