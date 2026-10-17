using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * TrackPath
     *
     * 적이 걷는 트랙(모서리가 둥근 직사각형 고리)의 위치·진행 방향을 계산하는 객체
     * 직선 4개와 1/4 원호 4개로 이뤄짐
     *
     * progress - 0 = 시작점, 1 = 한 바퀴 (1을 넘으면 처음부터 다시, 1.25는 0.25와 같은 자리)
     * progress는 걸은 거리의 비율이라, 같은 만큼 늘면 직선이든 모서리든 같은 거리를 감 (모서리에서 적 속도가 변하지 않음)
     * 시작점은 위쪽 직선의 왼쪽 끝이고 시계 방향으로 돎
     */
    public sealed class TrackPath
    {
        // 배경 아트에 맞춘 투기장 트랙 (가로 절반 3.2, 세로 절반 1.95, 모서리 반지름 1.2)
        public static readonly TrackPath Arena = new TrackPath(3.2f, 1.95f, 1.2f);

        // 가로·세로의 절반 (보드 중심부터 직선까지 거리)
        public float HalfWidth { get; }
        public float HalfHeight { get; }

        // 모서리 원호의 반지름
        public float Radius { get; }

        // 한 바퀴 길이 = 직선 4개 + 원호 4개 (원호 4개를 합치면 원 1개 둘레)
        public float Length { get; }

        // radius가 0 이하이거나 가로·세로 절반보다 크면 예외 (직선 길이가 음수가 됨)
        public TrackPath(float halfWidth, float halfHeight, float radius)
        {
            if (radius <= 0f || halfWidth < radius || halfHeight < radius)
                throw new ArgumentOutOfRangeException(nameof(radius));

            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            Radius = radius;
            Length = 4f * (halfWidth + halfHeight - 2f * radius) + 2f * Mathf.PI * radius;
        }

        // progress 지점의 좌표 (보드 중심이 원점)
        public Vector2 Position(float progress)
        {
            GetPositionAndDirection(progress, out Vector2 position, out _);
            return position;
        }

        // progress 지점에서 적이 나아가는 방향 (정규화된 방향 벡터, 예: (1, 0)이면 오른쪽)
        public Vector2 MoveDirection(float progress)
        {
            GetPositionAndDirection(progress, out _, out Vector2 moveDirection);
            return moveDirection;
        }

        // progress 지점의 위치와 진행 방향을 함께 구함 (Position·MoveDirection이 둘 중 필요한 값만 받아 감)
        // 1. progress를 한 바퀴 안의 거리(distance)로 바꿈
        // 2. 위쪽 직선부터 시계 방향으로 직선 -> 모서리 원호 길이를 차례로 빼 나가다가, distance가 남은 구간보다 짧아지면 멈춤
        // 3. 멈춘 구간이 직선이면 직선 위에서, 원호면 원 위에서 위치·방향을 계산
        void GetPositionAndDirection(float progress, out Vector2 position, out Vector2 moveDirection)
        {
            // 한 바퀴 안의 거리 (progress 소수부 × Length)
            float distance = (progress - Mathf.Floor(progress)) * Length;
            // 위·아래 직선 길이, 좌·우 직선 길이, 1/4 원호 길이
            float horizontal = 2f * (HalfWidth - Radius);
            float vertical = 2f * (HalfHeight - Radius);
            float arc = Mathf.PI * Radius * .5f;
            // 현재 직선의 시작점과 진행 방향 (위쪽 직선 왼쪽 끝에서 오른쪽 방향으로 시작, 반복마다 다음 직선으로 바뀜)
            Vector2 start = new Vector2(-HalfWidth + Radius, HalfHeight);
            Vector2 direction = Vector2.right;

            // side - 0 = 위, 1 = 오른쪽, 2 = 아래, 3 = 왼쪽 (한 번에 직선 하나와 그 뒤 모서리 원호 하나를 봄)
            for (int side = 0; side < 4; side++)
            {
                // 이번 side의 직선 길이 (짝수 side = 위·아래, 홀수 side = 좌·우)
                float straight = side % 2 == 0 ? horizontal : vertical;
                // 남은 거리가 이 직선보다 짧으면 적은 이 직선 위에 있음
                // 위치 = 직선 시작점에서 진행 방향으로 distance만큼 간 점, 방향 = 직선 방향 그대로
                if (distance < straight)
                {
                    position = start + direction * distance;
                    moveDirection = direction;
                    return;
                }
                // 직선을 지나쳤으므로 직선 길이만큼 빼고 뒤 원호를 봄
                distance -= straight;
                // normal - 진행 방향을 시계 방향으로 90도 돌린 벡터 ((x, y) -> (y, -x))
                // 진행 방향의 오른쪽을 가리킴
                Vector2 normal = new Vector2(direction.y, -direction.x);
                // center - 원호의 중심 (직선 끝에서 normal 쪽으로 반지름만큼)
                Vector2 center = start + direction * straight + normal * Radius;
                // 남은 거리가 이 원호보다 짧으면 적은 이 원호 위에 있음
                // 마지막 원호(side == 3)는 원호보다 길어도 무조건 여기서 끝냄
                // float 오차로 한 바퀴 끝에서 거리가 아주 조금 남을 수 있기 때문
                if (distance < arc || side == 3)
                {
                    // angle - 원호에 들어와서 돈 각도 (라디안, 0 ~ π/2)
                    float angle = distance / Radius;
                    // 위치 - 중심에서 반지름만큼 떨어진 원 위의 점
                    // 중심에서 직선 끝을 가리키는 방향(-normal)에서 시작해 진행 방향(direction) 쪽으로 angle만큼 돌림
                    // angle이 0이면 직선 끝, π/2(90도)면 다음 직선 시작
                    position = center + (-normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle)) * Radius;
                    // 진행 방향 - 그 점에서 원을 따라 나아가는 방향 (angle이 0이면 direction, π/2면 normal = 다음 직선 방향)
                    moveDirection = normal * Mathf.Sin(angle) + direction * Mathf.Cos(angle);
                    return;
                }

                // 원호도 지나쳤으므로 다음 직선으로 넘어감 (시작점은 원호 끝, 방향은 오른쪽으로 90도 꺾임)
                distance -= arc;
                start = center + direction * Radius;
                direction = normal;
            }

            // 실제로는 실행되지 않음 (반복문의 마지막 원호에서 항상 return함)
            // C#은 out 매개변수를 모든 경로에서 정해야 컴파일되므로 넣어 둠
            position = start;
            moveDirection = direction;
        }
    }
}
