using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * BoardAction
     *
     * 플레이어가 한 보드 조작의 종류 (PlacementController.BoardActed의 인자)
     * Placed·BoardChanged·PendingChanged는 보드가 어떻게 바뀌었는지만 알려서, 어떤 조작이었는지는 이 값으로 구분함
     * RunJournal이 행동 기록에 씀
     */
    public enum BoardAction
    {
        // 승급권(Joker)으로 성급을 올림
        JokerUsed,

        // 유닛을 빈 칸으로 옮김
        Moved,

        // 두 유닛의 자리를 맞바꿈
        Swapped,

        // 보드의 유닛을 퇴장시킴
        Retired,

        // Pending을 놓지 않고 버림 (소환 포기)
        Discarded,
    }

    /**
     * PlacementController
     *
     * 족보에 맞는 유닛을 Pending(배치 대기)으로 소환하고, 보드 조작을 실행해 결과를 이벤트로 알리는 객체
     * 보드 조작은 모두 여기를 거침 (전투 중에도 RoundPhase는 Place라 조작할 수 있음)
     *
     * 1. 족보 확정 -> 그 족보의 ★1 유닛을 Pending에 둠
     * 2. Pending 처리 - 배치·머지(TryPlace), 교체(TryReplacePending), 포기(TryDiscardPending)
     * Pending이 비어야 전투를 시작할 수 있음
     * 3. 배치 후 조정 - 머지(TryMergeSlots), 이동(TryMoveSlot), 교환(TrySwapSlots), 승급권(TryUseJoker), 퇴장(TryRetireSlot)
     */
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] StageController stage;
        [SerializeField] HandUnitTable unitTable;

        // 이 Run의 보드 (라운드가 바뀌어도 유지됨)
        readonly GridBoard board = new GridBoard();

        // Pending이 바뀔 때 발생 (인자: 새 배치 대기 유닛, 배치를 마치거나 교체·포기해서 비면 null)
        // 구독 - BoardScreen, ActionBarController
        public event Action<UnitInstance> PendingChanged;

        // 슬롯에 유닛이 놓이거나 성급이 오를 때 발생 (인자: 바뀐 슬롯 인덱스, 배치 결과)
        // 구독 - BoardScreen, ActionBarController, RunJournal
        public event Action<int, PlacementResult> Placed;

        // 유닛 수나 성급은 그대로이고 자리만 바뀌거나 유닛이 빠질 때 발생 (이동·자리 교환·퇴장)
        // 구독 - BoardScreen, ActionBarController
        public event Action BoardChanged;

        // 승급권 사용·이동·자리 교환·퇴장·소환 포기가 성공했을 때 발생 (인자: 조작 종류)
        // 승급권을 쓰면 이 이벤트 뒤에 Placed도 발생함
        // 이때 Placed는 결과 값이 Merged라 일반 머지와 구분되지 않음
        // 구독 - RunJournal
        public event Action<BoardAction> BoardActed;

        public GridBoard Board => board;

        // 배치를 기다리는 소환 유닛 (없으면 null)
        public UnitInstance Pending { get; private set; }

        void Awake()
        {
            round.Evaluated += OnEvaluated;
        }

        // 족보 확정 시 호출 - ★1 유닛을 Pending에 두고 PendingChanged로 알림
        // 소환 수(RunStats)는 여기가 아니라 Pending을 실제로 놓을 때 셈, 소환을 포기한 유닛은 세지 않기 위함
        void OnEvaluated(HandResult result)
        {
            Pending = new UnitInstance(unitTable.GetDefinition(result.Category));
            PendingChanged?.Invoke(Pending);
        }

        // index 유닛에 승급권(Joker)을 쓸 수 있는지
        public bool CanUseJokerOn(int index)
            => stage.Stage.Jokers > 0 && board[index] != null && board[index].IsMaxStar == false;

        // Pending을 index 슬롯에 배치하거나 머지
        // Pending이 없거나, 그 칸에 합칠 수 없는 유닛(다른 종류·다른 성급·★3)이 있으면 실패
        public bool TryPlace(int index)
        {
            if (Pending == null)
            {
                return false;
            }

            PlacementResult result = board.TryPlace(index, Pending);

            if (result == PlacementResult.Rejected)
            {
                return false;
            }

            Pending = null;
            stage.Stats.RecordSummon(board[index]);
            Placed?.Invoke(index, result);
            PendingChanged?.Invoke(null);
            return true;
        }

        // from 유닛을 to 유닛에 합침 (from은 빈 칸, to는 성급 + 1)
        // 같은 슬롯이거나, 빈 칸이 섞였거나, 두 유닛의 종류·성급이 다르거나 ★3이면 실패
        public bool TryMergeSlots(int from, int to)
        {
            if (board.TryMergeSlots(from, to) == PlacementResult.Rejected)
            {
                return false;
            }

            stage.Stats.RecordUnit(board[to]);
            Placed?.Invoke(to, PlacementResult.Merged);
            return true;
        }

        // 승급권(Joker) 1장을 써서 index 유닛의 성급을 짝 없이 1 올림
        // 승급권이 없거나, 빈 칸이거나, ★3이면 실패
        // BoardActed에 JokerUsed로 알린 뒤 Placed에도 Merged로 알림 (UI는 머지와 같은 연출을 씀)
        public bool TryUseJoker(int index)
        {
            if (CanUseJokerOn(index) == false)
            {
                return false;
            }

            if (stage.TryUseJoker() == false)
            {
                return false;
            }

            board.TryPromoteAt(index);
            stage.Stats.RecordUnit(board[index]);
            BoardActed?.Invoke(BoardAction.JokerUsed);
            Placed?.Invoke(index, PlacementResult.Merged);
            return true;
        }

        // from 유닛을 빈 칸 to로 옮김
        // 같은 슬롯이거나, from이 빈 칸이거나, to에 유닛이 있으면 실패
        public bool TryMoveSlot(int from, int to)
        {
            if (board.TryMoveSlot(from, to) == false)
            {
                return false;
            }

            BoardChanged?.Invoke();
            BoardActed?.Invoke(BoardAction.Moved);
            return true;
        }

        // 유닛이 있는 두 슬롯의 자리를 맞바꿈
        // 같은 슬롯이거나 둘 중 하나가 빈 칸이면 실패
        public bool TrySwapSlots(int from, int to)
        {
            if (board.TrySwapSlots(from, to) == false)
            {
                return false;
            }

            BoardChanged?.Invoke();
            BoardActed?.Invoke(BoardAction.Swapped);
            return true;
        }

        // index 유닛을 퇴장시킴 (유닛은 사라지고 돌아오지 않음)
        // 일시정지 중이거나, Pending이 있거나, 빈 칸이면 실패
        public bool TryRetireSlot(int index)
        {
            if (GameSession.IsPaused || Pending != null || board[index] == null)
            {
                return false;
            }
            board.ClearAt(index);
            BoardChanged?.Invoke();
            BoardActed?.Invoke(BoardAction.Retired);
            return true;
        }

        // Pending을 놓지 않고 버림 (소환 포기, 이번 라운드에는 다시 소환되지 않음)
        // 일시정지 중이거나 Pending이 없으면 실패
        public bool TryDiscardPending()
        {
            if (GameSession.IsPaused || Pending == null)
            {
                return false;
            }
            Pending = null;
            PendingChanged?.Invoke(null);
            BoardActed?.Invoke(BoardAction.Discarded);
            return true;
        }

        // index 슬롯의 expected를 퇴장시키고 Pending을 그 자리에 놓음
        // expected - 확인 창을 열 때 그 슬롯에 있던 유닛 (창을 연 뒤 바뀌었으면 교체하지 않음)
        // 일시정지 중이거나, Pending이 없거나, GridBoard.TryReplace가 막으면 실패
        public bool TryReplacePending(int index, UnitInstance expected)
        {
            if (GameSession.IsPaused || Pending == null || board.TryReplace(index, expected, Pending) == false)
            {
                return false;
            }
            Pending = null;
            stage.Stats.RecordSummon(board[index]);
            Placed?.Invoke(index, PlacementResult.Replaced);
            PendingChanged?.Invoke(null);
            return true;
        }
    }
}
