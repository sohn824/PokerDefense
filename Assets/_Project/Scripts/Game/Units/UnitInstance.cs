using System;

namespace PokerDefense.Game
{
    /**
     * UnitInstance
     *
     * 보드 위 유닛 1기의 종류(Definition)와 성급(Star)을 담는 불변 객체
     * 승급해도 이 객체는 그대로 두고, Promoted로 만든 새 객체(성급 + 1)를 GridBoard가 슬롯에 넣음
     * 예) 3번 칸의 A(★1)에 같은 유닛 B(★1)를 놓으면 A.Promoted()로 C(★2)를 만들어 3번 칸에 넣음
     * A·B는 ★1 그대로 보드에서 빠짐
     *
     * 불변으로 둔 이유 - 여러 곳이 이 객체를 참조로 들고 있음
     * 값을 직접 고치면 들고 있는 모든 곳의 값이 한꺼번에 바뀜
     * 그래서 값은 고칠 수 없게 하고, 바뀌어야 하면 새 객체를 만듦
     * -> 기존 참조는 예전 값을 그대로 보고, 새 객체와는 참조가 달라 ==로 바뀐 것을 바로 알 수 있음
     */
    public sealed class UnitInstance
    {
        // 새로 소환하는 유닛 (항상 ★1, definition이 null이면 예외)
        public UnitInstance(UnitDefinition definition) : this(definition, 1)
        {
        }

        // 성급을 지정하는 생성자 (Promoted만 씀)
        UnitInstance(UnitDefinition definition, int star)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Definition = definition;
            Star = star;
        }

        // 유닛 종류 (에셋)
        public UnitDefinition Definition { get; }

        // 성급 1~3 (★1로 소환되고 머지·승급권으로 1씩 오름)
        public int Star { get; }

        // ★3이면 true (더 머지하거나 승급권을 쓸 수 없음)
        public bool IsMaxStar => Star >= UnitDefinition.MaxStar;

        // 공격력과 초당 공격 횟수에 둘 다 성급 배수를 곱하므로 DPS는 배수의 제곱만큼 오름
        // 예) ★3 배수가 2.25면 DPS는 2.25 × 2.25 ≈ 5.06배
        public float AttackPower => Definition.AttackPower * Definition.MultiplierFor(Star);
        public float AttacksPerSecond => Definition.AttacksPerSecond * Definition.MultiplierFor(Star);

        // 사거리는 성급과 무관
        public float Range => Definition.Range;

        // other와 종류·성급이 같고 ★3이 아니면 true (other가 null이면 false)
        public bool CanMergeWith(UnitInstance other)
            => other != null
               && other.Definition == Definition
               && other.Star == Star
               && IsMaxStar == false;

        // 성급이 1 높은 새 객체를 돌려줌 (이 객체는 그대로, ★3인지는 호출하는 쪽이 확인함)
        public UnitInstance Promoted() => new UnitInstance(Definition, Star + 1);

        // 로그용 표기 (예: 원페어 건슬링어 ★2, BalanceSimRunner가 씀)
        public override string ToString() => $"{Definition.DisplayName} ★{Star}";
    }
}
