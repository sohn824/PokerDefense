using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * OnboardingGuide
     *
     * 첫 손패 · 배치 · 합치기  안내를 독립 팝업으로 표시한다.
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

        bool handShown;
        bool placeShown;
        bool mergeShown;
        bool showing;
        bool[] previousInput;

        void Awake()
        {
            cardHintRow.SetActive(false);
            cardHintClose.onClick.AddListener(Close);
        }

        void Update()
        {
            // 후보 선택이나 메뉴보다 먼저 끼어들지 않는다.
            if (showing || GameSession.IsPaused || GameSession.IsLoading || flow.IsFinished
                || inputGroups.Length == 0 || inputGroups[0].interactable == false)
            {
                return;
            }
            if (handShown == false && flow.RoundNumber == 1 && round.Phase == RoundPhase.Exchange)
            {
                handShown = true;
                Show("카드로 유닛을 소환하세요",
                    "<b>바꿀 카드를 선택 → 교체 → 손패 확정</b>\n\n5장의 포커 족보가 소환할 유닛을 정합니다.\n교체 비용 없이 원하는 족보를 노려보세요.\n\n매 라운드 <b>선택 교체 1회</b>로 마지막 한 장을 다듬으세요.", "손패 만들어 보기");
            }
            else if (placeShown == false && placement.Pending != null)
            {
                placeShown = true;
                Show("유닛을 전장에 놓으세요",
                    "<b>보드의 빈 칸을 누르세요.</b>\n\n배치한 유닛은 사거리 안에 들어온 적을\n자동으로 공격합니다.\n적이 지나가는 길과 사거리를 확인하세요.", "배치해 보기");
            }
            else if (mergeShown == false && HasMergePair())
            {
                mergeShown = true;
                Show("같은 유닛을 합쳐 강화하세요",
                    "<b>같은 유닛 + 같은 성급 = 한 단계 승급</b>\n\n유닛을 선택한 뒤 합칠 상대 칸을 누르세요.\n두 유닛이 더 강한 유닛 하나로 합쳐집니다.", "합쳐 보기");
            }
        }

        void Show(string title, string body, string action)
        {
            showing = true;
            previousInput = new bool[inputGroups.Length];
            for (int i = 0; i < inputGroups.Length; i++)
            {
                previousInput[i] = inputGroups[i].interactable;
                inputGroups[i].interactable = false;
            }
            GameSession.SetPaused(true);
            cardHintLabel.text = "<size=48><b>" + title + "</b></size>\n\n" + body;
            cardHintClose.GetComponentInChildren<TMP_Text>().text = action;
            cardHintRow.SetActive(true);
        }

        public void Close()
        {
            if (showing == false)
            {
                return;
            }
            showing = false;
            cardHintRow.SetActive(false);
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
