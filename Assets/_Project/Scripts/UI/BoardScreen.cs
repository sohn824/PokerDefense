using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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

        [SerializeField] Button sellButton;
        [SerializeField] TMP_Text sellLabel;
        [SerializeField, FormerlySerializedAs("supportButton")] Button randomSummonButton;
        [SerializeField, FormerlySerializedAs("supportLabel")] TMP_Text randomSummonLabel;
        [SerializeField] Button jokerButton;
        [SerializeField] TMP_Text jokerLabel;

        [Tooltip("상황 카드를 탭하면 수치 상세를 펼치고 접는다")]
        [SerializeField] Button detailToggle;

        [Tooltip("유닛 아트의 조준 방향·반동을 얻는 컨트롤러")]
        [SerializeField] CombatController combat;

        // 사거리 링 (에셋 없이 LineRenderer로 원을 그린다)
        const int RingSegments = 48;
        const float RingWidth = 0.05f;
        static readonly Color RingColor = new Color(1f, 0.95f, 0.6f, 0.5f);

        int selected = NoSelection;

        // 상황 카드 수치 상세를 펼친 상태인지 (탭으로 토글, 세션 동안 유지)
        bool detailExpanded;

        LineRenderer rangeRing;

        // 조커로 성급을 올리는 중인지
        // (OnPlaced에서 조커 승급음과 일반 머지음을 구분하는 데 사용)
        bool usingJoker;

        // ActionBarController가 판매·조커 버튼 노출을 정할 때 참조한다
        public bool HasSelection => selected != NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
            }

            sellButton.onClick.AddListener(OnSell);
            randomSummonButton.onClick.AddListener(OnRandomSummon);
            jokerButton.onClick.AddListener(OnUseJoker);
            detailToggle.onClick.AddListener(ToggleDetail);

            BuildRangeRing();

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
                // 빈 배경을 누르면 유닛 선택을 해제한다
                if (selected != NoSelection)
                {
                    selected = NoSelection;
                    Refresh();
                }

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
                if (placement.TryPlace(slot.Index) == false)
                {
                    // 못 놓는 칸을 누르면 안내 문구만 띄우고 return
                    pendingLabel.text = "여기엔 놓을 수 없습니다 · 같은 유닛·성급 칸에만 겹칠 수 있습니다";
                }

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

            if (moved)
            {
                selected = NoSelection;
            }
            else
            {
                // 슬롯을 클릭했는데 이동, 머지, 교환 모두 실패하면 안내 문구만 띄우고 선택 상태 유지
                pendingLabel.text = "그 자리에는 할 수 없습니다";
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

        void OnRandomSummon()
        {
            if (placement.TryRandomSummon())
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
                    // 대기 유닛이 있으면 배치 가능한 슬롯을 하이라이트
                    bool placeable = board.CanPlaceAt(i, pending);
                    slots[i].SetHighlight(placeable, false);
                    slots[i].SetInteractable(true);
                    slots[i].SetActionHint(string.Empty);
                }
                else if (selected == NoSelection)
                {
                    // 아무것도 안 고른 상태에서는 고를 수 있는 슬롯들을 하이라이트
                    bool hasUnit = board[i] != null;
                    slots[i].SetHighlight(board.HasMergePartner(i), false);
                    slots[i].SetInteractable(hasUnit);
                    slots[i].SetActionHint(string.Empty);
                }
                else
                {
                    // 유닛을 고른 상태 - 각 칸을 누르면 무슨 일이 일어나는지 문구로 미리 보여주기
                    slots[i].SetInteractable(true);
                    slots[i].SetHighlight(true, i == selected);
                    slots[i].SetActionHint(HintFor(board, i));
                }
            }

            UpdateLabels(pending);
            UpdateRangeRing();
        }

        // 상황 카드 탭 - 수치 상세를 펼치거나 접는다
        void ToggleDetail()
        {
            detailExpanded = !detailExpanded;
            Refresh();
        }

        // 반지름 1짜리 원을 한 번 만들어 두고, 표시할 때 위치·크기만 갱신한다
        void BuildRangeRing()
        {
            GameObject go = new GameObject("RangeRing");
            go.transform.SetParent(transform, false);

            rangeRing = go.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = RingSegments;
            rangeRing.widthMultiplier = RingWidth;
            rangeRing.numCapVertices = 2;
            rangeRing.alignment = LineAlignment.View;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            rangeRing.material = new Material(shader);

            rangeRing.startColor = RingColor;
            rangeRing.endColor = RingColor;
            rangeRing.sortingOrder = 20;
            rangeRing.enabled = false;
        }

        // 고른 유닛의 사거리를 그 칸 위에 원으로 표시한다 (다른 칸 탭은 막지 않는다)
        void UpdateRangeRing()
        {
            if (selected == NoSelection || placement.Board[selected] == null)
            {
                rangeRing.enabled = false;
                return;
            }

            Vector3 center = slots[selected].transform.position;
            float radius = placement.Board[selected].Range;

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                rangeRing.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    center.z));
            }

            rangeRing.enabled = true;
        }

        // 고른 유닛 기준으로 슬롯 i를 누르면 일어날 일을 안내하는 문구를 반환
        string HintFor(GridBoard board, int i)
        {
            if (i == selected)
            {
                return "선택";
            }

            if (board[i] == null)
            {
                return "이동";
            }

            if (board.CanMergeSlots(selected, i))
            {
                return $"머지 ★{board[selected].Star + 1}";
            }

            return "교환";
        }

        // 버튼 interactable·라벨 문구·안내 문구를 갱신한다
        // 버튼을 아예 보일지 말지는 ActionBarController가 단계별로 정한다
        void UpdateLabels(UnitInstance pending)
        {
            // 비용·남은 횟수를 항상 노출하고, 못 쓰는 이유는 회색만이 아니라 문구로 보여준다
            randomSummonButton.interactable = placement.CanRandomSummon;
            int summonsLeft = stage.Economy.RandomSummonsPerRound - placement.RandomSummonsUsed;
            randomSummonLabel.text = placement.CanRandomSummon
                ? $"랜덤 소환 · {placement.RandomSummonCost} Chip · 남은 {summonsLeft}회"
                : $"랜덤 소환 · {RandomSummonBlockReason()}";

            // 조커는 고른 유닛에만 쓸 수 있음
            bool jokerOffered = pending == null && selected != NoSelection;

            if (jokerOffered)
            {
                UnitInstance unit = placement.Board[selected];
                jokerButton.interactable = placement.CanUseJokerOn(selected);
                jokerLabel.text = placement.CanUseJokerOn(selected)
                    ? $"조커 ★{unit.Star} → ★{unit.Star + 1} · Joker {stage.Stage.Jokers}"
                    : $"조커 · {JokerBlockReason(unit)}";
            }

            if (pending != null)
            {
                sellLabel.text = $"소환 유닛 판매 +{stage.Economy.SellPriceFor(pending.Star)} Chip";
                ShowDetail(pending,
                    placement.IsStuck ? "놓을 자리가 없습니다 - 판매하세요" : "칸을 눌러 배치",
                    $"같은 유닛·성급 칸에 겹치면 ★{pending.Star + 1}");
                return;
            }

            if (selected != NoSelection)
            {
                sellLabel.text = $"판매 +{stage.Economy.SellPriceFor(placement.Board[selected].Star)} Chip";
                ShowDetail(placement.Board[selected], string.Empty, MergeHintFor(selected));
                return;
            }

            ShowDetail(null, "확정하면 유닛이 소환됩니다", null);
        }

        // 고른 유닛이 지금 머지할 수 있는지 한 줄로 안내한다
        string MergeHintFor(int index)
        {
            UnitInstance unit = placement.Board[index];

            if (unit.IsMaxStar)
            {
                return "최대 성급 - 더 못 올림";
            }

            if (placement.Board.HasMergePartner(index))
            {
                return $"★{unit.Star + 1}로 머지할 상대가 있습니다";
            }

            return $"같은 유닛 ★{unit.Star} 둘이면 ★{unit.Star + 1}";
        }

        // 랜덤 소환 버튼이 비활성인 이유
        string RandomSummonBlockReason()
        {
            if (placement.RandomSummonsUsed >= stage.Economy.RandomSummonsPerRound)
            {
                return "남은 0회";
            }

            if (placement.Board.OccupiedCount >= GridBoard.SlotCount)
            {
                return "빈 칸 없음";
            }

            if (stage.Stage.Chip < placement.RandomSummonCost)
            {
                return $"Chip {stage.Stage.Chip}/{placement.RandomSummonCost}";
            }

            return $"{placement.RandomSummonCost} Chip";
        }

        // 조커 버튼이 비활성인 이유
        string JokerBlockReason(UnitInstance unit)
        {
            if (unit.IsMaxStar)
            {
                return "이미 최대 성급";
            }

            if (stage.Stage.Jokers <= 0)
            {
                return "Joker 없음";
            }

            return "사용 불가";
        }

        // 유닛 상세 정보 표시
        // 슬롯에는 Sprite와 성급만 표시하고 이름·역할은 여기 앞줄에, 수치는 탭으로 펼치는 상세에 둔다
        void ShowDetail(UnitInstance unit, string hint, string mergeHint)
        {
            if (unit == null)
            {
                pendingLabel.text = hint;
                return;
            }

            string toggle = detailExpanded ? "  ▲" : "  ▼";
            string body =
                $"{unit.Definition.DisplayName} {new string('★', unit.Star)} · {AttackPatternNames.Of(unit.Definition.Pattern)}{toggle}";

            if (string.IsNullOrEmpty(hint) == false)
            {
                body += $"\n{hint}";
            }

            if (string.IsNullOrEmpty(mergeHint) == false)
            {
                body += $"\n<size=76%>{mergeHint}</size>";
            }

            if (detailExpanded)
            {
                float power = unit.AttackPower;
                body +=
                    $"\n<size=76%>공격력 {power:0.#}   초당 {unit.AttacksPerSecond:0.#}회   " +
                    $"명목 DPS {power * unit.AttacksPerSecond:0.#}   사거리 {unit.Range:0.#}</size>";
            }

            pendingLabel.text = body;
        }
    }
}
