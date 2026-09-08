using System.Collections.Generic;
using System.Text;
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
     * 진열된 4장 중 한 장을 고른 뒤 하단 구매 버튼으로 산다 (탭만으로는 소비하지 않음)
     * 산 카드는 보유 카드가 되어 교체 단계에서 손패에 놓을 수 있다
     */
    public sealed class ShopScreen : MonoBehaviour
    {
        const int NoSelection = -1;

        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] CardVisualSet cardVisuals;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text infoLabel;
        [SerializeField] TMP_Text heldLabel;
        [SerializeField] CardView[] cardSlots;
        [SerializeField] Button buyButton;
        [SerializeField] TMP_Text buyLabel;
        [SerializeField] Button closeButton;

        IReadOnlyList<Card> offer;
        bool[] bought;
        int selectedCard = NoSelection;

        void Awake()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                int index = i;
                cardSlots[i].Bind(cardVisuals);
                cardSlots[i].Clicked += _ => Select(index);
            }

            buyButton.onClick.AddListener(OnBuy);
            closeButton.onClick.AddListener(Close);

            flow.ShopOpened += Show;

            panel.SetActive(false);
        }

        void Show(IReadOnlyList<Card> cards)
        {
            offer = cards;
            bought = new bool[cardSlots.Length];
            selectedCard = NoSelection;
            panel.SetActive(true);
            Render();
        }

        // 카드 탭은 선택/해제일 뿐 - 구매는 하단 버튼으로만 (이미 산 카드는 못 고른다)
        void Select(int index)
        {
            if (offer == null || index >= offer.Count || bought[index])
            {
                return;
            }

            selectedCard = selectedCard == index ? NoSelection : index;
            Render();
        }

        void OnBuy()
        {
            if (selectedCard == NoSelection)
            {
                return;
            }

            if (stage.TryBuyShopCard(offer[selectedCard]))
            {
                bought[selectedCard] = true;
                // 구매 후 선택을 풀어 연타가 다음 카드 구매로 이어지지 않게 한다
                selectedCard = NoSelection;
                Render();
            }
        }

        void Render()
        {
            int price = stage.Economy.ShopCardPrice;
            int capacity = stage.Economy.HeldCardCapacity;
            int chip = stage.Stage.Chip;

            infoLabel.text = $"Chip {chip}    보유 {stage.Stage.HeldCards.Count}/{capacity}    카드 {price} Chip";

            // 실행 불가 사유는 회색만이 아니라 문구로. 선택·정보 확인은 막지 않고 구매만 막는다
            string block = stage.Stage.CanHoldMoreCards == false ? "보유 카드 가득"
                : chip < price ? "Chip 부족"
                : null;

            if (block != null)
            {
                infoLabel.text += $"    · {block}";
            }

            heldLabel.text = HeldCardsText(capacity);

            for (int i = 0; i < cardSlots.Length; i++)
            {
                bool filled = offer != null && i < offer.Count;
                cardSlots[i].gameObject.SetActive(filled);

                if (filled == false)
                {
                    continue;
                }

                cardSlots[i].Show(offer[i]);
                cardSlots[i].SetState(bought[i] ? CardView.CardState.Bought
                    : i == selectedCard ? CardView.CardState.TrayPick
                    : CardView.CardState.Normal);
                cardSlots[i].SetInteractable(bought[i] == false);
            }

            bool canBuy = selectedCard != NoSelection && block == null;
            buyButton.interactable = canBuy;
            buyLabel.text = selectedCard == NoSelection ? "카드를 고르세요"
                : block != null ? block
                : $"선택 카드 구매 · {price} Chip";
        }

        // 현재 보유 카드의 실제 숫자·무늬 + 빈 칸. 다음 손패는 아직 안 뽑혔으므로 추천은 하지 않는다
        string HeldCardsText(int capacity)
        {
            IReadOnlyList<Card> held = stage.Stage.HeldCards;

            if (held.Count == 0)
            {
                return $"보유 카드 없음  (빈 칸 {capacity})";
            }

            var text = new StringBuilder("보유 카드");

            for (int i = 0; i < held.Count; i++)
            {
                text.Append(' ').Append(CardText.Of(held[i]));
            }

            int empty = capacity - held.Count;

            if (empty > 0)
            {
                text.Append($"  (빈 칸 {empty})");
            }

            return text.ToString();
        }

        void Close()
        {
            // 패널을 먼저 닫는다. CloseShop이 다음 라운드를 열면서 화면 전체를 다시 그린다
            offer = null;
            selectedCard = NoSelection;
            panel.SetActive(false);
            flow.CloseShop();
        }
    }
}
