using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * 유닛 배치 페이즈 컨트롤러
     *
     * 판정 결과를 유닛 1기로 바꿔 들고 있다가 플레이어가 고른 슬롯에 배치
     */
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] HandUnitTable unitTable;

        readonly GridBoard board = new GridBoard();

        // 배치를 기다리는 유닛이 생기거나 사라졌을 때 호출하는 이벤트 (인자: 대기 유닛 or null)
        public event Action<UnitInstance> PendingChanged;

        // 그리드 슬롯 내용이 바뀌었을 때 호출하는 이벤트 (인자: 방금 바뀐 슬롯과 변경 결과)
        public event Action<int, PlacementResult> Placed;

        public GridBoard Board => board;

        public UnitInstance Pending { get; private set; }

        // true일 경우 들고 있는 유닛을 놓을 자리가 아예 없는 상태
        public bool IsStuck => Pending != null && board.CanAccept(Pending) == false;

        void Awake()
        {
            round.Evaluated += OnEvaluated;
        }

        void OnEvaluated(HandResult result)
        {
            Pending = new UnitInstance(unitTable.For(result.Category));
            PendingChanged?.Invoke(Pending);
        }

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

            Placed?.Invoke(to, PlacementResult.Merged);
            return true;
        }

        // 놓을 자리가 없을 때 유닛을 포기
        // §7 열린 이슈 2번이 정해지면 이 처리도 바뀐다
        public void DiscardPending()
        {
            if (Pending == null)
            {
                return;
            }

            Pending = null;
            PendingChanged?.Invoke(null);
        }
    }
}
