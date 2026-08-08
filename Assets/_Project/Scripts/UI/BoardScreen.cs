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
     * 슬롯은 월드 스페이스이고, 안내 문구와 버튼만 uGUI로 남는다
     *
     * 탭 규칙 (DESIGN §9.4)
     * - 배치 대기 유닛이 있으면: 칸을 누르면 배치하거나 머지
     * - 없으면: 유닛을 눌러 고른 뒤
     *     빈 칸을 누르면 이동, 짝을 누르면 머지, 판매 버튼을 누르면 판매
     *   모드를 늘리지 않고 세 행동을 한 선택으로 흡수한다
     */
    public sealed class BoardScreen : MonoBehaviour
    {
        const int NoSelection = -1;

        [SerializeField] PlacementController placement;
        [SerializeField] StageController stage;
        [SerializeField] Camera boardCamera;
        [SerializeField] UnitSlotView[] slots;
        [SerializeField] TMP_Text pendingLabel;
        [SerializeField] Button sellButton;
        [SerializeField] TMP_Text sellLabel;
        [SerializeField] Button supportButton;
        [SerializeField] TMP_Text supportLabel;

        int selected = NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
            }

            sellButton.onClick.AddListener(OnSell);
            supportButton.onClick.AddListener(OnSupportSummon);

            placement.PendingChanged += OnPendingChanged;
            placement.Placed += OnPlaced;
            placement.BoardChanged += Refresh;

            // Chip이 바뀌면 지원 소환 버튼의 활성 여부가 달라진다
            stage.Changed += Refresh;

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

            // 두 번째 탭 - 빈 칸이면 이동, 짝이면 머지
            bool moved = placement.Board[slot.Index] == null
                ? placement.TryMoveSlot(selected, slot.Index)
                : placement.TryMergeSlots(selected, slot.Index);

            selected = NoSelection;

            if (moved == false)
            {
                Refresh();
            }
        }

        void OnSell()
        {
            if (placement.Pending != null)
            {
                placement.TrySellPending();
                return;
            }

            if (selected != NoSelection)
            {
                placement.TrySellSlot(selected);
                selected = NoSelection;
            }
        }

        void OnSupportSummon()
        {
            placement.TrySupportSummon();
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
                    // 유닛이 있는 칸은 전부 고를 수 있다. 고르면 이동·머지·판매가 열린다
                    bool hasUnit = board[i] != null;
                    slots[i].SetHighlight(board.HasMergePartner(i), false);
                    slots[i].SetInteractable(hasUnit);
                }
                else
                {
                    bool isSelected = i == selected;
                    bool actionable = isSelected
                                      || board[i] == null
                                      || board.CanMergeSlots(selected, i);
                    slots[i].SetHighlight(actionable, isSelected);
                    slots[i].SetInteractable(actionable);
                }
            }

            UpdateLabels(pending);
        }

        void UpdateLabels(UnitInstance pending)
        {
            supportButton.interactable = placement.CanSupportSummon;
            supportLabel.text = $"지원 소환 {placement.SupportSummonCost}";

            if (pending != null)
            {
                bool stuck = placement.IsStuck;

                sellButton.gameObject.SetActive(true);
                sellLabel.text = "소환 유닛 판매";
                pendingLabel.text = stuck
                    ? $"{Describe(pending)}을 놓을 자리가 없습니다 - 판매하세요"
                    : $"{Describe(pending)} 소환 (공격력 {pending.AttackPower:0.#}) - 칸을 누르세요";
                return;
            }

            if (selected != NoSelection)
            {
                sellButton.gameObject.SetActive(true);
                sellLabel.text = "판매";
                pendingLabel.text = $"{Describe(placement.Board[selected])} 선택 - 빈 칸은 이동, 같은 유닛은 합치기";
                return;
            }

            sellButton.gameObject.SetActive(false);
            pendingLabel.text = "족보를 확정하면 유닛이 소환됩니다";
        }

        static string Describe(UnitInstance unit)
        {
            return unit.Definition.DisplayName + " " + new string('★', unit.Star);
        }
    }
}
