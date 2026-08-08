using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * BoardScreen
     *
     * 15슬롯 보드의 표현과 입력. PlacementController를 구독만 하고 입력은 메서드로 넘김
     * 슬롯은 월드 스페이스이고, 배치 대기 안내와 포기 버튼만 uGUI로 남는다
     * 슬롯 수가 상수 15라 칸을 씬에 고정해 두고 생성하지 않는다
     *
     * 탭 규칙 두 가지
     * - 배치 대기 유닛이 있으면: 칸을 누르면 배치하거나 머지
     * - 없으면: 유닛을 눌러 고르고 다른 유닛을 눌러 합침 (성급 최대치는 이 경로로만 만들 수 있음)
     */
    public sealed class BoardScreen : MonoBehaviour
    {
        const int NoSelection = -1;

        [SerializeField] PlacementController placement;
        [SerializeField] Camera boardCamera;
        [SerializeField] UnitSlotView[] slots;
        [SerializeField] TMP_Text pendingLabel;
        [SerializeField] Button discardButton;

        int selected = NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
            }

            discardButton.onClick.AddListener(placement.DiscardPending);

            placement.PendingChanged += OnPendingChanged;
            placement.Placed += OnPlaced;

            Refresh();
        }

        // 월드 슬롯은 uGUI 버튼이 아니라서 클릭을 직접 잡는다
        void Update()
        {
            if (Pointer.current == null || Pointer.current.press.wasPressedThisFrame == false)
            {
                return;
            }

            // 카드 UI 위를 누른 것은 보드 클릭이 아니다
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 screenPoint = Pointer.current.position.ReadValue();
            Vector2 worldPoint = boardCamera.ScreenToWorldPoint(screenPoint);

            Collider2D hit = Physics2D.OverlapPoint(worldPoint);

            if (hit == null)
            {
                return;
            }

            UnitSlotView slot = hit.GetComponent<UnitSlotView>();

            if (slot != null)
            {
                OnSlotClicked(slot);
            }
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

            // 두 번째 탭이 머지 대상. 실패하면 선택만 푼다
            if (placement.TryMergeSlots(selected, slot.Index) == false)
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
                    // 합칠 상대가 있는 유닛만 강조. 나머지는 눌러도 할 일이 없다
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
                    ? "족보를 확정하면 유닛이 소환됩니다"
                    : $"{Describe(placement.Board[selected])} 합치기 - 같은 유닛을 누르세요";
                discardButton.gameObject.SetActive(false);
                return;
            }

            bool stuck = placement.IsStuck;
            discardButton.gameObject.SetActive(stuck);

            pendingLabel.text = stuck
                ? $"{Describe(pending)}을 놓을 자리가 없습니다"
                : $"{Describe(pending)} 소환 (공격력 {pending.AttackPower:0.#}) - 칸을 누르세요";
        }

        static string Describe(UnitInstance unit)
        {
            return unit.Definition.DisplayName + " " + new string('★', unit.Star);
        }
    }
}
