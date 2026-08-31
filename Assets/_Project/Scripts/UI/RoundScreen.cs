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
        [SerializeField] CardVisualSet cardVisuals;
        [SerializeField] CardView[] cardViews;
        [SerializeField] TMP_Text categoryLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Button exchangeButton;
        [SerializeField] TMP_Text exchangeLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;

        [Tooltip("상점에서 산 보유 카드 Tray")]
        [SerializeField] GameObject heldTray;
        [SerializeField] CardView[] heldCardViews;

        RoundPhase phase;

        // 배치하려고 고른 트레이 카드 (null이면 평소의 교체-선택 모드)
        CardView armedHeld;

        void Awake()
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Bind(cardVisuals);
                cardViews[i].Clicked += OnCardClicked;
            }

            for (int i = 0; i < heldCardViews.Length; i++)
            {
                heldCardViews[i].Bind(cardVisuals);
                heldCardViews[i].Clicked += OnHeldClicked;
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

            RefreshTray();
            Refresh();
        }

        void ShowHand(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Show(hand[i]);
                cardViews[i].SetSelected(false);
            }

            RefreshTray();
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
            // 트레이 카드를 고른 상태면 이 손패 칸에 놓는다 (교체 선택 토글이 아니라)
            if (armedHeld != null)
            {
                PlaceArmedOn(card);
                return;
            }

            card.SetSelected(!card.Selected);
            Refresh();
        }

        // 트레이 카드 탭 - 배치할 카드를 고르거나(무장) 다시 눌러 해제
        void OnHeldClicked(CardView trayCard)
        {
            if (armedHeld == trayCard)
            {
                armedHeld = null;
            }
            else
            {
                // 배치 모드로 들어가면 교체 선택은 버린다
                for (int i = 0; i < cardViews.Length; i++)
                {
                    cardViews[i].SetSelected(false);
                }

                armedHeld = trayCard;
            }

            RefreshTray();
            Refresh();
        }

        void PlaceArmedOn(CardView handCard)
        {
            int slot = System.Array.IndexOf(cardViews, handCard);
            int trayIndex = System.Array.IndexOf(heldCardViews, armedHeld);

            if (slot < 0 || controller.IsLocked(slot) || trayIndex < 0 || trayIndex >= stage.HeldCards.Count)
            {
                return;
            }

            Card held = stage.HeldCards[trayIndex];

            armedHeld = null;
            stage.RemoveHeldCard(held);
            // HandChanged -> ShowHand 가 RefreshTray + Refresh 를 부른다
            controller.PlaceHeldCard(slot, held);
        }

        // 보유 카드 트레이를 현재 상태에 맞춘다
        void RefreshTray()
        {
            IReadOnlyList<Card> held = stage.HeldCards;
            bool show = phase == RoundPhase.Exchange && held.Count > 0;

            heldTray.SetActive(show);

            if (show == false)
            {
                armedHeld = null;
            }

            for (int i = 0; i < heldCardViews.Length; i++)
            {
                bool filled = show && i < held.Count;
                heldCardViews[i].gameObject.SetActive(filled);

                if (filled == false)
                {
                    continue;
                }

                heldCardViews[i].Show(held[i]);
                heldCardViews[i].SetSelected(heldCardViews[i] == armedHeld);
                heldCardViews[i].SetInteractable(true);
            }
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
            UnitDefinition unit = unitTable.GetDefinition(preview.Category);
            int bonus = stage.HoldBonusFor(controller.UsedExchanges);
            int left = controller.ExchangeableCount;

            categoryLabel.text = HandCategoryNames.Of(preview.Category);
            confirmLabel.text = bonus > 0 ? $"확정 +{bonus}" : "확정";

            if (armedHeld != null)
            {
                statusLabel.text = "손패 칸을 눌러 카드를 놓으세요 (교체 아님)";
                return;
            }

            statusLabel.text = left == 0
                ? $"확정하면 {unit.DisplayName} 소환 - 더 바꿀 카드가 없습니다"
                : $"확정하면 {unit.DisplayName} 소환 - 남은 교체 {left}칸 (자리당 1회)";
        }
    }
}
