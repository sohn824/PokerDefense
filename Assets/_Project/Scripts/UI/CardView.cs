using System;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /// <summary>
    /// 손패 카드 한 장. 플레이스홀더 표시다 — 무늬는 기호(♠) 대신 문자(S/H/D/C)를 쓴다.
    /// TMP 기본 폰트에 카드 심볼 글리프가 없어 기호를 쓰면 두부가 나온다. M6에서 스프라이트로 교체한다.
    /// </summary>
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
            rankLabel.text = RankText(card.Rank);
            suitLabel.text = SuitText(card.Suit);

            Color color = IsRed(card.Suit) ? RedSuit : BlackSuit;
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

        static bool IsRed(Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;

        static string RankText(Rank rank) => rank switch
        {
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)rank).ToString(),
        };

        static string SuitText(Suit suit) => suit switch
        {
            Suit.Spade => "S",
            Suit.Heart => "H",
            Suit.Diamond => "D",
            _ => "C",
        };
    }
}
