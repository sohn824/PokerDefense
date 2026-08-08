using System;

namespace PokerDefense.Game
{
    public enum PlacementResult
    {
        /// <summary>빈 칸에 새로 놓았다.</summary>
        Placed,

        /// <summary>같은 유닛·같은 성급과 합쳐져 ★가 올랐다.</summary>
        Merged,

        /// <summary>다른 유닛이 있거나 ★3이라 놓을 수 없다.</summary>
        Rejected,
    }

    /// <summary>
    /// 5열 × 3행 = 15슬롯 보드 (DESIGN §5.1). 슬롯 점유와 배치·머지 규칙을 소유한다.
    /// MonoBehaviour가 아니라서 EditMode 테스트에서 그대로 쓴다.
    ///
    /// 슬롯 ↔ 월드 좌표 변환은 아직 없다. 유닛이 월드의 적을 조준해야 하는 M4에서 붙인다.
    /// </summary>
    public sealed class GridBoard
    {
        public const int Columns = 5;
        public const int Rows = 3;
        public const int SlotCount = Columns * Rows;

        readonly UnitInstance[] slots = new UnitInstance[SlotCount];

        /// <summary>빈 칸이면 null.</summary>
        public UnitInstance this[int index]
        {
            get
            {
                RequireInRange(index);
                return slots[index];
            }
        }

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

        public bool CanPlaceAt(int index, UnitInstance unit)
        {
            RequireInRange(index);

            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return slots[index] == null || slots[index].CanMergeWith(unit);
        }

        /// <summary>이 유닛을 놓을 자리가 하나라도 있는지. 빈 칸도 머지 대상도 없으면 false.</summary>
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

        /// <summary>
        /// 빈 칸이면 배치, 같은 유닛·같은 성급이면 머지한다. 그 외에는 보드를 건드리지 않고 거부한다.
        /// </summary>
        public PlacementResult TryPlace(int index, UnitInstance unit)
        {
            RequireInRange(index);

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

        /// <summary>보드 위 두 슬롯을 합칠 수 있는지. 보드를 바꾸지 않는다.</summary>
        public bool CanMergeSlots(int from, int to)
        {
            RequireInRange(from);
            RequireInRange(to);

            if (from == to)
            {
                return false;
            }

            UnitInstance source = slots[from];
            UnitInstance target = slots[to];

            return source != null && target != null && target.CanMergeWith(source);
        }

        /// <summary>
        /// 보드에 이미 있는 두 유닛을 합친다. from이 비워지고 to의 성급이 오른다.
        ///
        /// 이게 따로 필요한 이유: 소환된 유닛을 놓을 때만 머지할 수 있으면 ★2 두 기를 합칠 방법이 없어
        /// ★3에 영원히 도달하지 못한다.
        /// </summary>
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

        /// <summary>이 슬롯의 유닛과 합칠 상대가 보드에 있는지.</summary>
        public bool HasMergePartner(int index)
        {
            RequireInRange(index);

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

        static void RequireInRange(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어났다: {index}");
            }
        }
    }
}
