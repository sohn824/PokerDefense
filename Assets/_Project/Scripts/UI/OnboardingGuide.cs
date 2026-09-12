using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * OnboardingGuide
     *
     * 강제 튜토리얼 대신 첫 손패 · 첫 배치 대기 · 첫 머지 기회 · 첫 상점, 네 순간에만
     * 기존 화면 위에 한 줄 안내를 띄운다. 족보·확률·가격·보상은 건드리지 않는다
     * 각 안내는 세션 동안 한 번만 뜨고, 자연스러운 종료(확정 · 배치 · 머지 · 구매 또는 닫기)로 사라진다
     */
    public sealed class OnboardingGuide : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;

        [Tooltip("상황 카드에 얹는 안내줄 (손패 · 배치 대기 · 머지, 셋이 겹치지 않아 하나로 공용)")]
        [SerializeField] GameObject cardHintRow;
        [SerializeField] TMP_Text cardHintLabel;
        [SerializeField] Button cardHintClose;

        [Tooltip("상점 패널에 얹는 안내줄")]
        [SerializeField] GameObject shopHintRow;
        [SerializeField] TMP_Text shopHintLabel;
        [SerializeField] Button shopHintClose;

        bool handClosed;
        bool placeClosed;
        bool mergeClosed;
        bool shopClosed;

        bool shopWasOpen;
        int heldCountAtShopOpen;

        void Awake()
        {
            cardHintClose.onClick.AddListener(CloseCardHint);
            shopHintClose.onClick.AddListener(() => shopClosed = true);

            placement.Placed += OnPlaced;
        }

        // 실제로 놓였을 때만 "첫 배치"가 끝나고, 진짜 머지(조커 승급 포함)일 때만 "첫 머지"가 끝난다
        void OnPlaced(int index, PlacementResult result)
        {
            if (result == PlacementResult.Placed || result == PlacementResult.Merged)
            {
                placeClosed = true;
            }

            if (result == PlacementResult.Merged)
            {
                mergeClosed = true;
            }
        }

        // 지금 보이는 안내를 닫는다 (아래 Update와 같은 우선순위로 판정)
        void CloseCardHint()
        {
            if (IsHandHintActive())
            {
                handClosed = true;
            }
            else if (IsPlaceHintActive())
            {
                placeClosed = true;
            }
            else if (IsMergeHintActive())
            {
                mergeClosed = true;
            }
        }

        bool IsHandHintActive() => handClosed == false && flow.RoundNumber == 1 && round.Phase == RoundPhase.Exchange;

        bool IsPlaceHintActive() => placeClosed == false && placement.Pending != null;

        bool IsMergeHintActive() => mergeClosed == false && HasMergePair();

        void Update()
        {
            UpdateCardHint();
            UpdateShopHint();
        }

        void UpdateCardHint()
        {
            string text = IsHandHintActive()
                ? "안내 · 포커 족보가 소환할 유닛을 정합니다 · 적게 교체할수록 유지 보너스(Chip)가 커집니다  · 닫기"
                : IsPlaceHintActive()
                    ? "안내 · 빈 칸을 눌러 배치하면 사거리 안의 적을 자동으로 공격합니다  · 닫기"
                    : IsMergeHintActive()
                        ? "안내 · 같은 유닛 · 같은 성급 칸이 있습니다 · 겹치면 다음 성급으로 합쳐집니다  · 닫기"
                        : null;

            bool show = text != null;

            if (cardHintRow.activeSelf != show)
            {
                cardHintRow.SetActive(show);
            }

            if (show)
            {
                cardHintLabel.text = text;
            }
        }

        void UpdateShopHint()
        {
            bool shopOpen = flow.ShopCards != null;

            if (shopOpen && shopWasOpen == false)
            {
                heldCountAtShopOpen = stage.Stage.HeldCards.Count;
            }

            if (shopClosed == false && shopOpen && stage.Stage.HeldCards.Count > heldCountAtShopOpen)
            {
                // 첫 구매가 일어났으므로 여기서 끝
                shopClosed = true;
            }

            if (shopWasOpen && shopOpen == false && shopClosed == false)
            {
                // 구매 없이 상점을 닫음 - 다음 상점부터는 안내하지 않는다
                shopClosed = true;
            }

            shopWasOpen = shopOpen;

            bool show = shopClosed == false && shopOpen;

            if (shopHintRow.activeSelf != show)
            {
                shopHintRow.SetActive(show);
            }

            if (show)
            {
                shopHintLabel.text = "안내 · 카드를 눌러 확인만 하고 구매는 아래 버튼으로 · 산 카드는 다음 교체 단계에서 손패에 놓입니다  · 닫기";
            }
        }

        bool HasMergePair()
        {
            GridBoard board = placement.Board;

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                if (board[i] != null && board.HasMergePartner(i))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
