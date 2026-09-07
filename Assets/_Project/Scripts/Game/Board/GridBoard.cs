using System;
using UnityEngine;

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
     * 유닛 배치, 유닛 머지 규칙, 슬롯의 배치 좌표를 소유
     */
    public sealed class GridBoard
    {
        public const int Columns = 5;
        public const int Rows = 3;
        public const int SlotCount = Columns * Rows;

        // 슬롯 한 칸의 월드 크기. 유닛 사거리도 같은 단위로 잰다.
        public const float CellSize = 1f;

        readonly UnitInstance[] slots = new UnitInstance[SlotCount];

        // 슬롯 인덱스 -> 보드 중심을 원점으로 하는 로컬 좌표
        // 0번이 좌상단, 14번이 우하단
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

        // 적이 도는 트랙이 보드 바깥으로 떨어진 거리
        public const float TrackMargin = 0.9f;

        // 적이 도는 트랙은 보드를 감싸는 닫힌 사각 루프
        public static float TrackHalfWidth => Columns * 0.5f * CellSize + TrackMargin;
        public static float TrackHalfHeight => Rows * 0.5f * CellSize + TrackMargin;
        public static float TrackLength => 4f * (TrackHalfWidth + TrackHalfHeight);

        // 트랙 위 적 진행 로직
        // 진행도(progress)는 좌상단에서 시작해 시계 방향으로 한 바퀴가 1임
        // 1을 넘으면 한바퀴 돈 것으로 보고 리셋
        public static Vector2 TrackPosition(float progress)
        {
            float wrapped = progress - Mathf.Floor(progress);
            float distance = wrapped * TrackLength;

            float width = TrackHalfWidth * 2f;
            float height = TrackHalfHeight * 2f;

            // 트랙 위쪽 (진행 방향: 왼쪽 -> 오른쪽)
            if (distance < width)
            {
                return new Vector2(-TrackHalfWidth + distance, TrackHalfHeight);
            }

            distance -= width;

            // 트랙 오른쪽 (진행 방향: 위 -> 아래)
            if (distance < height)
            {
                return new Vector2(TrackHalfWidth, TrackHalfHeight - distance);
            }

            distance -= height;

            // 트랙 아래쪽 (진행 방향: 오른쪽 -> 왼쪽)
            if (distance < width)
            {
                return new Vector2(TrackHalfWidth - distance, -TrackHalfHeight);
            }

            distance -= width;

            // 트랙 왼쪽 (진행 방향: 아래 -> 위쪽)
            return new Vector2(-TrackHalfWidth, -TrackHalfHeight + distance);
        }

        // 트랙 위 진행 방향(TrackPosition과 같은 구간 분기). 적 아트를 이동 방향에 맞춰 고르는 데 쓴다
        public static AimDirection TrackDirection(float progress)
        {
            float wrapped = progress - Mathf.Floor(progress);
            float distance = wrapped * TrackLength;

            float width = TrackHalfWidth * 2f;
            float height = TrackHalfHeight * 2f;

            if (distance < width)
            {
                return AimDirection.Right;
            }

            distance -= width;

            if (distance < height)
            {
                return AimDirection.Down;
            }

            distance -= height;

            if (distance < width)
            {
                return AimDirection.Left;
            }

            return AimDirection.Up;
        }

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

        // 유닛을 빈 칸으로 옮김. 배치 위치가 전투 결과에 영향을 주므로 필요하다
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

        // 그리드에 있는 서로 다른(머지 불가능한) 두 유닛의 자리를 맞바꿈
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

        // 짝 없이 성급만 한 단계 올림. Joker 전용이며 최대 성급이면 거부
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

        // 슬롯을 비우고 있던 유닛을 돌려줌. 판매용이며 빈 칸이면 null
        public UnitInstance TakeAt(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"슬롯 범위를 벗어남: {index}");
            }

            UnitInstance unit = slots[index];
            slots[index] = null;
            return unit;
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
