using System;
using System.Collections.Generic;
using System.IO;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RunSaver
     *
     * 진행 중인 Run을 파일에 저장하고, 이어하기를 고르면 저장한 상태로 되돌리는 객체
     * 파일 읽기·쓰기는 RunSaveStore, 값 되돌리기는 각 Controller의 Restore가 맡고 이 객체는 순서만 정함
     *
     * 저장 - Run 상태가 바뀌었다는 이벤트가 오면 표시만 해 두고 프레임 끝(LateUpdate)에 한 번 씀
     * 앱이 백그라운드로 가거나 꺼질 때는 바로 씀
     * 저장하지 않는 때 - 라운드 시작 전, ForceHand(치트)를 쓴 라운드, 자동 플레이 시뮬레이션
     * 지우는 때 - Run이 끝났을 때(게임 오버·마지막 웨이브 클리어), 앱 종료가 아닌데 Game 씬을 벗어날 때(메뉴의 포기)
     * 메뉴의 "저장하고 타이틀로"(SaveAndLeave)로 나갈 때는 지우지 않음
     * 에디터에서 Play를 멈추는 것은 앱 종료로 보므로 지우지 않음
     *
     * 복원 순서 - 값을 모두 만들고 검사한 뒤 스테이지 -> 보드·배치 대기 -> 라운드 -> 진행(라운드 번호·시간)
     * 그다음 전투 중이었으면 남은 전투를 즉시 계산하고, 라운드 결과 화면이었으면 다음 손패부터 시작함
     */
    public sealed class RunSaver : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] RoundController round;
        [SerializeField] PlacementController placement;
        [SerializeField] CombatController combat;
        [SerializeField] StageController stage;
        [SerializeField] HandUnitTable unitTable;

        // 저장 파일 폴더 (타이틀의 이어하기 버튼도 이 폴더를 봄)
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "RunSave");

        RunSaveStore store;
        // 마지막 저장 뒤 Run 상태가 바뀌었는지
        bool dirty;
        // 앱이 종료되는 중인지 (종료 중에 Game 씬이 사라지는 것은 포기가 아님)
        bool quitting;
        // 메뉴의 "저장하고 타이틀로"로 나가는 중인지 (이때도 저장을 지우지 않음)
        bool leavingWithSave;

        void Awake()
        {
            store = new RunSaveStore(SaveDirectory);

            round.HandChanged += OnHandChanged;
            round.PhaseChanged += OnPhaseChanged;
            round.AssistanceChanged += MarkDirty;
            placement.PendingChanged += OnPendingChanged;
            placement.Placed += OnPlaced;
            placement.BoardChanged += MarkDirty;
            combat.CombatStarted += OnCombatStarted;
            combat.CombatFinished += OnCombatFinished;
            stage.Changed += MarkDirty;
            flow.FlowChanged += MarkDirty;
        }

        void OnDestroy()
        {
            round.HandChanged -= OnHandChanged;
            round.PhaseChanged -= OnPhaseChanged;
            round.AssistanceChanged -= MarkDirty;
            placement.PendingChanged -= OnPendingChanged;
            placement.Placed -= OnPlaced;
            placement.BoardChanged -= MarkDirty;
            combat.CombatStarted -= OnCombatStarted;
            combat.CombatFinished -= OnCombatFinished;
            stage.Changed -= MarkDirty;
            flow.FlowChanged -= MarkDirty;

            // 메뉴에서 포기해 타이틀로 가면 이 Run은 다시 이을 수 없음
            if (quitting == false && leavingWithSave == false)
            {
                store.Delete();
            }
        }

        // 메뉴의 "저장하고 타이틀로" - 지금 상태를 바로 저장한 뒤 타이틀로 감 (타이틀에서 이어하기로 이어 감)
        public void SaveAndLeave()
        {
            leavingWithSave = true;
            Write();
            GameSession.Load(GameSession.TitleScene);
        }

        void MarkDirty() => dirty = true;
        void OnHandChanged(IReadOnlyList<Card> hand) => dirty = true;
        void OnPhaseChanged(RoundPhase phase) => dirty = true;
        void OnPendingChanged(UnitInstance pending) => dirty = true;
        void OnPlaced(int index, PlacementResult result) => dirty = true;
        void OnCombatStarted(CombatContext context) => dirty = true;
        void OnCombatFinished(CombatOutcome outcome, int unresolved) => dirty = true;

        void LateUpdate()
        {
            if (dirty)
            {
                Write();
            }
        }

        // 모바일에서는 백그라운드로 간 뒤 그대로 종료될 수 있으므로 바로 씀
        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Write();
            }
        }

        void OnApplicationQuit()
        {
            quitting = true;
            Write();
        }

        void Write()
        {
            dirty = false;

            if (flow.IsFinished)
            {
                store.Delete();
                return;
            }

            if (CanSave() == false)
            {
                return;
            }

            store.Save(Capture());
        }

        bool CanSave()
        {
#if UNITY_EDITOR
            if (flow.IsSimulated)
            {
                return false;
            }
#endif
            return round.IsReplayable;
        }

        RunSaveData Capture()
        {
            StageContext context = stage.Stage;
            RunStats stats = stage.Stats;
            RunSaveData data = new RunSaveData
            {
                life = context.Life,
                jokers = context.Jokers,
                waveIndex = context.WaveIndex,
                roundNumber = flow.RoundNumber,
                elapsedSeconds = flow.CurrentElapsedSeconds,
                inCombat = combat.IsFighting,
                waitingForBreak = flow.IsWaitingForBreak,
                roundSeed = round.LastSeed,
                summons = stats.Summons,
                bestHand = stats.BestHand.HasValue ? (int)stats.BestHand.Value : -1,
            };
#if UNITY_EDITOR
            data.usedDevSkip = flow.UsedDevSkip;
#endif

            foreach (RoundAction action in round.Actions)
            {
                data.roundActions.Add(RoundActionData.From(action));
            }

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                UnitInstance unit = placement.Board[i];
                data.boardUnitIds[i] = unit == null ? string.Empty : unit.Definition.Id;
                data.boardStars[i] = unit == null ? 0 : unit.Star;
            }

            if (placement.Pending != null)
            {
                data.pendingUnitId = placement.Pending.Definition.Id;
                data.pendingStar = placement.Pending.Star;
            }

            if (stats.BestUnit != null)
            {
                data.bestUnitId = stats.BestUnit.Definition.Id;
                data.bestUnitStar = stats.BestUnit.Star;
            }

            return data;
        }

        // 이어하기를 골랐고 쓸 수 있는 저장이 있으면 복원하고 true
        // 저장이 없거나 쓸 수 없으면 새 Run으로 시작하도록 false (쓸 수 없는 저장은 지우거나 .bad로 옮김)
        public bool TryResume()
        {
            if (GameSession.ConsumeContinueRequest() == false)
            {
                return false;
            }

            RunSaveLoadResult result = store.TryLoad(out RunSaveData data);

            if (result != RunSaveLoadResult.Loaded)
            {
                if (result == RunSaveLoadResult.Invalid)
                {
                    Debug.LogWarning("이어하기 저장을 읽을 수 없어 새 Run으로 시작함", this);
                }
                return false;
            }

            try
            {
                Apply(data);
            }
            catch (Exception e)
            {
                // Apply는 값을 모두 검사한 뒤에야 상태를 바꾸므로, 여기로 오면 아무것도 바뀌지 않은 상태임
                Debug.LogWarning("이어하기 저장이 맞지 않아 새 Run으로 시작함: " + e.Message, this);
                store.Delete();
                return false;
            }

            return true;
        }

        void Apply(RunSaveData data)
        {
            // 1. 상태를 바꾸기 전에 값을 모두 만들고 검사함 (중간에 실패해 반쯤 복원된 Run이 남지 않게)
            UnitInstance[] slots = new UnitInstance[GridBoard.SlotCount];

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = FindUnit(data.boardUnitIds[i], data.boardStars[i]);
            }

            UnitInstance pending = FindUnit(data.pendingUnitId, data.pendingStar);
            UnitInstance bestUnit = FindUnit(data.bestUnitId, data.bestUnitStar);
            HandCategory? bestHand = null;

            if (data.bestHand >= 0)
            {
                if (Enum.IsDefined(typeof(HandCategory), data.bestHand) == false)
                {
                    throw new InvalidOperationException($"알 수 없는 족보: {data.bestHand}");
                }
                bestHand = (HandCategory)data.bestHand;
            }

            List<RoundAction> actions = new List<RoundAction>();

            foreach (RoundActionData action in data.roundActions)
            {
                actions.Add(action.ToAction());
            }

            // 재현해 보고 기록이 맞지 않으면 여기서 예외 (RoundController에 넣기 전에 확인)
            RoundContext replayed = RoundContext.Replay(data.roundSeed, actions);

            if (data.roundNumber < 1)
            {
                throw new InvalidOperationException($"라운드 번호가 잘못됨: {data.roundNumber}");
            }

            // 전투 중·라운드 결과 화면은 손패를 확정하고 배치 대기 유닛을 다 놓은 뒤에만 올 수 있음
            if ((data.inCombat || data.waitingForBreak) && (replayed.Phase != RoundPhase.Place || pending != null))
            {
                throw new InvalidOperationException("전투 중·결과 화면 저장인데 배치를 마치지 않은 라운드임");
            }

            // 2. 되돌림 - StageController.Restore는 값을 검사한 뒤에만 바꾸므로 여기가 마지막 실패 지점임
            stage.Restore(data.life, data.jokers, data.waveIndex, data.summons, bestHand, bestUnit);
            placement.Restore(slots, pending);
            round.Restore(data.roundSeed, actions);
            flow.RestoreProgress(data.roundNumber, data.elapsedSeconds, data.usedDevSkip);

            // 3. 끊긴 지점에서 이어 감
            if (data.inCombat)
            {
                combat.ResolveImmediately();
            }
            else if (data.waitingForBreak)
            {
                flow.ContinueAfterRestoredBreak();
            }
        }

        // id·성급으로 유닛을 다시 만듦 (id가 비어 있으면 null, 없는 id면 예외)
        UnitInstance FindUnit(string id, int star)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            UnitDefinition definition = unitTable.FindById(id);

            if (definition == null)
            {
                throw new InvalidOperationException($"저장한 유닛을 찾을 수 없음: {id}");
            }

            return UnitInstance.Restore(definition, star);
        }
    }
}
