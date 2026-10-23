using PokerDefense.Game;
using UnityEngine;
using UnityEngine.Serialization;

namespace PokerDefense.UI
{
    /**
     * ActionBarController
     *
     * 지금 단계에 맞춰 행동 버튼과 안내 라벨을 켜고 끄는 클래스
     * 단계 판정은 컨트롤러(Round·Placement·Combat·GameFlow) 이벤트를 받아 하고, 보드 유닛 선택 여부만 BoardScreen에서 매 프레임 읽음
     */
    public sealed class ActionBarController : MonoBehaviour
    {
        // 화면 기준 단계 (RoundPhase와 달리 배치를 Pending 있음·없음으로 나누고 전투·결과를 따로 둠)
        enum Stage { Exchange, PlacePending, PlaceReady, Combat, Result }

        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] CombatController combat;
        [SerializeField] GameFlowController flow;
        [SerializeField] BoardScreen board;

        [Header("행동 버튼")]
        [SerializeField] GameObject confirmButton;
        [SerializeField] GameObject exchangeButton;
        [SerializeField] GameObject startCombatButton;

        // 소환 포기·퇴장 버튼 (이름은 예전 판매 기능에서 남음)
        [SerializeField] GameObject sellButton;
        [SerializeField] GameObject jokerButton;

        [Header("상황 카드")]
        [SerializeField] GameObject situationCard;
        [SerializeField] GameObject handPanel;
        [SerializeField] GameObject categoryLabel;
        [SerializeField] GameObject combatLabel;
        [SerializeField] GameObject statusLabel;
        [SerializeField] GameObject pendingLabel;

        // 직전 Apply 때의 보드 선택 여부 (바뀌었을 때만 Apply)
        bool lastSelection;

        void Awake()
        {
            round.PhaseChanged += OnPhase;
            placement.PendingChanged += OnPending;
            placement.Placed += OnPlaced;
            placement.BoardChanged += Apply;
            combat.CombatStarted += OnCombatStarted;
            combat.CombatFinished += OnCombatFinished;
            flow.FlowChanged += Apply;
        }

        void Start()
        {
            Apply();
        }

        void OnDestroy()
        {
            round.PhaseChanged -= OnPhase;
            placement.PendingChanged -= OnPending;
            placement.Placed -= OnPlaced;
            placement.BoardChanged -= Apply;
            combat.CombatStarted -= OnCombatStarted;
            combat.CombatFinished -= OnCombatFinished;
            flow.FlowChanged -= Apply;
        }

        // 보드 유닛 선택은 PlacementController 이벤트를 거치지 않으므로 매 프레임 확인함
        void Update()
        {
            if (board.HasSelection != lastSelection)
            {
                Apply();
            }
        }

        void OnPhase(RoundPhase phase) => Apply();
        void OnPending(UnitInstance unit) => Apply();
        void OnPlaced(int index, PlacementResult result) => Apply();
        void OnCombatStarted(CombatContext context) => Apply();
        void OnCombatFinished(CombatOutcome outcome, int unresolved) => Apply();

        // 지금 단계를 판정하고 버튼·라벨 노출을 거기에 맞춤
        void Apply()
        {
            lastSelection = board.HasSelection;
            Stage stage = Resolve();
            // 유닛을 골랐으면 배치 완료 단계와 전투 중에도 퇴장·승급권 버튼을 보여 줌
            bool combatEdit = (stage == Stage.PlaceReady || stage == Stage.Combat) && lastSelection;

            SetActive(confirmButton, stage == Stage.Exchange);
            SetActive(exchangeButton, stage == Stage.Exchange);
            SetActive(startCombatButton, stage == Stage.PlaceReady);
            // 소환 포기·퇴장 버튼은 Pending이 있거나 유닛을 골랐을 때 보여 줌
            SetActive(sellButton, placement.Pending != null || combatEdit);
            SetActive(jokerButton, combatEdit);

            SetActive(handPanel, stage == Stage.Exchange);
            SetActive(situationCard, stage != Stage.Result);
            SetActive(categoryLabel, stage == Stage.Exchange || stage == Stage.PlacePending);
            SetActive(statusLabel, stage == Stage.Exchange);
            SetActive(pendingLabel, placement.Pending != null || stage == Stage.PlaceReady);
            SetActive(combatLabel, stage == Stage.Combat || stage == Stage.PlaceReady);
        }

        // 지금 화면 단계를 판정함 (Run 종료 > 전투 > 교체 > 배치 순)
        Stage Resolve()
        {
            if (flow.IsFinished)
            {
                return Stage.Result;
            }

            if (combat.IsFighting)
            {
                return Stage.Combat;
            }

            if (round.Phase == RoundPhase.Exchange)
            {
                return Stage.Exchange;
            }

            return placement.Pending != null ? Stage.PlacePending : Stage.PlaceReady;
        }

        static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value)
            {
                go.SetActive(value);
            }
        }
    }
}
