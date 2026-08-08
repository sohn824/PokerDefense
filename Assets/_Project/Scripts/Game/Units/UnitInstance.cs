using System;

namespace PokerDefense.Game
{
    /// <summary>
    /// 보드에 올라간 유닛 1기. 종류 + 성급이 전부다.
    ///
    /// 불변이다. 머지는 성급을 올리는 대신 승급된 새 인스턴스를 만든다 —
    /// 보드가 슬롯을 교체하는 방식이라 중간 상태가 생기지 않는다.
    /// MonoBehaviour가 아니라서 EditMode 테스트에서 그대로 쓴다.
    /// </summary>
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

        /// <summary>사거리는 성급 배수를 받지 않는다. 성급으로 사거리까지 늘면 배치 위치 선택이 무의미해진다.</summary>
        public float Range => Definition.Range;

        /// <summary>동일 종류 + 동일 성급 2기만 합쳐진다. ★3은 더 못 올린다 (DESIGN §5.2).</summary>
        public bool CanMergeWith(UnitInstance other)
            => other != null
               && other.Definition == Definition
               && other.Star == Star
               && !IsMaxStar;

        public UnitInstance Promoted() => new UnitInstance(Definition, Star + 1);

        public override string ToString() => $"{Definition.DisplayName} ★{Star}";
    }
}
