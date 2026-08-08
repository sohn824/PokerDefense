using System;

namespace PokerDefense.Game
{
    /**
     * UnitInstance
     * 
     * 그리드에 올라가는 유닛 1기
     * 한번 생성되면 불변 (머지 시 기존 유닛들은 파괴하고 승급된 새 instance를 만들음)
     */
    public sealed class UnitInstance
    {
        public UnitInstance(UnitDefinition definition) : this(definition, 1)
        {
        }

        UnitInstance(UnitDefinition definition, int star)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Definition = definition;
            Star = star;
        }

        public UnitDefinition Definition { get; }

        public int Star { get; }

        public bool IsMaxStar => Star >= UnitDefinition.MaxStar;

        public float AttackPower => Definition.AttackPower * Definition.MultiplierFor(Star);

        public float AttacksPerSecond => Definition.AttacksPerSecond * Definition.MultiplierFor(Star);

        public float Range => Definition.Range;

        /// 동일 종류 + 동일 성급 2기만 합칠 수 있음
        public bool CanMergeWith(UnitInstance other)
            => other != null
               && other.Definition == Definition
               && other.Star == Star
               && IsMaxStar == false;

        public UnitInstance Promoted() => new UnitInstance(Definition, Star + 1);

        public override string ToString() => $"{Definition.DisplayName} ★{Star}";
    }
}
