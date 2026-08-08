using System;

namespace PokerDefense.Game
{
    public enum PlacementResult
    {
        // 빈 칸에 새로 놓기
        Placed,

        // 같은 유닛 + 같은 성급과 합치기
        Merged,

        // 그리드에 다른 유닛이 있거나 최대 성급이라 놓을 수 없음
        Rejected,
    }

    /**
     * GridBoard
     * 
     * 5열 × 3행 = 15슬롯 보드
     * 유닛 배치, 유닛 머지 규칙을 소유
     * 
     * 슬롯 ↔ 월드 좌표 변환은 아직 없다. 유닛이 월드의 적을 조준해야 하는 M4에서 붙인다.
     */
    public sealed class GridBoard
    {
        public const int Columns = 5;
        public const int Rows = 3;
        public const int SlotCount = Columns * Rows;

        readonly UnitInstance[] slots = new UnitInstance[SlotCount];

        // UnitInstance의 indexer
        // UnitInstance 객체를 배열 인덱스 접근 시 get 내부를 실행
        public UnitInstance this[int index]
        {
            get
            {
                if (index < 0 || index >= SlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
                }

                return slots[index];
            }
        }

        // 사용 중인 슬롯 개수 getter
        public int OccupiedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < SlotCount; i++)
                {
                    if (slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // 그리드 슬롯에 유닛을 놓을 수 있는지 검사
        public bool CanPlaceAt(int index, UnitInstance unit)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return slots[index] == null || slots[index].CanMergeWith(unit);
        }

        // 그리드 전체에 유닛을 놓을 자리가 있는지 검사 (빈 칸 혹은 머지 가능한 대상이 있는지)
        public bool CanAccept(UnitInstance unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null || slots[i].CanMergeWith(unit))
                {
                    return true;
                }
            }

            return false;
        }

        // 그리드 슬롯이 빈칸이면 유닛 배치, 같은 유닛/성급이면 머지
        // 그 외에는 거부 (Reject)
        public PlacementResult TryPlace(int index, UnitInstance unit)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (slots[index] == null)
            {
                slots[index] = unit;
                return PlacementResult.Placed;
            }

            if (slots[index].CanMergeWith(unit))
            {
                slots[index] = slots[index].Promoted();
                return PlacementResult.Merged;
            }

            return PlacementResult.Rejected;
        }

        // 두 슬롯에 있는 유닛끼리 합칠 수 있는지 검사
        public bool CanMergeSlots(int from, int to)
        {
            if (from < 0 || from >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from), $"슬롯 범위를 벗어남: {from}");
            }
            if (to < 0 || to >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(to), $"슬롯 범위를 벗어남: {to}");
            }

            if (from == to)
            {
                return false;
            }

            UnitInstance source = slots[from];
            UnitInstance target = slots[to];

            return source != null && target != null && target.CanMergeWith(source);
        }

        // 그리드에 있는 두 유닛을 합침
        // from 슬롯이 비워지고 to 슬롯의 유닛이 업그레이드 됨
        public PlacementResult TryMergeSlots(int from, int to)
        {
            if (!CanMergeSlots(from, to))
            {
                return PlacementResult.Rejected;
            }

            slots[to] = slots[to].Promoted();
            slots[from] = null;
            return PlacementResult.Merged;
        }

        // 그리드 슬롯에 있는 유닛과 합칠 유닛이 그리드 내에 있는지 검사
        public bool HasMergePartner(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            if (slots[index] == null)
            {
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (i != index && slots[i] != null && slots[i].CanMergeWith(slots[index]))
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            Array.Clear(slots, 0, SlotCount);
        }
    }
}
