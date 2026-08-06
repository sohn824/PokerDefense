using System;
using System.Collections.Generic;

namespace PokerDefense.Poker
{
    /// <summary>
    /// 조커를 제외한 52장 덱. 라운드마다 새로 생성한다.
    /// 시드를 주입받아 셔플 결과를 재현할 수 있다.
    /// </summary>
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
                throw new InvalidOperationException("덱이 비어 더 뽑을 수 없다.");
            }

            return cards[nextIndex++];
        }

        void Shuffle(Random random)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
