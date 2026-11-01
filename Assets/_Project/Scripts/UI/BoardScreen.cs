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
     * 보드 Grid의 입력을 받아 PlacementController에 넘기고, 보드 상태를 보여주는 화면
     * 슬롯은 월드 스페이스라 UI 버튼이 아니고 Update에서 포인터 위치를 Physics2D로 직접 검사
     *
     * 슬롯 눌렀을 때 처리
     * Pending(배치 대기)이 있을 때 - 배치·합치기, 다른 유닛이 있는 칸이면 교체 확인 창
     * 고른 유닛이 없을 때 - 그 유닛 선택
     * 고른 유닛이 있을 때 - 빈 칸이면 이동, 같은 유닛·성급이면 머지, 그 외엔 자리 교환
     *
     * 구독 - PlacementController(PendingChanged·Placed·BoardChanged), StageController.Changed, RoundController.PhaseChanged
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
        [SerializeField] BoardDecisionScreen decision;
        [SerializeField] Button jokerButton;
        [SerializeField] TMP_Text jokerLabel;

        [Tooltip("상황 카드를 탭하면 수치 상세를 펼치고 접음")]
        [SerializeField] Button detailToggle;

        [Tooltip("유닛의 조준 방향·반동을 얻는 CombatController")]
        [SerializeField] CombatController combat;

        // 사거리 링 (에셋 없이 LineRenderer로 원을 그림)
        const int RingSegments = 48;
        const float RingWidth = 0.05f;
        static readonly Color RingColor = new Color(1f, 0.95f, 0.6f, 0.5f);

        int selected = NoSelection;

        // 상황 카드 수치 상세를 펼친 상태인지
        bool detailExpanded;

        LineRenderer rangeRing;

        // 승급권(Joker)으로 성급을 올리는 중인지
        bool usingJoker;

        // ActionBarController가 퇴장·승급권 버튼 노출을 정할 때 참조
        public bool HasSelection => selected != NoSelection;

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(i);
            }

            sellButton.onClick.AddListener(OnSell);
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

        // 전투 중에도 보드를 조작할 수 있어 유닛을 고른 채로 전투가 끝날 수 있음
        // 새 라운드 시작(StartRound)과 손패 확정(ConfirmHand) 때 선택 해제
        void OnPhaseChanged(RoundPhase phase)
        {
            selected = NoSelection;
            Refresh();
        }

        // 그리드 슬롯은 UI 버튼이 아니라 월드 스페이스라 마우스·터치를 직접 검사
        void Update()
        {
            UpdateSlotArt();

            if (GameSession.IsPaused || Pointer.current == null || Pointer.current.press.wasPressedThisFrame == false)
            {
                return;
            }

            // 카드 UI 위를 누른 것은 보드 클릭이 아님
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 screenPoint = Pointer.current.position.ReadValue();
            Vector2 worldPoint = boardCamera.ScreenToWorldPoint(screenPoint);

            Collider2D hit = Physics2D.OverlapPoint(worldPoint);
            if (hit == null)
            {
                // 빈 배경을 누르면 유닛 선택 해제
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

        // Pending이 생기거나(족보 확정) 없어질 때(배치·교체·소환 포기) 호출
        // 생기면 소환 효과음을 내고, 선택을 해제한 뒤 Refresh
        void OnPendingChanged(UnitInstance pending)
        {
            if (pending != null)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.UnitSummon);
            }

            selected = NoSelection;
            Refresh();
        }

        // 슬롯 내용이 배치·교체·칸끼리 머지·승급권으로 바뀌었을 때 호출
        // 결과에 맞는 효과음을 내고, 선택을 해제한 뒤 Refresh
        void OnPlaced(int index, PlacementResult result)
        {
            if (result == PlacementResult.Placed || result == PlacementResult.Replaced)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.UnitPlace);
            }
            else if (result == PlacementResult.Merged)
            {
                // 승급권 승급도 Merged로 들어오므로 먼저 구분해 승급 효과음을 내고, 일반 머지는 합쳐진 성급에 맞는 효과음 선택
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
            CombatContext combatContext = combat.IsFighting ? combat.LatestCombatContext : null;

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

                // 각 슬롯(UnitSlotView)의 SetAim에
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
                    // 다른 유닛이 있는 칸이면 교체 확인 창을 엶
                    UnitInstance old = placement.Board[slot.Index];
                    UnitInstance next = placement.Pending;
                    int index = slot.Index;
                    if (old != null)
                    {
                        decision.Open($"{old.Definition.DisplayName} ★{old.Star} 퇴장\n↓\n{next.Definition.DisplayName} ★{next.Star} 배치\n\n기존 유닛은 사라집니다.", () =>
                        {
                            if (placement.Pending == next) placement.TryReplacePending(index, old);
                        });
                    }
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

            // 누른 칸이 빈 칸이면 이동, 머지 가능하면 머지, 그 외엔 자리 교환
            // 고른 칸에는 유닛이 있으므로 세 경우 모두 성공함
            if (placement.Board[slot.Index] == null)
            {
                placement.TryMoveSlot(selected, slot.Index);
            }
            else if (placement.Board.CanMergeSlots(selected, slot.Index))
            {
                placement.TryMergeSlots(selected, slot.Index);
            }
            else
            {
                placement.TrySwapSlots(selected, slot.Index);
            }

            // 자리 교환은 BoardChanged로 Refresh가 선택이 남은 채 먼저 돌았으므로 해제한 뒤 다시 그림
            selected = NoSelection;
            Refresh();
        }

        // 소환 포기·퇴장 버튼
        void OnSell()
        {
            UnitInstance pending = placement.Pending;
            if (pending != null)
            {
                decision.Open($"{pending.Definition.DisplayName} ★{pending.Star} 소환을 포기할까요?\n이번 라운드에는 다시 소환되지 않습니다.", () =>
                {
                    if (placement.Pending == pending) placement.TryDiscardPending();
                });
            }
            else if (selected != NoSelection)
            {
                int index = selected;
                UnitInstance unit = placement.Board[index];
                decision.Open($"{unit.Definition.DisplayName} ★{unit.Star}을 퇴장시킬까요?\n퇴장한 유닛은 돌아오지 않습니다.", () =>
                {
                    if (placement.Board[index] == unit) placement.TryRetireSlot(index);
                });
            }
        }

        // 고른 유닛에 승급권 사용 (usingJoker로 효과음 구분)
        void OnUseJoker()
        {
            if (selected != NoSelection)
            {
                usingJoker = true;
                placement.TryUseJoker(selected);
                usingJoker = false;
            }
        }

        // 슬롯·버튼 라벨·안내 문구·사거리 링을 지금 보드 상태로 다시 그림
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
                    slots[i].SetActionHint(board[i] == null ? "배치" : placeable ? "합치기" : "유닛 교체");
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

        void ToggleDetail()
        {
            detailExpanded = !detailExpanded;
            Refresh();
        }

        // 사거리 링용 LineRenderer를 한 번 만들어 두고, 표시할 때 점 위치만 갱신
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

        // 고른 유닛의 사거리를 그 칸 위에 원으로 표시
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
                return $"합치기 ★{board[selected].Star + 1}";
            }

            return "교환";
        }

        // 버튼 interactable·라벨 문구·안내 문구 갱신
        void UpdateLabels(UnitInstance pending)
        {
            // 승급권은 고른 유닛에만 쓸 수 있음
            bool jokerOffered = pending == null && selected != NoSelection;

            if (jokerOffered)
            {
                UnitInstance unit = placement.Board[selected];
                jokerButton.interactable = placement.CanUseJokerOn(selected);
                jokerLabel.text = placement.CanUseJokerOn(selected)
                    ? $"승급권 ★{unit.Star} → ★{unit.Star + 1} · 남은 {stage.Stage.Jokers}회"
                    : $"승급권 · {JokerBlockReason(unit)}";
            }

            if (pending != null)
            {
                sellLabel.text = "이번 소환 포기";
                ShowDetail(pending,
                    "빈 칸 배치 · 같은 유닛 합치기 · 다른 유닛 교체",
                    $"같은 유닛·성급 칸에 겹치면 ★{pending.Star + 1}");
                return;
            }

            if (selected != NoSelection)
            {
                sellLabel.text = "선택 유닛 퇴장";
                ShowDetail(placement.Board[selected], string.Empty, MergeHintFor(selected));
                return;
            }

            ShowDetail(null, "확정하면 유닛이 소환됩니다", null);
        }

        // 고른 유닛이 지금 머지할 수 있는지 한 줄로 안내
        string MergeHintFor(int index)
        {
            UnitInstance unit = placement.Board[index];

            if (unit.IsMaxStar)
            {
                return "최대 성급 - 더 못 올림";
            }

            if (placement.Board.HasMergePartner(index))
            {
                return $"★{unit.Star + 1}로 합칠 상대가 있습니다";
            }

            return $"같은 유닛 ★{unit.Star} 둘이면 ★{unit.Star + 1}";
        }

        // 승급권 버튼이 비활성인 이유
        string JokerBlockReason(UnitInstance unit)
        {
            if (unit.IsMaxStar)
            {
                return "이미 최대 성급";
            }

            if (stage.Stage.Jokers <= 0)
            {
                return "승급권 없음";
            }

            return "사용 불가";
        }

        // 유닛 상세 정보 표시
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
