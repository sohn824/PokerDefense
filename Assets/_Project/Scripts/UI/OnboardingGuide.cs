using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * OnboardingGuide
     *
     * 첫 손패 · 배치 · 합치기 · 상점 안내를 독립 팝업으로 표시한다.
     * 읽는 동안 진행과 뒤쪽 입력을 멈추고, 확인한 안내는 이번 런에서 반복하지 않는다.
     */
    public sealed class OnboardingGuide : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] CanvasGroup[] inputGroups;
        [SerializeField] GameObject cardHintRow;
        [SerializeField] TMP_Text cardHintLabel;
        [SerializeField] Button cardHintClose;
        [SerializeField] GameObject shopHintRow;
        [SerializeField] TMP_Text shopHintLabel;
        [SerializeField] Button shopHintClose;

        bool handShown;
        bool placeShown;
        bool mergeShown;
        bool shopShown;
        bool showing;
        bool[] previousInput;

        void Awake()
        {
            cardHintRow.SetActive(false);
            shopHintRow.SetActive(false);
            cardHintClose.onClick.AddListener(Close);
            shopHintClose.onClick.AddListener(Close);
        }

        void Update()
        {
            // 후보 선택이나 메뉴보다 먼저 끼어들지 않는다.
            if (showing || GameSession.IsPaused || GameSession.IsLoading || flow.IsFinished
                || inputGroups.Length == 0 || inputGroups[0].interactable == false)
            {
                return;
            }
            if (flow.ShopCards != null)
            {
                if (shopShown == false)
                {
                    shopShown = true;
                    Show(true, "다음 손패를 준비하세요",
                        "카드를 누르면 정보를 확인합니다.\n마음에 들면 <b>구매 버튼</b>을 누르세요.\n\n산 카드는 다음 손패에 직접 놓을 수 있고,\n유지 보너스도 줄지 않습니다.", "상점 둘러보기");
                }
            }
            else if (handShown == false && flow.RoundNumber == 1 && round.Phase == RoundPhase.Exchange)
            {
                handShown = true;
                Show(false, "카드로 유닛을 소환하세요",
                    "<b>바꿀 카드를 선택 → 교체 → 손패 확정</b>\n\n5장의 포커 족보가 소환할 유닛을 정합니다.\n교체를 아끼면 보너스 Chip을 받습니다.\n\n한 장이 아쉽다면 <b>추가 교체</b>도 확인해 보세요.", "손패 만들어 보기");
            }
            else if (placeShown == false && placement.Pending != null)
            {
                placeShown = true;
                Show(false, "유닛을 전장에 놓으세요",
                    "<b>보드의 빈 칸을 누르세요.</b>\n\n배치한 유닛은 사거리 안에 들어온 적을\n자동으로 공격합니다.\n적이 지나가는 길과 사거리를 확인하세요.", "배치해 보기");
            }
            else if (mergeShown == false && HasMergePair())
            {
                mergeShown = true;
                Show(false, "같은 유닛을 합쳐 강화하세요",
                    "<b>같은 유닛 + 같은 성급 = 한 단계 승급</b>\n\n유닛을 선택한 뒤 합칠 상대 칸을 누르세요.\n두 유닛이 더 강한 유닛 하나로 합쳐집니다.", "합쳐 보기");
            }
        }

        void Show(bool shop, string title, string body, string action)
        {
            showing = true;
            previousInput = new bool[inputGroups.Length];
            for (int i = 0; i < inputGroups.Length; i++)
            {
                previousInput[i] = inputGroups[i].interactable;
                inputGroups[i].interactable = false;
            }
            GameSession.SetPaused(true);
            (shop ? shopHintLabel : cardHintLabel).text = "<size=48><b>" + title + "</b></size>\n\n" + body;
            (shop ? shopHintClose : cardHintClose).GetComponentInChildren<TMP_Text>().text = action;
            (shop ? shopHintRow : cardHintRow).SetActive(true);
        }

        public void Close()
        {
            if (showing == false)
            {
                return;
            }
            showing = false;
            cardHintRow.SetActive(false);
            shopHintRow.SetActive(false);
            for (int i = 0; i < inputGroups.Length; i++)
            {
                if (inputGroups[i] != null)
                {
                    inputGroups[i].interactable = previousInput[i];
                }
            }
            if (GameSession.IsLoading == false)
            {
                GameSession.SetPaused(false);
            }
        }

        void OnDisable()
        {
            Close();
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
