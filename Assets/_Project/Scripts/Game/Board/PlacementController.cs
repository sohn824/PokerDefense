using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * 유닛 배치 페이즈 컨트롤러
     *
     * 판정 결과를 유닛 1기로 바꿔 들고 있다가 플레이어가 고른 슬롯에 배치
     * Place 단계에서 가능한 행동을 전부 관리
     * 배치 / 머지 / 이동 / 판매 / 지원 소환 / Joker 사용
     */
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] StageController stage;
        [SerializeField] HandUnitTable unitTable;

        readonly GridBoard board = new GridBoard();
        // 배치를 기다리는 유닛이 생기거나 사라졌을 때 호출하는 이벤트 (인자: 대기 유닛 or null)
        public event Action<UnitInstance> PendingChanged;

        // 그리드 슬롯 내용이 바뀌었을 때 호출하는 이벤트 (인자: 방금 바뀐 슬롯과 변경 결과)
        public event Action<int, PlacementResult> Placed;

        // 배치와 무관하게 보드가 바뀌었을 때 (이동 / 판매)
        public event Action BoardChanged;

        public GridBoard Board => board;

        public UnitInstance Pending { get; private set; }

        void Awake()
        {
            round.Evaluated += OnEvaluated;
        }

        void OnEvaluated(HandResult result)
        {
            Pending = new UnitInstance(unitTable.GetDefinition(result.Category));
            stage.Stats.RecordSummon(Pending);
            PendingChanged?.Invoke(Pending);
        }

        // Joker를 써서 고른 유닛의 성급을 한 단계 올릴 수 있는지
        public bool CanUseJokerOn(int index)
            => stage.Stage.Jokers > 0 && board[index] != null && board[index].IsMaxStar == false;

        // 고른 슬롯에 배치하거나 머지
        // 실패했을 경우 false를 반환하고 아무것도 바뀌지 않음
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
            stage.Stats.RecordUnit(board[index]);
            Placed?.Invoke(index, result);
            PendingChanged?.Invoke(null);
            return true;
        }

        // 그리드에 이미 있는 두 유닛을 합침
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

        // Joker로 머지 없이 성급을 올리기
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
            Placed?.Invoke(index, PlacementResult.Merged);
            return true;
        }

        // 유닛을 빈 칸으로 옮기기
        public bool TryMoveSlot(int from, int to)
        {
            if (board.TryMoveSlot(from, to) == false)
            {
                return false;
            }

            BoardChanged?.Invoke();
            return true;
        }

        // 그리드에 있는 서로 다른 두 유닛의 자리를 맞바꾸기
        public bool TrySwapSlots(int from, int to)
        {
            if (board.TrySwapSlots(from, to) == false)
            {
                return false;
            }

            BoardChanged?.Invoke();
            return true;
        }

        // 퇴장·교체는 UI 확인 뒤에만 호출한다. 보드 변화도 기록한다.
        public bool TryRetireSlot(int index)
        {
            if (GameSession.IsPaused || Pending != null || board[index] == null)
            {
                return false;
            }
            board.TakeAt(index);
            BoardChanged?.Invoke();
            return true;
        }

        public bool TryDiscardPending()
        {
            if (GameSession.IsPaused || Pending == null)
            {
                return false;
            }
            Pending = null;
            PendingChanged?.Invoke(null);
            return true;
        }

        public bool TryReplacePending(int index, UnitInstance expected)
        {
            if (GameSession.IsPaused || Pending == null || board.TryReplace(index, expected, Pending) == false)
            {
                return false;
            }
            Pending = null;
            stage.Stats.RecordUnit(board[index]);
            Placed?.Invoke(index, PlacementResult.Replaced);
            PendingChanged?.Invoke(null);
            return true;
        }
    }
}
