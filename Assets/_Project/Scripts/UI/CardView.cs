using System;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * CardView
     *
     * 손패 카드 한 장. 플레이스홀더 표시이며 M6에서 스프라이트로 교체한다
     */
    public sealed class CardView : MonoBehaviour
    {
        static readonly Color RedSuit = new Color(0.80f, 0.15f, 0.15f);
        static readonly Color BlackSuit = new Color(0.12f, 0.12f, 0.14f);

        [SerializeField] TMP_Text rankLabel;
        [SerializeField] TMP_Text suitLabel;
        [SerializeField] Image selectionFrame;
        [SerializeField] Button button;

        public event Action<CardView> Clicked;

        public bool Selected { get; private set; }

        void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Show(Card card)
        {
            rankLabel.text = CardText.RankOf(card.Rank);
            suitLabel.text = CardText.SuitOf(card.Suit);

            Color color = CardText.IsRed(card.Suit) ? RedSuit : BlackSuit;
            rankLabel.color = color;
            suitLabel.color = color;
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
