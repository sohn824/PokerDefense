using System.Collections.Generic;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /// <summary>
    /// 드로우 -> 교체 -> 족보 표시 화면. RoundController를 구독만 하고 입력은 메서드 호출로 넘긴다.
    /// 손패 크기가 상수 5라 카드 칸을 씬에 고정해 두고 생성하지 않는다.
    ///
    /// 교체는 여러 번 할 수 있고 한 번 바꾼 자리는 잠긴다. 그래서 "교체"와 "확정"이 별개 버튼이다.
    /// </summary>
    public sealed class RoundScreen : MonoBehaviour
    {
        [SerializeField] RoundController controller;
        [SerializeField] CardView[] cardViews;
        [SerializeField] TMP_Text categoryLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Button exchangeButton;
        [SerializeField] TMP_Text exchangeLabel;
        [SerializeField] Button confirmButton;

        RoundPhase phase;

        void Awake()
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Clicked += OnCardClicked;
            }

            exchangeButton.onClick.AddListener(OnExchange);
            confirmButton.onClick.AddListener(controller.ConfirmHand);

            controller.PhaseChanged += ShowPhase;
            controller.HandChanged += ShowHand;
            controller.Evaluated += ShowResult;
        }

        void ShowPhase(RoundPhase newPhase)
        {
            phase = newPhase;

            if (phase == RoundPhase.Exchange)
            {
                categoryLabel.text = string.Empty;
            }

            Refresh();
        }

        void ShowHand(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Show(hand[i]);
                cardViews[i].SetSelected(false);
            }

            Refresh();
        }

        void ShowResult(HandResult result)
        {
            categoryLabel.text = HandCategoryNames.Of(result.Category);

            var keyCards = new System.Text.StringBuilder("키카드");

            for (int i = 0; i < result.KeyCards.Count; i++)
            {
                keyCards.Append(' ').Append(CardText.Of(result.KeyCards[i]));
            }

            statusLabel.text = keyCards.ToString();
        }

        void OnCardClicked(CardView card)
        {
            card.SetSelected(!card.Selected);
            Refresh();
        }

        void OnExchange()
        {
            var indices = new List<int>();

            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i].Selected)
                {
                    indices.Add(i);
                }
            }

            controller.ExchangeCards(indices);
        }

        /// <summary>카드 조작 가능 여부, 버튼 상태, 안내 문구를 현재 상태에 맞춘다.</summary>
        void Refresh()
        {
            bool exchanging = phase == RoundPhase.Exchange;
            int selected = 0;

            for (int i = 0; i < cardViews.Length; i++)
            {
                // 잠긴 자리는 이번 라운드에 다시 못 바꾸므로 고를 수도 없다.
                cardViews[i].SetInteractable(exchanging && !controller.IsLocked(i));

                if (cardViews[i].Selected)
                {
                    selected++;
                }
            }

            exchangeButton.interactable = exchanging && selected > 0;
            confirmButton.interactable = exchanging;
            exchangeLabel.text = selected == 0 ? "교체" : $"교체 {selected}장";

            if (!exchanging)
            {
                return;
            }

            int left = controller.ExchangeableCount;
            statusLabel.text = left == 0
                ? "더 바꿀 카드가 없습니다 - 확정하세요"
                : $"카드를 눌러 교체하세요 (남은 자리 {left}칸, 자리당 1회)";
        }
    }
}
