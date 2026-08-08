using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /// <summary>
    /// 15슬롯 보드와 배치 대기 유닛 표시. PlacementController를 구독만 하고 입력은 메서드로 넘긴다.
    /// 슬롯 수가 상수 15라 칸을 씬에 고정해 두고 생성하지 않는다.
    ///
    /// 탭 규칙은 두 가지다.
    /// - 배치 대기 유닛이 있으면: 칸을 누르면 배치하거나 머지한다.
    /// - 없으면: 유닛을 눌러 고르고 다른 유닛을 눌러 합친다. ★3은 이 경로로만 만들 수 있다.
    /// </summary>
    public sealed class BoardScreen : MonoBehaviour
    {
        const int NoSelection = -1;

        [SerializeField] PlacementController placement;
        [SerializeField] UnitSlotView[] slots;
        [SerializeField] TMP_Text pendingLabel;
        [SerializeField] Button discardButton;

        int selected = NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
                slots[i].Clicked += OnSlotClicked;
            }

            discardButton.onClick.AddListener(placement.DiscardPending);

            placement.PendingChanged += OnPendingChanged;
            placement.Placed += OnPlaced;

            Refresh();
        }

        void OnPendingChanged(UnitInstance pending)
        {
            selected = NoSelection;
            Refresh();
        }

        void OnPlaced(int index, PlacementResult result)
        {
            selected = NoSelection;
            Refresh();
        }

        void OnSlotClicked(UnitSlotView slot)
        {
            if (placement.Pending != null)
            {
                placement.TryPlace(slot.Index);
                return;
            }

            if (selected == slot.Index)
            {
                selected = NoSelection;
                Refresh();
                return;
            }

            if (selected == NoSelection)
            {
                selected = slot.Index;
                Refresh();
                return;
            }

            // 두 번째 탭이 머지 대상이다. 실패하면 선택만 푼다.
            if (!placement.TryMergeSlots(selected, slot.Index))
            {
                selected = NoSelection;
                Refresh();
            }
        }

        void Refresh()
        {
            UnitInstance pending = placement.Pending;
            GridBoard board = placement.Board;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Show(board[i]);

                if (pending != null)
                {
                    bool placeable = board.CanPlaceAt(i, pending);
                    slots[i].SetHighlight(placeable, false);
                    slots[i].SetInteractable(placeable);
                }
                else if (selected == NoSelection)
                {
                    // 합칠 상대가 있는 유닛만 강조한다. 나머지는 눌러도 할 일이 없다.
                    bool hasPartner = board.HasMergePartner(i);
                    slots[i].SetHighlight(hasPartner, false);
                    slots[i].SetInteractable(hasPartner);
                }
                else
                {
                    bool isSelected = i == selected;
                    bool mergeable = isSelected || board.CanMergeSlots(selected, i);
                    slots[i].SetHighlight(mergeable, isSelected);
                    slots[i].SetInteractable(mergeable);
                }
            }

            UpdatePendingLabel(pending);
        }

        void UpdatePendingLabel(UnitInstance pending)
        {
            if (pending == null)
            {
                pendingLabel.text = selected == NoSelection
                    ? "Confirm a hand to summon a unit"
                    : $"Merging {placement.Board[selected]} - tap its match";
                discardButton.gameObject.SetActive(false);
                return;
            }

            bool stuck = placement.IsStuck;
            discardButton.gameObject.SetActive(stuck);

            pendingLabel.text = stuck
                ? $"No room for {pending} - discard it"
                : $"Summoned {pending} (ATK {pending.AttackPower:0.#}) - tap a slot";
        }
    }
}
