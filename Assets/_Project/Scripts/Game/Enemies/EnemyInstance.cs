using System;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EnemyInstance
     *
     * 트랙 위를 도는 적 1기
     * 위치는 트랙 진행도 하나로만 들고 있고, 월드 좌표는 그릴 때 변환
     * 진행도는 바퀴 수를 포함해 계속 누적 ("가장 앞선 적"을 판정하기 위함)
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

        public float Progress { get; private set; }

        public bool IsAlive => Hp > 0f;

        // 보드 중심 기준 로컬 좌표
        public Vector2 Position => GridBoard.TrackPosition(Progress);

        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "시간은 뒤로 흐르지 않습니다");
            }

            Progress += Definition.MoveSpeed * deltaTime / GridBoard.TrackLength;
        }

        public void TakeDamage(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), $"피해량이 음수입니다: {amount}");
            }

            Hp = Mathf.Max(0f, Hp - amount);
        }

        public override string ToString() => $"{Definition.DisplayName} HP {Hp:0.#}/{Definition.MaxHp:0.#}";
    }
}
