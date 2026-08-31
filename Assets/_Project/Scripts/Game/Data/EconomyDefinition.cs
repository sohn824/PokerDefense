using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EconomyDefinition
     *
     * 게임 내 Chip 경제 수치 데이터
     */
    [CreateAssetMenu(menuName = "PokerDefense/Economy Definition", fileName = "Economy")]
    public sealed class EconomyDefinition : ScriptableObject
    {
        [Serializable]
        public struct SupportEntry
        {
            public HandCategory category;

            [Tooltip("가중치")]
            public int weight;
        }

        [Header("교체를 사용하지 않았을 때 받을 수 있는 보너스 칩 최대치")]
        [Tooltip("(1장 쓸 때마다 1씩 줄어듦)")]
        [SerializeField] int holdBonusMax = 4;

        [Header("지원 소환 코스트")]
        [SerializeField] int supportSummonCost = 6;

        [Tooltip("라운드당 지원 소환 제한 횟수")]
        [SerializeField] int supportSummonsPerRound = 2;

        [Tooltip("지원 소환에서 나올 손패 종류 풀")]
        [SerializeField] SupportEntry[] supportPool;

        [Header("유닛 판매 가격")]
        [SerializeField] int[] sellPrices = { 1, 2, 4 };

        [Header("카드 상점")]
        [Tooltip("상점 카드 1장 가격 (우선 균일)")]
        [SerializeField] int shopCardPrice = 4;

        [Tooltip("보유 카드 상한")]
        [SerializeField] int heldCardCapacity = 3;

        public int SupportSummonCost => supportSummonCost;
        public int SupportSummonsPerRound => supportSummonsPerRound;
        public IReadOnlyList<SupportEntry> SupportPool => supportPool;
        public int ShopCardPrice => shopCardPrice;
        public int HeldCardCapacity => heldCardCapacity;

        // 남은 교체 횟수에 대한 유지 보너스
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
