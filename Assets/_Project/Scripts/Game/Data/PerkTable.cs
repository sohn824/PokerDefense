using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * PerkCategory
     *
     * 특전의 계열 정의 (포커/경제/유닛/머지)
     * 특전의 선택지 3개는 포커/경제에서 1, 유닛/머지에서 1, 완전 랜덤 1로 뽑음
     * (비슷한 종류 3개가 나오는 상황을 줄이기 위한 안전장치)
     */
    public enum PerkCategory
    {
        Poker,
        Economy,
        Unit,
        Merge,
    }

    /**
     * PerkId
     *
     * 특전 하나를 가리키는 고유 id
     * 값을 더할 때는 반드시 뒤에 붙인다 (에셋이 enum을 정수로 들고 있기 때문에 중간에 끼우면 어긋남)
     */
    public enum PerkId
    {
        Patience,
        Insurance,
        Bargain,
        Interest,
        RookieTraining,
        Veteran,
    }

    /**
     * PerkTable
     *
     * 특전 전체 목록
     *
     * amount / limit는 특전마다 뜻이 다름
     * 조건은 PerkSet이 갖고 여기에는 수치만 둠
     *
     * | 특전           | amount                              | limit                      |
     * |--------------  |------------------------------------|---                          |
     * | Patience       | 교체 0장일 때 유지 보너스에 더할 Chip | -                           |
     * | Insurance      | 하이카드로 끝났을 때 받을 Chip       | 발동에 필요한 최소 교체 장수   |
     * | Bargain        | 지원 소환 할인 Chip                 | -                           |
     * | Interest       | Chip 몇 개당 +1인지                 | 한 웨이브에 받을 수 있는 상한  |
     * | RookieTraining | ★1 공격력 증가율 (0.2 = +20%)       | -                          |
     * | Veteran        | ★2 이상 공격력 증가율               | -                           |
     */
    [CreateAssetMenu(menuName = "PokerDefense/Perk Table", fileName = "PerkTable")]
    public sealed class PerkTable : ScriptableObject
    {
        [Serializable]
        public struct PerkEntry
        {
            public PerkId id;
            public string displayName;

            [TextArea(2, 3)]
            public string description;

            public PerkCategory category;

            public float amount;

            public float limit;
        }

        [SerializeField] PerkEntry[] entries;

        public IReadOnlyList<PerkEntry> Entries => entries;

        public PerkEntry GetEntry(PerkId id)
        {
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                if (entries[i].id == id)
                {
                    return entries[i];
                }
            }

            throw new InvalidOperationException($"{name}에 {id} 행이 없습니다.");
        }
    }
}
