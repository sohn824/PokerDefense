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

        [Tooltip("유닛 아트의 조준 방향·반동을 얻는 컨트롤러")]
        [SerializeField] CombatController combat;

        int selected = NoSelection;

        // 조커로 성급을 올리는 중인지
        // (OnPlaced에서 조커 승급음과 일반 머지음을 구분하는 데 사용)
        bool usingJoker;

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

        
        // 전투 중에도 보드를 조작할 수 있으므로 유닛을 고른 채로 웨이브가 끝날 수 있기 때문에
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
            UpdateSlotArt();

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
            if (pending != null)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.UnitSummon);
            }

            selected = NoSelection;
            Refresh();
        }

        // 그리드 슬롯에 유닛을 배치했을 때 호출
        // selected 상태를 초기화하고 Refresh를 호출해 UI 상태 갱신
        void OnPlaced(int index, PlacementResult result)
        {
            if (result == PlacementResult.Placed)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.UnitPlace);
            }
            else if (result == PlacementResult.Merged)
            {
                // 조커 승급도 Merged로 들어오므로 먼저 갈라내고, 나머지는 합쳐진 성급에 맞는 효과음 선택
                int star = placement.Board[index] != null ? placement.Board[index].Star : 2;

                if (usingJoker)
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.JokerPromote);
                }
                else if (star >= 3)
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.MergeStar3);
                }
                else
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.MergeStar2);
                }
            }

            selected = NoSelection;
            Refresh();
        }

        // 유닛 아트의 방향과 반동을 매 프레임 갱신
        void UpdateSlotArt()
        {
            GridBoard board = placement.Board;
            CombatContext combatContext = combat.IsFighting ? combat.Combat : null;

            for (int i = 0; i < slots.Length; i++)
            {
                UnitInstance unit = board[i];

                if (unit == null)
                {
                    continue;
                }

                // 전투 중이 아닐 경우 조준 방향은 아래로 고정하고 발사 반동은 0으로 설정
                if (combatContext == null)
                {
                    slots[i].SetAim(AimDirection.Down, -1f, unit.AttacksPerSecond, 0);
                    continue;
                }

                // 각 슬롯(UnitSlotView)의 업데이트 메소드에
                // CombatContext에서 얻은 조준 방향과 발사 반동 정보를 넘겨줌
                slots[i].SetAim(combatContext.AimOf(unit), combatContext.SecondsSinceShot(unit),
                    unit.AttacksPerSecond, combatContext.ShotCountOf(unit));
            }
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

            // 선택한 그리드 슬롯이 빈 칸이면 이동, 머지 가능하면 머지, 그 외엔 자리 교환
            bool moved;

            if (placement.Board[slot.Index] == null)
            {
                moved = placement.TryMoveSlot(selected, slot.Index);
            }
            else if (placement.Board.CanMergeSlots(selected, slot.Index))
            {
                moved = placement.TryMergeSlots(selected, slot.Index);
            }
            else
            {
                moved = placement.TrySwapSlots(selected, slot.Index);
            }

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
                if (placement.TrySellPending())
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.ChipGain);
                }

                return;
            }

            if (selected != NoSelection)
            {
                if (placement.TrySellSlot(selected))
                {
                    AudioManager.Instance?.Play(AudioManager.Sfx.ChipGain);
                }

                selected = NoSelection;
            }
        }

        void OnSupportSummon()
        {
            if (placement.TrySupportSummon())
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.UnitSummon);
            }
        }

        void OnUseJoker()
        {
            if (selected != NoSelection)
            {
                usingJoker = true;
                placement.TryUseJoker(selected);
                usingJoker = false;
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
                    // 이미 배치된 유닛을 고른 상태에서는 자신 외 모든 칸을 하이라이트
                    // (이동/머지/자리교환 모두 가능)
                    bool isSelected = i == selected;
                    slots[i].SetHighlight(true, isSelected);
                    slots[i].SetInteractable(true);
                }
            }

            UpdateLabels(pending);
        }

        // UI 버튼과 안내 문구 갱신
        void UpdateLabels(UnitInstance pending)
        {
            supportButton.interactable = placement.CanSupportSummon;
            supportLabel.text = $"지원 소환 {placement.SupportSummonCost}";

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
                sellButton.gameObject.SetActive(true);
                sellLabel.text = "판매";
                ShowDetail(placement.Board[selected], string.Empty);
                return;
            }

            sellButton.gameObject.SetActive(false);
            ShowDetail(null, "확정하면 유닛이 소환됩니다");
        }

        // 유닛 상세 정보 표시
        // 슬롯에는 Sprite와 성급만 표시하고 이름, 공격력 등의 상세 정보는 전부 여기서 표시
        void ShowDetail(UnitInstance unit, string hint)
        {
            detailPlate.SetActive(unit != null);

            if (unit == null)
            {
                pendingLabel.text = hint;
                return;
            }

            string suffix = string.IsNullOrEmpty(hint) ? string.Empty : "  -  " + hint;

            float power = unit.AttackPower;

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
