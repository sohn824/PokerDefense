using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * TrackPath
     *
     * 직선과 원호로 된 투기장 길. 위치·접선·총 길이가 같은 거리 좌표를 사용한다.
     * 시작점은 위쪽 직선의 왼쪽 끝이며 시계 방향으로 돈다.
     */
    public sealed class TrackPath
    {
        public static readonly TrackPath Arena = new TrackPath(3.2f, 1.95f, 1.2f);

        public float HalfWidth { get; }
        public float HalfHeight { get; }
        public float Radius { get; }
        public float Length { get; }

        public TrackPath(float halfWidth, float halfHeight, float radius)
        {
            if (radius <= 0f || halfWidth < radius || halfHeight < radius)
                throw new ArgumentOutOfRangeException(nameof(radius));
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            Radius = radius;
            Length = 4f * (halfWidth + halfHeight - 2f * radius) + 2f * Mathf.PI * radius;
        }

        public Vector2 Position(float progress)
        {
            Sample(progress, out Vector2 position, out _);
            return position;
        }

        public Vector2 Tangent(float progress)
        {
            Sample(progress, out _, out Vector2 tangent);
            return tangent;
        }

        void Sample(float progress, out Vector2 position, out Vector2 tangent)
        {
            float distance = (progress - Mathf.Floor(progress)) * Length;
            float horizontal = 2f * (HalfWidth - Radius);
            float vertical = 2f * (HalfHeight - Radius);
            float arc = Mathf.PI * Radius * .5f;
            Vector2 start = new Vector2(-HalfWidth + Radius, HalfHeight);
            Vector2 direction = Vector2.right;

            for (int side = 0; side < 4; side++)
            {
                float straight = side % 2 == 0 ? horizontal : vertical;
                if (distance < straight)
                {
                    position = start + direction * distance;
                    tangent = direction;
                    return;
                }
                distance -= straight;
                Vector2 normal = new Vector2(direction.y, -direction.x);
                Vector2 center = start + direction * straight + normal * Radius;
                if (distance < arc || side == 3)
                {
                    float angle = distance / Radius;
                    position = center + (-normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle)) * Radius;
                    tangent = normal * Mathf.Sin(angle) + direction * Mathf.Cos(angle);
                    return;
                }
                distance -= arc;
                start = center + direction * Radius;
                direction = normal;
            }
            position = start;
            tangent = direction;
        }
    }
}
