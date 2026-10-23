using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * AssistScreen
     *
     * 선택 교체 창 - 다시 바꿀 카드 고르기 -> 후보 3장 공개 -> 한 장 골라 교체
     * 화면 표시와 입력 전달만 하고, 손패·덱은 RoundController만 바꿈
     * 창이 열린 동안 게임 입력을 막고, 후보를 공개한 뒤에는 한 장을 고를 때까지 닫을 수 없음
     */
    public sealed class AssistScreen : MonoBehaviour
    {
        // 창이 열린 동안 막는 게임 입력과, 열기 전 상태(닫을 때 되돌림)
        [SerializeField] CanvasGroup gameInput;
        bool previousInput;

        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] GameFlowController flow;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] Button openButton;
        [SerializeField] TMP_Text openLabel;
        [SerializeField] GameObject panel;
        [SerializeField] Button[] slots;
        [SerializeField] Button[] candidates;
        [SerializeField] Button revealButton;
        [SerializeField] Button chooseButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text instruction;
        [SerializeField] TMP_Text preview;
        [SerializeField] TMP_Text oddsLabel;
        [SerializeField] Button oddsButton;
        [SerializeField] HandOddsScreen oddsScreen;

        // 다시 바꿀 손패 자리 (-1 = 아직 안 고름)
        int target = -1;

        // 고른 후보 번호 (-1 = 아직 안 고름)
        int selected = -1;

        void Awake()
        {
            panel.SetActive(false);
            round.OddsChanged += OnOddsChanged;
            openButton.onClick.AddListener(Open);
            oddsButton.onClick.AddListener(() => oddsScreen.Open("선택 교체 · 한 장 기준", true));
            cancelButton.onClick.AddListener(Close);
            revealButton.onClick.AddListener(Reveal);
            chooseButton.onClick.AddListener(Choose);
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() => { target = index; Refresh(); });
            }
            for (int i = 0; i < candidates.Length; i++)
            {
                int index = i;
                candidates[i].onClick.AddListener(() => { selected = index; Refresh(); });
            }
        }

        void OnOddsChanged()
        {
            if (isActiveAndEnabled && panel.activeSelf)
            {
                Refresh();
            }
        }

        void OnDisable()
        {
            round.CancelOdds(HandOddsSource.Assist);
        }

        void OnEnable()
        {
            if (panel.activeSelf)
            {
                Refresh();
            }
        }

        void OnDestroy() => round.OddsChanged -= OnOddsChanged;

        // 매 프레임 열기 버튼의 표시·문구·눌림 여부를 맞추고, 교체 단계가 끝나면 창을 닫음
        void Update()
        {
            bool available = round.Phase == RoundPhase.Exchange && flow.IsFinished == false;
            openButton.gameObject.SetActive(available);
            bool eligible = HasTarget();
            string reason = round.AssistUsed ? "이번 손패 사용 완료"
                : eligible == false ? "일반 교체 후 사용" : "이번 손패 1회";
            openLabel.text = "선택 교체\n<size=30>" + reason + "</size>";
            openButton.interactable = available && panel.activeSelf == false
                && round.AssistUsed == false && eligible;
            if (available == false)
            {
                Close();
            }
        }

        // 선택 교체 창을 엶 (일시정지·교체 단계 아님·이미 사용·바꿀 카드 없음이면 무시)
        public void Open()
        {
            if (GameSession.IsPaused || panel.activeSelf || round.Phase != RoundPhase.Exchange
                || flow.IsFinished || round.AssistUsed || HasTarget() == false)
            {
                return;
            }
            target = -1;
            // 다시 바꿀 수 있는 카드가 하나뿐이면 미리 골라 둠
            int targets = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (round.CanAssist(i))
                {
                    target = i;
                    targets++;
                }
            }
            if (targets != 1)
            {
                target = -1;
            }
            selected = -1;
            previousInput = gameInput.interactable;
            gameInput.interactable = false;
            panel.SetActive(true);
            round.SetAssistOddsActive(true);
            Refresh();
        }

        // 다시 바꿀 수 있는 손패 자리가 하나라도 있는지
        bool HasTarget()
        {
            for (int i = 0; i < RoundContext.HandSize; i++)
            {
                if (round.CanAssist(i))
                {
                    return true;
                }
            }
            return false;
        }

        // 창을 닫고 막아 둔 입력을 되돌림 (후보를 공개하고 아직 안 골랐으면 닫지 않음)
        void Close()
        {
            if (round.IsChoosingCandidate)
            {
                return;
            }
            if (panel.activeSelf)
            {
                gameInput.interactable = previousInput;
            }
            panel.SetActive(false);
            round.SetAssistOddsActive(false);
        }

        void Reveal()
        {
            if (round.RevealCandidates(target))
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.Exchange);
                Refresh();
            }
        }

        void Choose()
        {
            if (selected < 0 || GameSession.IsPaused)
            {
                return;
            }
            round.ChooseCandidate(selected);
            Close();
            AudioManager.Instance?.Play(AudioManager.Sfx.CardFlip);
        }

        // 창의 버튼·문구를 지금 상태에 맞춤
        void Refresh()
        {
            bool choosing = round.IsChoosingCandidate;
            // 대상을 고른 뒤에만 확률을 요청함, 같은 대상이면 Worker가 다시 계산하지 않고 기존 결과를 유지함
            if (choosing == false && target >= 0)
            {
                round.RequestOdds(HandOddsSource.Assist, new[] { target });
            }
            else
            {
                round.CancelOdds(HandOddsSource.Assist);
            }
            string oddsText = round.OddsState == HandOddsState.Ready ? HandOddsText.Summary(round.Odds, round.PreviewHand().Category)
                : round.OddsState == HandOddsState.Failed ? "확률을 계산하지 못했습니다. 후보 공개는 가능합니다."
                : "교체 확률 계산 중...";
            oddsButton.gameObject.SetActive(choosing == false && target >= 0);
            oddsButton.interactable = round.OddsState == HandOddsState.Ready;
            oddsLabel.text = "남은 덱에서 한 장을 뽑을 때\n" + oddsText;
            int count = RoundContext.CandidateCount;
            bool eligible = HasTarget();
            instruction.text = choosing
                ? $"② 후보 {count}장 확인 → ③ 한 장으로 교체\n후보를 누르면 바뀔 족보를 미리 볼 수 있어요."
                : $"교체했던 카드 한 장을 다시 바꿀 수 있어요.\n① 카드 선택 → ② 후보 {count}장 확인 → ③ 교체";
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].interactable = choosing == false && round.CanAssist(i);
                slots[i].GetComponentInChildren<TMP_Text>().text = CardText.Of(round.Hand[i])
                    + (target == i ? "\n선택" : round.CanAssist(i) ? "\n교체 가능" : "\n선택 불가");
            }
            for (int i = 0; i < candidates.Length; i++)
            {
                bool show = i < (choosing ? round.Candidates.Count : count);
                candidates[i].gameObject.SetActive(show);
                candidates[i].interactable = choosing;
                if (show && choosing)
                {
                    candidates[i].GetComponentInChildren<TMP_Text>().text = CardText.Of(round.Candidates[i])
                        + "\n" + HandCategoryNames.Of(round.PreviewCandidate(i).Category) + (selected == i ? "\n선택" : "");
                }
                else if (show)
                {
                    candidates[i].GetComponentInChildren<TMP_Text>().text = "?\n후보 " + (i + 1);
                }
            }
            revealButton.gameObject.SetActive(choosing == false);
            revealButton.interactable = target >= 0 && round.CanAssist(target);
            revealButton.GetComponentInChildren<TMP_Text>().text = $"후보 {count}장 보기 · 이번 손패 1회";
            cancelButton.gameObject.SetActive(choosing == false);
            chooseButton.gameObject.SetActive(choosing);
            chooseButton.interactable = selected >= 0;
            cancelButton.GetComponentInChildren<TMP_Text>().text = eligible ? "돌아가기 · 기회 소모 없음" : "돌아가서 먼저 일반 교체하기";
            preview.text = eligible
                ? (target >= 0 ? "선택한 " + CardText.Of(round.Hand[target]) + " 카드 대신 들어올 후보를 확인하세요." : "위에서 다시 바꿀 카드 한 장을 선택하세요.")
                    + $"\n매 라운드 1회 · 후보 공개 전에는 취소 가능\n공개한 뒤에는 후보 한 장을 반드시 골라야 합니다."
                : "먼저 손패에서 카드를 한 번 교체하세요.\n그때 바뀐 카드만 여기에서 다시 바꿀 수 있어요.";
            if (choosing && selected < 0)
            {
                preview.text = $"이번 손패의 선택 교체를 사용했습니다\n후보 중 한 장을 선택한 뒤 ‘이 카드로 교체’를 누르세요.\n원하는 카드가 없어도 한 장을 골라야 합니다.";
            }
            if (choosing && selected >= 0)
            {
                HandResult result = round.PreviewCandidate(selected);
                UnitDefinition definition = unitTable.GetDefinition(result.Category);
                bool merge = false;
                for (int i = 0; i < GridBoard.SlotCount; i++)
                {
                    UnitInstance unit = placement.Board[i];
                    if (unit != null && unit.Star == 1 && unit.Definition == definition)
                    {
                        merge = true;
                    }
                }
                string hand = "";
                for (int i = 0; i < RoundContext.HandSize; i++)
                {
                    hand += CardText.Of(i == target ? round.Candidates[selected] : round.Hand[i]) + "  ";
                }
                preview.text = hand + "\n" + HandCategoryNames.Of(result.Category) + " · " + definition.DisplayName
                    + (merge ? "\n현재 보드에 머지 가능한 유닛 있음" : "\n새 유닛 소환");
            }
        }
    }
}
