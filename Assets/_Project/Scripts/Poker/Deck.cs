using System;
using System.Collections.Generic;

namespace PokerDefense.Poker
{
    /**
     * Deck
     * 
     * 조커를 제외한 Card 52장 덱
     * 라운드마다 seed를 받아 새로 생성
     */
    public sealed class Deck
    {
        public const int FullSize = 52;

        readonly List<Card> cards = new List<Card>(FullSize);
        int nextIndex;

        public Deck(int seed)
        {
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (Rank rank = Rank.Two; rank <= Rank.Ace; rank++)
                {
                    cards.Add(new Card(rank, suit));
                }
            }

            Shuffle(new Random(seed));
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
