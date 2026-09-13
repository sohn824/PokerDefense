using PokerDefense.Game;
using UnityEngine;
using UnityEngine.Serialization;

namespace PokerDefense.UI
{
    /**
     * ActionBarController
     *
     * 행동 버튼들과 안내 라벨들을 단계별로 바꿔 끼우는 클래스
     * 같은 오브젝트의 RoundScreen / BoardScreen / CombatScreen 과 이벤트로만 엮인다
     */
    public sealed class ActionBarController : MonoBehaviour
    {
        // 화면에 실제로 보이는 단계
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
        [SerializeField] GameObject sellButton;
        [SerializeField] GameObject jokerButton;

        [Header("상황 카드")]
        [SerializeField] GameObject situationCard;
        [SerializeField] GameObject handPanel;
        [SerializeField] GameObject categoryLabel;
        [SerializeField] GameObject combatLabel;
        [SerializeField] GameObject statusLabel;
        [SerializeField] GameObject pendingLabel;

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

        // 보드 유닛 선택은 PlacementController 이벤트를 거치지 않으므로 매 프레임 확인한다
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

        // 지금 단계를 판정하고 버튼/라벨 노출을 거기에 맞춘다
        void Apply()
        {
            lastSelection = board.HasSelection;
            Stage stage = Resolve();
            bool combatEdit = (stage == Stage.PlaceReady || stage == Stage.Combat) && lastSelection;

            SetActive(confirmButton, stage == Stage.Exchange);
            SetActive(exchangeButton, stage == Stage.Exchange);
            SetActive(startCombatButton, stage == Stage.PlaceReady);
            // 소환 포기와 퇴장 버튼은 대기/선택 상태에 맞춰 노출한다
            SetActive(sellButton, placement.Pending != null || combatEdit);
            SetActive(jokerButton, combatEdit);

            SetActive(handPanel, stage == Stage.Exchange);
            SetActive(situationCard, stage != Stage.Result);
            SetActive(categoryLabel, stage == Stage.Exchange || stage == Stage.PlacePending);
            SetActive(statusLabel, stage == Stage.Exchange);
            SetActive(pendingLabel, placement.Pending != null || stage == Stage.PlaceReady);
            SetActive(combatLabel, stage == Stage.Combat || stage == Stage.PlaceReady);
        }

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
