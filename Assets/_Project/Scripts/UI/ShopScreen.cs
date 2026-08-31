using System.Collections.Generic;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ShopScreen
     *
     * 5웨이브마다 여는 보너스 카드 상점
     *
     * 진열된 4장 중 원하는 만큼 Chip을 주고 산다.
     * 산 카드는 보유 카드가 되어 교체 단계에서 손패에 놓을 수 있다.
     */
    public sealed class ShopScreen : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] CardVisualSet cardVisuals;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text infoLabel;
        [SerializeField] CardView[] cardSlots;
        [SerializeField] Button closeButton;

        IReadOnlyList<Card> offer;
        bool[] bought;

        void Awake()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                int index = i;
                cardSlots[i].Bind(cardVisuals);
                cardSlots[i].Clicked += _ => Buy(index);
            }

            closeButton.onClick.AddListener(Close);

            flow.ShopOpened += Show;

            panel.SetActive(false);
        }

        void Show(IReadOnlyList<Card> cards)
        {
            offer = cards;
            bought = new bool[cardSlots.Length];
            panel.SetActive(true);
            Render();
        }

        void Render()
        {
            int price = stage.Economy.ShopCardPrice;
            int capacity = stage.Economy.HeldCardCapacity;

            infoLabel.text =
                $"Chip {stage.Stage.Chip}    보유 {stage.Stage.HeldCards.Count}/{capacity}    카드 {price} Chip";

            for (int i = 0; i < cardSlots.Length; i++)
            {
                bool filled = offer != null && i < offer.Count;
                cardSlots[i].gameObject.SetActive(filled);

                if (filled == false)
                {
                    continue;
                }

                cardSlots[i].Show(offer[i]);

                bool canBuy = bought[i] == false
                              && stage.Stage.CanHoldMoreCards
                              && stage.Stage.Chip >= price;

                cardSlots[i].SetInteractable(canBuy);
                // 산 카드는 선택 프레임으로 표시해 둔다
                cardSlots[i].SetSelected(bought[i]);
            }
        }

        void Buy(int index)
        {
            if (offer == null || index >= offer.Count || bought[index])
            {
                return;
            }

            if (stage.TryBuyShopCard(offer[index]))
            {
                bought[index] = true;
                Render();
            }
        }

        void Close()
        {
            // 패널을 먼저 닫는다
            // CloseShop이 다음 라운드를 열면서 화면 전체를 다시 그린다
            offer = null;
            panel.SetActive(false);
            flow.CloseShop();
        }
    }
}
