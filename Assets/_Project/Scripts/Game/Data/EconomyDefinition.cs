using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EconomyDefinition
     *
     * Chip 경제 수치
     * 스테이지마다 달라질 값이 아니라 게임 전역 규칙이라 StageDefinition과 분리했다
     * 지금 값은 전부 플레이스홀더이며 M10에서 밸런싱한다
     */
    [CreateAssetMenu(menuName = "PokerDefense/Economy Definition", fileName = "Economy")]
    public sealed class EconomyDefinition : ScriptableObject
    {
        [Serializable]
        public struct SupportEntry
        {
            public HandCategory category;

            [Tooltip("가중치. 전체 합에 대한 비율로 뽑는다")]
            public int weight;
        }

        [Header("유지 보너스 — 교체를 덜 쓸수록 Chip을 받는다")]
        [Tooltip("교체 0장일 때 받는 Chip. 1장 쓸 때마다 1씩 줄고 0에서 멈춘다")]
        [SerializeField] int holdBonusMax = 4;

        [Header("지원 소환")]
        [SerializeField] int supportSummonCost = 6;

        [Tooltip("라운드당 지원 소환 횟수. 제한이 없으면 Chip을 한 번에 쏟는 것이 정답이 된다")]
        [SerializeField] int supportSummonsPerRound = 2;

        [Tooltip("지원 소환 풀. 스트레이트 이상은 넣지 않는다")]
        [SerializeField] SupportEntry[] supportPool;

        [Header("판매 — 유닛 종류와 무관하게 성급으로만 정한다")]
        [Tooltip("★1 / ★2 / ★3 판매 가격")]
        [SerializeField] int[] sellPrices = { 1, 2, 4 };

        public int SupportSummonCost => supportSummonCost;
        public int SupportSummonsPerRound => supportSummonsPerRound;
        public IReadOnlyList<SupportEntry> SupportPool => supportPool;

        /// <summary>사용한 교체 장수에 대한 유지 보너스.</summary>
        public int HoldBonusFor(int usedExchanges)
        {
            if (usedExchanges < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(usedExchanges), $"교체 장수가 음수입니다: {usedExchanges}");
            }

            return Math.Max(0, holdBonusMax - usedExchanges);
        }

        public int SellPriceFor(int star)
        {
            int index = Mathf.Clamp(star, 1, UnitDefinition.MaxStar) - 1;
            return index < sellPrices.Length ? sellPrices[index] : 0;
        }
    }
}
