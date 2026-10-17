using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * PlacementResult
     *
     * 유닛을 슬롯에 놓거나 합친 결과 (PlacementController.Placed로 UI·RunJournal에 전달)
     */
    public enum PlacementResult
    {
        // 빈 칸에 새로 놓음
        Placed,

        // 같은 종류 + 같은 성급 유닛과 합침 (슬롯끼리 머지, 승급권 사용도 이 값)
        Merged,

        // 놓거나 합칠 수 없음 (그 칸에 다른 종류·다른 성급·★3 유닛이 있음)
        Rejected,

        // 기존 유닛을 퇴장시키고 새 유닛 배치
        Replaced,
    }

    /**
     * GridBoard
     *
     * 15칸(5열 × 3행) 보드에 놓인 유닛을 갖고, 보드 규칙을 검사·실행하는 객체
     * 보드 규칙 - 배치, 머지, 이동, 자리 교환, 교체, 퇴장, 승급
     *
     * 쓰는 곳
     * PlacementController - 하나 만들어 Run 동안 들고 있으며 보드를 바꿈
     * CombatContext - 전투 중 슬롯의 유닛을 읽어 공격시킴
     *
     * 좌표·트랙 값도 여기서 제공
     * SlotToLocalPosition - 슬롯 번호 -> 슬롯 좌표
     * Track으로 시작하는 멤버 - 적이 걷는 트랙의 위치·방향·길이 (계산은 TrackPath.Arena가 함)
     * 단위 - 보드 중심이 원점, 한 칸 = 1 (유닛 사거리도 같은 단위)
     */
    public sealed class GridBoard
    {
        public const int Columns = 5;
        public const int Rows = 3;
        public const int SlotCount = Columns * Rows;

        // 슬롯 한 칸의 월드 크기 (유닛 사거리도 같은 단위)
        public const float CellSize = 1f;

        readonly UnitInstance[] slots = new UnitInstance[SlotCount];

        // 슬롯 번호 -> 보드 중심을 원점으로 하는 로컬 좌표
        // 번호 = 행 × 5 + 열 (ex: 0번 좌상단, 7번 정중앙 = 원점, 14번 우하단)
        public static Vector2 SlotToLocalPosition(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            int column = index % Columns;
            int row = index / Columns;

            float x = (column - (Columns - 1) * 0.5f) * CellSize;
            float y = ((Rows - 1) * 0.5f - row) * CellSize;

            return new Vector2(x, y);
        }

        // 배경 아트에 맞춘 트랙(TrackPath.Arena) 값을 그대로 돌려줌
        // 전투 판정과 화면이 모두 여기서 읽으므로 적이 걷는 경로가 어긋나지 않음
        // progress - 0 = 시작점, 1 = 한 바퀴
        // TrackHalfWidth·TrackHalfHeight는 테스트에서만 씀 (CombatTests - 트랙이 보드를 감싸는지, 트랙 위 점이 경계선에 있는지)
        public static float TrackHalfWidth => TrackPath.Arena.HalfWidth;
        public static float TrackHalfHeight => TrackPath.Arena.HalfHeight;
        public static float TrackLength => TrackPath.Arena.Length;

        public static Vector2 TrackPosition(float progress) => TrackPath.Arena.Position(progress);

        // progress 지점에서 적이 바라볼 방향을 상하좌우 중 하나로 고름 (CombatScreen의 적 아트 선택용)
        // progress - 한 바퀴 중 얼마나 왔는지 (0 = 시작점, 0.5 = 반 바퀴)
        // TrackPath.MoveDirection - 그 지점에서 움직이는 쪽을 정규화된 방향 벡터로 돌려줌
        // 예) (1, 0)이면 오른쪽으로, (0, -1)이면 아래로 걷는 중
        // x 크기가 y보다 크거나 같으면 Left·Right, 작으면 Up·Down
        public static AimDirection TrackDirection(float progress)
        {
            Vector2 moveDirection = TrackPath.Arena.MoveDirection(progress);
            if (Mathf.Abs(moveDirection.x) >= Mathf.Abs(moveDirection.y))
            {
                return moveDirection.x >= 0f ? AimDirection.Right : AimDirection.Left;
            }
            return moveDirection.y >= 0f ? AimDirection.Up : AimDirection.Down;
        }

        // index 슬롯의 유닛 (빈 칸이면 null, 읽기 전용)
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

        // 유닛이 있는 슬롯 수 (테스트에서만 씀)
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

        // index 슬롯에 unit을 놓을 수 있는지 (빈 칸이거나 머지할 수 있으면 true, TryPlace와 같은 판정)
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

        // 보드 어딘가에 unit을 놓을 수 있는지 (빈 칸이나 머지 상대가 하나라도 있으면 true, 테스트에서만 씀)
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

        // index 슬롯의 expected를 replacement로 바꿈 (기존 유닛은 사라짐)
        // 확인 창에서 교체를 누른 뒤 호출되므로, 그사이 슬롯 유닛이 바뀌었으면 바꾸지 않음
        // 범위 밖이거나, null이 들어왔거나, 슬롯 유닛이 expected가 아니거나, 두 유닛을 합칠 수 있으면 실패
        public bool TryReplace(int index, UnitInstance expected, UnitInstance replacement)
        {
            if (index < 0 || index >= SlotCount || expected == null || replacement == null
                || slots[index] != expected || expected.CanMergeWith(replacement))
            {
                return false;
            }
            slots[index] = replacement;
            return true;
        }

        // 빈 칸이면 배치(Placed), 합칠 수 있는 유닛(같은 종류·같은 성급)이 있으면 머지(Merged)
        // 그 밖의 유닛이 있으면 Rejected (보드는 그대로)
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

        // from 유닛을 to 유닛에 합칠 수 있는지
        // 같은 슬롯이거나, 빈 칸이 섞였거나, 종류·성급이 다르거나 ★3이면 false
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

        // from 유닛을 to 유닛에 합침 -> from은 빈 칸, to는 성급 + 1
        // 합칠 수 없으면 Rejected
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

        // from 유닛을 빈 칸 to로 옮김
        // 같은 슬롯이거나, from이 빈 칸이거나, to에 유닛이 있으면 실패
        public bool TryMoveSlot(int from, int to)
        {
            if (from < 0 || from >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from), $"슬롯 범위를 벗어남: {from}");
            }
            if (to < 0 || to >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(to), $"슬롯 범위를 벗어남: {to}");
            }

            if (from == to || slots[from] == null || slots[to] != null)
            {
                return false;
            }

            slots[to] = slots[from];
            slots[from] = null;
            return true;
        }

        // 유닛이 있는 두 슬롯의 자리를 맞바꿈
        // 머지 가능한지는 따로 검사하지 않음 (머지할지는 BoardScreen이 이 메서드를 부르기 전에 판단)
        // 같은 슬롯이거나 둘 중 하나가 빈 칸이면 실패
        public bool TrySwapSlots(int from, int to)
        {
            if (from < 0 || from >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from), $"슬롯 범위를 벗어남: {from}");
            }
            if (to < 0 || to >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(to), $"슬롯 범위를 벗어남: {to}");
            }

            if (from == to || slots[from] == null || slots[to] == null)
            {
                return false;
            }

            UnitInstance temp = slots[from];
            slots[from] = slots[to];
            slots[to] = temp;
            return true;
        }

        // 승급권(Joker)을 쓸 때 index 유닛의 성급을 짝 없이 1 올림
        // 빈 칸이거나 ★3이면 실패
        public bool TryPromoteAt(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            if (slots[index] == null || slots[index].IsMaxStar)
            {
                return false;
            }

            slots[index] = slots[index].Promoted();
            return true;
        }

        // index 슬롯을 비움 (유닛 퇴장용)
        public void ClearAt(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            slots[index] = null;
        }

        // index 유닛과 합칠 수 있는 유닛이 다른 슬롯에 있는지 여부 반환(빈 칸이면 false)
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

        // 모든 슬롯을 비움 (테스트에서만 씀)
        public void Clear()
        {
            Array.Clear(slots, 0, SlotCount);
        }
    }
}
