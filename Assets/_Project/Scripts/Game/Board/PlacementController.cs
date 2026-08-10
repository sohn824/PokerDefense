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
        readonly System.Random seedSource = new System.Random();

        SupportSummon supportSummon;

        // 배치를 기다리는 유닛이 생기거나 사라졌을 때 호출하는 이벤트 (인자: 대기 유닛 or null)
        public event Action<UnitInstance> PendingChanged;

        // 그리드 슬롯 내용이 바뀌었을 때 호출하는 이벤트 (인자: 방금 바뀐 슬롯과 변경 결과)
        public event Action<int, PlacementResult> Placed;

        // 배치와 무관하게 보드가 바뀌었을 때 (이동 / 판매)
        public event Action BoardChanged;

        public GridBoard Board => board;

        public UnitInstance Pending { get; private set; }

        // 이번 라운드에 쓴 지원 소환 횟수
        public int SupportSummonsUsed { get; private set; }

        // true일 경우 들고 있는 유닛을 놓을 자리가 아예 없는 상태
        public bool IsStuck => Pending != null && board.CanAccept(Pending) == false;

        public int SupportSummonCost
            => stage.Stage.Perks.SupportSummonCost(stage.Economy.SupportSummonCost);

        // 지원 소환은 대기 유닛을 먼저 처리한 뒤에만 가능하다. 라운드당 횟수 제한이 있다
        public bool CanSupportSummon => Pending == null
                                        && round.Phase == RoundPhase.Place
                                        && SupportSummonsUsed < stage.Economy.SupportSummonsPerRound
                                        && stage.Stage.Chip >= SupportSummonCost
                                        && HasEmptySlot;

        bool HasEmptySlot => board.OccupiedCount < GridBoard.SlotCount;

        void Awake()
        {
            round.Evaluated += OnEvaluated;
            supportSummon = new SupportSummon(stage.Economy, unitTable, seedSource.Next());
        }

        void OnEvaluated(HandResult result)
        {
            SupportSummonsUsed = 0;
            Pending = new UnitInstance(unitTable.For(result.Category));
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

        // 보드의 유닛을 팔아 Chip으로 바꾸기
        public bool TrySellSlot(int index)
        {
            UnitInstance unit = board[index];

            if (unit == null)
            {
                return false;
            }

            board.TakeAt(index);
            stage.AddChip(stage.Economy.SellPriceFor(unit.Star));
            BoardChanged?.Invoke();
            return true;
        }

        // 배치 대기 유닛을 팔기
        public bool TrySellPending()
        {
            if (Pending == null)
            {
                return false;
            }

            stage.AddChip(stage.Economy.SellPriceFor(Pending.Star));
            Pending = null;
            PendingChanged?.Invoke(null);
            return true;
        }

        // Chip으로 랜덤 하위 유닛 ★1을 뽑아 빈 칸에 놓기
        public bool TrySupportSummon()
        {
            if (CanSupportSummon == false)
            {
                return false;
            }

            if (stage.TrySpendChip(SupportSummonCost) == false)
            {
                return false;
            }

            SupportSummonsUsed++;

            UnitInstance unit = new UnitInstance(supportSummon.Draw());
            stage.Stats.RecordSummon(unit);

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                if (board[i] == null)
                {
                    board.TryPlace(i, unit);
                    Placed?.Invoke(i, PlacementResult.Placed);
                    return true;
                }
            }

            return false;
        }
    }
}
