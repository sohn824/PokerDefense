using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /// <summary>
    /// 족보 → 유닛 매핑. 에셋 1개만 두고 전역으로 쓴다 (DESIGN §4).
    ///
    /// 이 테이블이 별도로 존재하는 이유는 **판정 결과와 보상 세기가 별개 축**이기 때문이다.
    /// 백스트레이트는 정통 포커에서 가장 약한 스트레이트지만 이 게임에서는 특수 유닛을 준다.
    /// 그 연결을 코드가 아니라 데이터로 두어야 밸런싱을 코드 수정 없이 바꿀 수 있다 (DESIGN §3.3).
    /// </summary>
    [CreateAssetMenu(menuName = "PokerDefense/Hand Unit Table", fileName = "HandUnitTable")]
    public sealed class HandUnitTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public HandCategory category;
            public UnitDefinition unit;
        }

        [SerializeField] Entry[] entries;

        public UnitDefinition For(HandCategory category)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].category == category && entries[i].unit != null)
                {
                    return entries[i].unit;
                }
            }

            // 빠진 매핑은 데이터 버그다. 나중에 널 참조로 터지는 것보다 여기서 알아채는 편이 낫다.
            throw new InvalidOperationException($"{name}에 {category} 매핑이 없다.");
        }
    }
}
