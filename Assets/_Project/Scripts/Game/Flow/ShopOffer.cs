using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * ShopOffer
     *
     * 5웨이브마다 열리는 보너스 카드 상점의 진열 카드를 뽑는 클래스
     * 이미 든 카드(held)는 덱에도 없으므로 진열에도 안 나옴
     * 진열은 4장이고 플레이어는 그 안에서 원하는 만큼 살 수 있음
     */
    public sealed class ShopOffer
    {
        public const int OfferCount = 4;

        readonly Random random;

        public ShopOffer(int seed)
        {
            random = new Random(seed);
        }

        // held를 뺀 52장에서 서로 다른 OfferCount장을 뽑는다 (풀이 모자라면 그만큼만)
        public IReadOnlyList<Card> Roll(IReadOnlyList<Card> held)
        {
            List<Card> pool = Deck.BuildCards(held);
            var offered = new List<Card>(OfferCount);

            int take = Math.Min(OfferCount, pool.Count);

            for (int i = 0; i < take; i++)
            {
                int pick = random.Next(pool.Count);
                offered.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            return offered;
        }
    }
}
