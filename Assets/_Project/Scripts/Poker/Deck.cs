using System;
using System.Collections.Generic;

namespace PokerDefense.Poker
{
    /**
     * Deck
     *
     * 조커를 제외한 Card 52장 덱
     * RoundContext가 라운드마다 seed를 받아 새로 생성
     */
    public sealed class Deck
    {
        public const int FullSize = 52;

        readonly List<Card> cards = new List<Card>(FullSize);
        int nextIndex;

        public Deck(int seed)
        {
            cards.AddRange(BuildCards());
            Shuffle(new Random(seed));
        }

        // 52장 카드 목록을 새로 만들어 돌려줌 (excluded로 넘긴 카드는 빼고, 셔플은 안 함)
        // 기본적으로 덱 생성에 쓰고, 밖에서는 교체 확률 계산에 넘길 남은 카드 목록을 만들 때 사용
        // (RoundContext·HandOddsBenchmark)
        public static List<Card> BuildCards(IReadOnlyList<Card> excluded = null)
        {
            List<Card> list = new List<Card>(FullSize);

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (Rank rank = Rank.Two; rank <= Rank.Ace; rank++)
                {
                    Card card = new Card(rank, suit);

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

        // 아직 뽑히지 않은 카드인지
        public bool ContainsRemaining(Card card) => cards.IndexOf(card, nextIndex) >= nextIndex;

        public Card Draw()
        {
            if (Remaining == 0)
            {
                throw new InvalidOperationException("Draw 불가: 덱이 비었음");
            }

            return cards[nextIndex++];
        }

        // count장까지 뽑음 (덱이 모자라면 남은 만큼만)
        public Card[] DrawUpTo(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            Card[] drawn = new Card[Math.Min(count, Remaining)];
            for (int i = 0; i < drawn.Length; i++)
            {
                drawn[i] = Draw();
            }
            return drawn;
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
