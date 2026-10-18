using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EnemyInstance
     *
     * 트랙 위를 도는 적 1기의 HP와 위치(progress)를 담는 객체
     * 위치는 progress 하나로만 들고, 좌표가 필요할 때(사거리 판정·그리기) Position으로 계산함
     * progress는 1을 넘어도 계속 누적됨
     * 바퀴 수까지 포함해야 적끼리 누가 앞섰는지 비교할 수 있기 때문
     */
    public sealed class EnemyInstance
    {
        public EnemyInstance(EnemyDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Definition = definition;
            Hp = definition.MaxHp;
        }

        public EnemyDefinition Definition { get; }

        public float Hp { get; private set; }

        // 트랙 진행도 (0에서 시작, 1 = 한 바퀴, 계속 누적)
        public float Progress { get; private set; }

        public bool IsAlive => Hp > 0f;

        // 보드 중심을 원점으로 하는 좌표 (TrackPath가 progress로 계산)
        public Vector2 Position => GridBoard.TrackPosition(Progress);

        // deltaTime(초) 동안 이동 속도만큼 트랙을 따라 나아감
        // 이동 속도는 칸 단위라 트랙 길이로 나눠 progress 단위(1 = 한 바퀴)로 바꿈
        public void Move(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime은 음수가 될 수 없음");
            }

            Progress += Definition.MoveSpeed * deltaTime / GridBoard.TrackLength;
        }

        // amount만큼 HP를 깎되 HP는 0 아래로는 내려가지 않음
        public void TakeDamage(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), $"피해량이 음수임: {amount}");
            }

            Hp = Mathf.Max(0f, Hp - amount);
        }
    }
}
