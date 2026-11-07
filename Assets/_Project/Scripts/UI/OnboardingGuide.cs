using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * OnboardingGuide
     *
     * 첫 손패·배치·합치기 안내를 팝업으로 한 번씩 띄우는 클래스
     * 안내가 떠 있는 동안 입력을 끄고 일시정지함
     * 확인한 안내는 PlayerPrefs에 저장해 다음 Run부터 띄우지 않음
     */
    public sealed class OnboardingGuide : MonoBehaviour
    {
        const string Prefix = "PokerDefense.Onboarding.";
        const string HandKey = "Hand";
        const string PlaceKey = "Place";
        const string MergeKey = "Merge";

        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] GameFlowController flow;
        [SerializeField] CanvasGroup[] inputGroups;
        [SerializeField] GameObject cardHintRow;
        [SerializeField] TMP_Text cardHintLabel;
        [SerializeField] Button cardHintClose;

        bool handShown;
        bool placeShown;
        bool mergeShown;
        bool showing;
        bool[] previousInput;

        // 지금 띄운 안내의 저장 키
        string showingKey;

        void Awake()
        {
            cardHintRow.SetActive(false);
            cardHintClose.onClick.AddListener(Confirm);
            handShown = IsSeen(HandKey);
            placeShown = IsSeen(PlaceKey);
            mergeShown = IsSeen(MergeKey);
        }

        static bool IsSeen(string key) => PlayerPrefs.GetInt(Prefix + key, 0) == 1;

        void Update()
        {
            // 다른 창이 열려 있으면 띄우지 않음
            if (showing || GameSession.IsPaused || GameSession.IsLoading || flow.IsFinished
                || inputGroups.Length == 0 || inputGroups[0].interactable == false)
            {
                return;
            }
            if (handShown == false && flow.RoundNumber == 1 && round.Phase == RoundPhase.Exchange)
            {
                handShown = true;
                Show(HandKey, "카드로 유닛을 소환하세요",
                    "<b>바꿀 카드를 선택 → 교체 → 손패 확정</b>\n\n5장의 포커 족보가 소환할 유닛을 정합니다.\n원하는 족보를 노려보세요.\n\n매 라운드 <b>선택 교체 1회</b>로 마지막 한 장을 다듬으세요.", "손패 만들어 보기");
            }
            else if (placeShown == false && placement.Pending != null)
            {
                placeShown = true;
                Show(PlaceKey, "유닛을 전장에 놓으세요",
                    "<b>보드의 빈 칸을 누르세요.</b>\n\n배치한 유닛은 사거리 안에 들어온 적을\n자동으로 공격합니다.\n적이 지나가는 길과 사거리를 확인하세요.", "배치해 보기");
            }
            else if (mergeShown == false && HasMergePair())
            {
                mergeShown = true;
                Show(MergeKey, "같은 유닛을 합쳐 강화하세요",
                    "<b>같은 유닛 + 같은 성급 = 한 단계 승급</b>\n\n유닛을 선택한 뒤 합칠 상대 칸을 누르세요.\n두 유닛이 더 강한 유닛 하나로 합쳐집니다.", "합쳐 보기");
            }
        }

        // 입력을 끄고 일시정지한 뒤 안내 표시
        void Show(string key, string title, string body, string action)
        {
            showing = true;
            showingKey = key;
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

        // 확인 버튼 - 이미 본 안내로 저장한 뒤 닫음
        void Confirm()
        {
            if (showing == false)
            {
                return;
            }
            PlayerPrefs.SetInt(Prefix + showingKey, 1);
            PlayerPrefs.Save();
            Close();
        }

        // 닫고 입력/일시정지를 되돌림
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
