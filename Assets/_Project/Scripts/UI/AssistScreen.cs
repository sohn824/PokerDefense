using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    // 추가 교체의 자리 선택·후보 공개·선택 프리뷰. 덱이나 재화는 컨트롤러만 변경한다.
    public sealed class AssistScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup gameInput;
        bool previousInput;

        [SerializeField] RoundController round;
        [SerializeField] StageController stage;
        [SerializeField] PlacementController placement;
        [SerializeField] GameFlowController flow;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] Button openButton;
        [SerializeField] TMP_Text openLabel;
        [SerializeField] TMP_Text actionHint;
        [SerializeField] GameObject panel;
        [SerializeField] Button[] slots;
        [SerializeField] Button[] candidates;
        [SerializeField] Button revealButton;
        [SerializeField] Button chooseButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text instruction;
        [SerializeField] TMP_Text preview;

        int target = -1;
        int selected = -1;

        void Awake()
        {
            panel.SetActive(false);
            openButton.onClick.AddListener(Open);
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

        void Update()
        {
            bool available = round.Phase == RoundPhase.Exchange && flow.ShopCards == null && flow.IsFinished == false
                && round.Assistance != RoundController.AssistMode.Disabled;
            openButton.gameObject.SetActive(available);
            bool eligible = HasTarget();
            string reason = round.AssistUsed ? "이번 손패 사용 완료"
                : stage.Stage.AssistCharges == 0 ? "남은 기회 없음"
                : eligible == false ? "일반 교체 후 사용" : $"남은 기회 {stage.Stage.AssistCharges}회";
            openLabel.text = "추가 교체\n<size=30>" + reason + "</size>";
            openButton.interactable = available && panel.activeSelf == false && stage.Stage.AssistCharges > 0
                && round.AssistUsed == false && eligible;
            actionHint.gameObject.SetActive(available && panel.activeSelf == false);
            actionHint.text = round.AssistUsed ? "추가 교체 완료 · 남은 카드를 일반 교체하거나 손패를 확정하세요."
                : stage.Stage.AssistCharges == 0 ? "추가 교체 기회가 없습니다 · 일반 교체하거나 손패를 확정하세요."
                : eligible ? "한 장이 더 아쉽다면 추가 교체 · 교체했던 카드 1장을 다시 바꿉니다."
                : "손패에서 바꿀 카드를 고른 뒤 일반 교체 · 그대로 확정해도 됩니다.";
            if (available == false)
            {
                Close();
            }
        }

        public void Open()
        {
            if (GameSession.IsPaused || panel.activeSelf || round.Phase != RoundPhase.Exchange
                || flow.ShopCards != null || flow.IsFinished || round.AssistUsed || stage.Stage.AssistCharges == 0 || HasTarget() == false)
            {
                return;
            }
            target = -1;
            // 대상이 하나뿐이면 같은 카드를 다시 고르게 하지 않는다.
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
            Refresh();
        }

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

        void Refresh()
        {
            bool choosing = round.IsChoosingCandidate;
            int current = stage.HoldBonusFor(round.UsedExchanges);
            int after = stage.HoldBonusFor(round.UsedExchanges + 1);
            int count = round.Assistance == RoundController.AssistMode.RandomOne ? 1 : 3;
            bool eligible = false;
            for (int i = 0; i < slots.Length; i++)
            {
                eligible |= round.CanAssist(i);
            }
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
            revealButton.interactable = target >= 0 && round.CanAssist(target) && stage.Stage.AssistCharges > 0;
            revealButton.GetComponentInChildren<TMP_Text>().text = $"기회 1회 쓰고 후보 {count}장 보기";
            cancelButton.gameObject.SetActive(choosing == false);
            chooseButton.gameObject.SetActive(choosing);
            chooseButton.interactable = selected >= 0;
            cancelButton.GetComponentInChildren<TMP_Text>().text = eligible ? "돌아가기 · 기회 소모 없음" : "돌아가서 먼저 일반 교체하기";
            preview.text = eligible
                ? (target >= 0 ? "선택한 " + CardText.Of(round.Hand[target]) + " 카드 대신 들어올 후보를 확인하세요." : "위에서 다시 바꿀 카드 한 장을 선택하세요.")
                    + $"\n후보를 보면 기회 1회 소모 · 유지 보너스 +{current} → +{after}\n공개한 뒤에는 후보 한 장을 반드시 골라야 합니다."
                : "먼저 손패에서 카드를 한 번 교체하세요.\n그때 바뀐 카드만 여기에서 다시 바꿀 수 있어요.\n상점 보유 카드를 놓은 자리는 대상에서 제외됩니다.";
            if (choosing && selected < 0)
            {
                preview.text = $"기회 1회 사용 · 유지 보너스 +{current} → +{after}\n후보 중 한 장을 선택한 뒤 ‘이 카드로 교체’를 누르세요.\n원하는 카드가 없어도 한 장을 골라야 합니다.";
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
                    hand += CardText.Of(i == target ? round.Candidates[selected] : round.Hand[i]) + "  ";
                preview.text = hand + "\n" + HandCategoryNames.Of(result.Category) + " · " + definition.DisplayName
                    + (merge ? "\n현재 보드에 머지 가능한 유닛 있음" : "\n새 유닛 소환") + $"\n유지 보너스 +{after}";
            }
        }
    }
}
