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
     * 15슬롯 그리드 슬롯 표현, 입력 처리
     * PlacementController를 구독만 하고 입력은 메서드로 넘김
     * 그리드 슬롯은 월드 스페이스 / 안내 문구, 버튼 등의 요소는 UI 레이어
     */
    public sealed class BoardScreen : MonoBehaviour
    {
        const int NoSelection = -1;

        [SerializeField] PlacementController placement;
        [SerializeField] StageController stage;
        [SerializeField] RoundController round;
        [SerializeField] Camera boardCamera;
        [SerializeField] UnitSlotView[] slots;
        [SerializeField] TMP_Text pendingLabel;

        [Tooltip("상세 정보가 떠 있을 때만 보이는 배경판")]
        [SerializeField] GameObject detailPlate;

        [SerializeField] Button sellButton;
        [SerializeField] TMP_Text sellLabel;
        [SerializeField] Button supportButton;
        [SerializeField] TMP_Text supportLabel;
        [SerializeField] Button jokerButton;
        [SerializeField] TMP_Text jokerLabel;

        [Tooltip("고른 딜러 특전. 상단 줄 라이프·라운드 사이에 둔다")]
        [SerializeField] TMP_Text perkLabel;

        int selected = NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
            }

            sellButton.onClick.AddListener(OnSell);
            supportButton.onClick.AddListener(OnSupportSummon);
            jokerButton.onClick.AddListener(OnUseJoker);

            // PlacementController 이벤트 구독
            placement.PendingChanged += OnPendingChanged;
            placement.Placed += OnPlaced;
            placement.BoardChanged += Refresh;

            // StageController 이벤트 구독
            stage.Changed += Refresh;

            // RoundController 이벤트 구독
            round.PhaseChanged += OnPhaseChanged;

            Refresh();
        }

        
        // 전투 중에도 보드를 조작할 수 있으므로 유닛을 고른 채로 웨이브가 끝날 수 있는데
        // 웨이브가 끝나면 강제로 유닛 선택을 해제하고 Refresh 호출    
        void OnPhaseChanged(RoundPhase phase)
        {
            selected = NoSelection;
            Refresh();
        }

        // 그리드 슬롯은 UI 버튼이 아니고 월드 스페이스이므로
        // 마우스 클릭을 직접 검사해줘야 함
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

            // OverlapPoint로 찾은 오브젝트에서 UnitSlotView 컴포넌트를 가져와서 클릭 처리
            UnitSlotView slot = hit.GetComponent<UnitSlotView>();
            if (slot != null)
            {
                OnSlotClicked(slot);
            }
        }

        // 선택한 유닛이 바뀌었을 때 호출
        // selected 상태를 초기화하고 Refresh를 호출해 UI 상태 갱신
        void OnPendingChanged(UnitInstance pending)
        {
            selected = NoSelection;
            Refresh();
        }

        // 그리드 슬롯에 유닛을 배치했을 때 호출
        // selected 상태를 초기화하고 Refresh를 호출해 UI 상태 갱신
        void OnPlaced(int index, PlacementResult result)
        {
            selected = NoSelection;
            Refresh();
        }

        // 그리드 슬롯을 클릭했을 때 호출
        void OnSlotClicked(UnitSlotView slot)
        {
            // 배치 대기 중인 유닛이 있으면 배치 시도
            if (placement.Pending != null)
            {
                placement.TryPlace(slot.Index);
                return;
            }

            // 선택한 유닛을 다시 클릭했으면 선택 해제
            if (selected == slot.Index)
            {
                selected = NoSelection;
                Refresh();
                return;
            }

            // 선택한 유닛이 없는 상태면 선택 처리
            if (selected == NoSelection)
            {
                selected = slot.Index;
                Refresh();
                return;
            }

            // 선택한 그리드 슬롯이 빈 칸이면 이동, 머지 가능하면 머지
            bool moved = placement.Board[slot.Index] == null
                ? placement.TryMoveSlot(selected, slot.Index)
                : placement.TryMergeSlots(selected, slot.Index);

            selected = NoSelection;

            // 이동이나 머지가 실패하면 선택 상태를 초기화하고 Refresh 호출
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

        void OnUseJoker()
        {
            if (selected != NoSelection)
            {
                placement.TryUseJoker(selected);
            }
        }

        void Refresh()
        {
            UnitInstance pending = placement.Pending;
            GridBoard board = placement.Board;

            // 고른 칸이 비었으면 선택 상태 초기화
            if (selected != NoSelection && board[selected] == null)
            {
                selected = NoSelection;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Show(board[i]);

                if (pending != null)
                {
                    // 대기 유닛이 있으면 배치 가능한 슬롯들을 하이라이트
                    bool placeable = board.CanPlaceAt(i, pending);
                    slots[i].SetHighlight(placeable, false);
                    slots[i].SetInteractable(placeable);
                }
                else if (selected == NoSelection)
                {
                    // 아무것도 안 고른 상태에서는 고를 수 있는 슬롯들을 하이라이트
                    bool hasUnit = board[i] != null;
                    slots[i].SetHighlight(board.HasMergePartner(i), false);
                    slots[i].SetInteractable(hasUnit);
                }
                else
                {
                    // 이미 배치된 유닛을 고른 상태에서는 이동/머지 가능한 슬롯들을 하이라이트
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

        // UI 버튼과 안내 문구 갱신
        void UpdateLabels(UnitInstance pending)
        {
            supportButton.interactable = placement.CanSupportSummon;
            supportLabel.text = $"지원 소환 {placement.SupportSummonCost}";

            ShowPerks();

            // 조커는 고른 유닛에만 쓴다. 고르기 전에는 대상이 없어 버튼을 띄울 이유가 없다
            bool jokerOffered = pending == null && selected != NoSelection;
            jokerButton.gameObject.SetActive(jokerOffered);

            if (jokerOffered)
            {
                jokerButton.interactable = placement.CanUseJokerOn(selected);
                jokerLabel.text = $"조커 ★+1 ({stage.Stage.Jokers})";
            }

            if (pending != null)
            {
                sellButton.gameObject.SetActive(true);
                sellLabel.text = "소환 유닛 판매";
                ShowDetail(pending, placement.IsStuck ? "놓을 자리가 없습니다 - 판매하세요" : "칸을 눌러 배치");
                return;
            }

            if (selected != NoSelection)
            {
                // 이동·머지는 고른 순간 칸이 강조되므로(§9.4) 문구로 또 설명하지 않는다
                sellButton.gameObject.SetActive(true);
                sellLabel.text = "판매";
                ShowDetail(placement.Board[selected], string.Empty);
                return;
            }

            sellButton.gameObject.SetActive(false);
            ShowDetail(null, "족보를 확정하면 유닛이 소환됩니다");
        }

        /**
         * 고른 특전 표시
         *
         * 특전은 한 판 내내 규칙을 바꾸므로 라이프·Chip과 같은 "판이 끝날 때까지 유지되는 상태"다
         * 효과 자체는 이미 다른 숫자에 드러나지만(확정 +6 / 지원 소환 5 / 공격력) 무엇 때문인지는 여기서만 알 수 있다
         *
         * 한 판에 최대 2개라 이름만 늘어놓아도 최악값 263px이다 (상단 줄 빈 폭 409px)
         */
        void ShowPerks()
        {
            var owned = stage.Stage.Perks.Owned;

            if (owned.Count == 0)
            {
                perkLabel.text = string.Empty;
                return;
            }

            string names = stage.PerkTable.GetEntry(owned[0]).displayName;

            for (int i = 1; i < owned.Count; i++)
            {
                names += " · " + stage.PerkTable.GetEntry(owned[i]).displayName;
            }

            perkLabel.text = "특전  " + names;
        }

        /**
         * 유닛 상세 정보 표시
         *
         * 슬롯에는 Sprite와 성급만 표시하고 이름, 공격력 등의 상세 정보는 전부 여기서 표시
         */
        void ShowDetail(UnitInstance unit, string hint)
        {
            detailPlate.SetActive(unit != null);

            if (unit == null)
            {
                pendingLabel.text = hint;
                return;
            }

            string suffix = string.IsNullOrEmpty(hint) ? string.Empty : "  -  " + hint;

            // 특전이 붙은 공격력을 띄운다. 전투에서 실제로 나가는 수와 같아야 비교가 성립한다
            float power = stage.Stage.Perks.AttackPowerOf(unit);

            pendingLabel.text =
                $"{unit.Definition.DisplayName} {new string('★', unit.Star)}{suffix}\n" +
                $"<size=76%>{AttackPatternNames.Of(unit.Definition.Pattern)}   " +
                $"공격력 {power:0.#}   " +
                $"초당 {unit.AttacksPerSecond:0.#}회   " +
                $"DPS {power * unit.AttacksPerSecond:0.#}   " +
                $"사거리 {unit.Range:0.#}</size>";
        }
    }
}
