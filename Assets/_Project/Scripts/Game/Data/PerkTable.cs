using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * PerkCategory
     *
     * 선택지 3칸을 뽑을 때 쓰는 계열 (DESIGN §11)
     * 포커/경제에서 1, 유닛/머지에서 1, 완전 랜덤 1로 뽑아 비슷한 것 셋만 나오는 상황을 줄인다
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
     * 특전 하나를 가리키는 식별자. 규칙은 PerkSet이 이 값으로 갈린다
     *
     * 값을 더할 때는 반드시 뒤에 붙인다 - 에셋이 enum을 정수로 들고 있어
     * 중간에 끼우면 PerkTable의 모든 행이 조용히 어긋난다 (EnemyType과 같은 이유)
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
     * 딜러 특전 전체 목록 (DESIGN §11). 전역 에셋 1개다 - HandUnitTable과 같은 이유로
     * 특전마다 SO를 두지 않는다. 뽑기 풀도 이 목록을 그대로 쓴다
     *
     * amount / limit는 특전마다 뜻이 다르다. 조건은 PerkSet이 갖고 여기에는 수치만 둔다
     *
     * | 특전 | amount | limit |
     * |---|---|---|
     * | Patience       | 교체 0장일 때 유지 보너스에 더할 Chip | - |
     * | Insurance      | 하이카드로 끝났을 때 받을 Chip | 발동에 필요한 최소 교체 장수 |
     * | Bargain        | 지원 소환 할인 Chip | - |
     * | Interest       | Chip 몇 개당 +1인지 | 한 웨이브에 받을 수 있는 상한 |
     * | RookieTraining | ★1 공격력 증가율 (0.2 = +20%) | - |
     * | Veteran        | ★2 이상 공격력 증가율 | - |
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

            [Tooltip("특전마다 뜻이 다르다. PerkTable 주석의 표를 볼 것")]
            public float amount;

            public float limit;
        }

        [SerializeField] PerkEntry[] entries;

        public IReadOnlyList<PerkEntry> Entries => entries;

        public PerkEntry For(PerkId id)
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
