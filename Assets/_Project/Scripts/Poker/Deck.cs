using System;
using System.Collections.Generic;

namespace PokerDefense.Poker
{
    /**
     * Deck
     *
     * 조커를 제외한 Card 52장 덱
     * 라운드마다 seed를 받아 새로 생성
     * excluded로 넘긴 카드는 덱에서 빠진다 (플레이어가 상점 카드를 들고 있는 동안 — DESIGN §13.3)
     */
    public sealed class Deck
    {
        public const int FullSize = 52;

        readonly List<Card> cards = new List<Card>(FullSize);
        int nextIndex;

        public Deck(int seed, IReadOnlyList<Card> excluded = null)
        {
            cards.AddRange(BuildCards(excluded));
            Shuffle(new Random(seed));
        }

        // 52장 전체에서 excluded를 뺀 목록 (셔플 안 함)
        // 상점 진열도 이 목록에서 뽑는다 (ShopOffer — DESIGN §13.2)
        public static List<Card> BuildCards(IReadOnlyList<Card> excluded = null)
        {
            var list = new List<Card>(FullSize);

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (Rank rank = Rank.Two; rank <= Rank.Ace; rank++)
                {
                    var card = new Card(rank, suit);

                    if (excluded == null || Contains(excluded, card) == false)
                    {
                        list.Add(card);
                    }
                }
            }

            return list;
        }

        static bool Contains(IReadOnlyList<Card> list, Card card)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == card)
                {
                    return true;
                }
            }

            return false;
        }

        public int Remaining => cards.Count - nextIndex;

        public Card Draw()
        {
            if (Remaining == 0)
            {
                throw new InvalidOperationException("Draw 불가: 덱이 비었음");
            }

            return cards[nextIndex++];
        }

        // Fisher-Yates Shuffle 알고리즘으로 덱 셔플
        void Shuffle(Random random)
        {
            // 뒤에서부터 하나씩 카드 최종 위치 결정
            for (int i = cards.Count - 1; i > 0; i--)
            {
                // 아직 섞이지 않은 범위(0 ~ i)에서 임의의 위치 j 선택
                int j = random.Next(i + 1);
                // 선택한 카드(j)와 현재 카드(i)를 교환
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
