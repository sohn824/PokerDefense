using System.Collections.Generic;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RoundScreen
     *
     * 드로우 -> 교체 -> 족보 표시 화면. RoundController를 구독만 하고 입력은 메서드로 넘김
     * 손패 크기가 상수 5라 카드 칸을 씬에 고정해 두고 생성하지 않음
     *
     * 교체 중에도 '지금 확정하면 나올 족보·유닛·유지 보너스'를 보여줌
     */
    public sealed class RoundScreen : MonoBehaviour
    {
        [SerializeField] RoundController controller;
        [SerializeField] StageController stage;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] CardView[] cardViews;
        [SerializeField] TMP_Text categoryLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Button exchangeButton;
        [SerializeField] TMP_Text exchangeLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;

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

        // 카드 조작 가능 여부, 버튼 상태, 안내 문구를 현재 상태에 맞추기
        void Refresh()
        {
            bool exchanging = phase == RoundPhase.Exchange;
            int selected = 0;

            for (int i = 0; i < cardViews.Length; i++)
            {
                // 잠긴 자리는 이번 라운드에 다시 못 바꾸므로 못 고르도록 함
                cardViews[i].SetInteractable(exchanging && controller.IsLocked(i) == false);

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
                confirmLabel.text = "확정";
                return;
            }

            ShowPreview();
        }

        // 프리뷰: 지금 확정하면 어떤 족보로 어떤 유닛이 나오고 유지 보너스 칩이 얼마인지 미리 보여줌
        void ShowPreview()
        {
            HandResult preview = controller.PreviewHand();
            UnitDefinition unit = unitTable.For(preview.Category);
            int bonus = stage.Economy.HoldBonusFor(controller.UsedExchanges);
            int left = controller.ExchangeableCount;

            categoryLabel.text = HandCategoryNames.Of(preview.Category);
            confirmLabel.text = bonus > 0 ? $"확정 +{bonus}" : "확정";

            statusLabel.text = left == 0
                ? $"확정하면 {unit.DisplayName} 소환 - 더 바꿀 카드가 없습니다"
                : $"확정하면 {unit.DisplayName} 소환 - 남은 교체 {left}칸 (자리당 1회)";
        }
    }
}
