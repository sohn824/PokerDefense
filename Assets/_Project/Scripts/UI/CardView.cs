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
     * 손패 카드 한 장의 클래스
     * 프레임·하이라이트·무늬는 CardVisualSet 스프라이트 묶음으로 사용
     * rank는 텍스트로 씀
     */
    public sealed class CardView : MonoBehaviour
    {
        static readonly Color RedSuit = new Color(0.80f, 0.15f, 0.15f);
        static readonly Color BlackSuit = new Color(0.12f, 0.12f, 0.14f);

        [SerializeField] TMP_Text rankLabel;
        [SerializeField] Image suitIcon;
        [SerializeField] Image selectionFrame;
        [SerializeField] Button button;

        public event Action<CardView> Clicked;

        public bool Selected { get; private set; }

        CardVisualSet visuals;

        void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
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

        public void SetSelected(bool selected)
        {
            Selected = selected;
            selectionFrame.enabled = selected;
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }

    }
}
