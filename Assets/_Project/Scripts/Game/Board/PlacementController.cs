using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /// <summary>
    /// Place 페이즈를 구동한다. 판정 결과를 유닛 1기로 바꿔 들고 있다가 플레이어가 고른 슬롯에 넘긴다.
    /// 보드는 라운드보다 오래 살기 때문에 RoundController와 분리했다.
    /// UI는 이 컴포넌트의 이벤트를 구독하고 입력만 되돌려준다 (DESIGN §2).
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] HandUnitTable unitTable;

        readonly GridBoard board = new GridBoard();

        /// <summary>배치를 기다리는 유닛. 없으면 null.</summary>
        public event Action<UnitInstance> PendingChanged;

        /// <summary>슬롯 내용이 바뀌었다. 인자는 방금 바뀐 슬롯과 그 결과.</summary>
        public event Action<int, PlacementResult> Placed;

        public GridBoard Board => board;

        public UnitInstance Pending { get; private set; }

        /// <summary>들고 있는 유닛을 놓을 자리가 아예 없는 상태 (DESIGN §7 열린 이슈 2번).</summary>
        public bool IsStuck => Pending != null && !board.CanAccept(Pending);

        void Awake()
        {
            round.Evaluated += OnEvaluated;
        }

        void OnEvaluated(HandResult result)
        {
            Pending = new UnitInstance(unitTable.For(result.Category));
            PendingChanged?.Invoke(Pending);
        }

        /// <summary>고른 슬롯에 배치하거나 머지한다. 거부되면 false를 돌려주고 아무것도 바뀌지 않는다.</summary>
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

        /// <summary>보드에 이미 있는 두 유닛을 합친다. 배치 대기 유닛과는 무관하다.</summary>
        public bool TryMergeSlots(int from, int to)
        {
            if (board.TryMergeSlots(from, to) == PlacementResult.Rejected)
            {
                return false;
            }

            Placed?.Invoke(to, PlacementResult.Merged);
            return true;
        }

        /// <summary>놓을 자리가 없을 때 유닛을 포기한다. §7 열린 이슈 2번이 정해지면 이 처리도 바뀐다.</summary>
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
