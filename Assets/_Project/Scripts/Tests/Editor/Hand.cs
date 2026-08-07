using System;
using System.Linq;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /// <summary>
    /// 테스트에서 핸드를 "As Ks Qs Js Ts" 표기로 쓰기 위한 헬퍼. 테스트 전용이다.
    /// </summary>
    static class Hand
    {
        public static Card[] Of(string notation)
            => notation.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(Parse)
                       .ToArray();

        static Card Parse(string token)
        {
            Rank rank = token[0] switch
            {
                'T' => Rank.Ten,
                'J' => Rank.Jack,
                'Q' => Rank.Queen,
                'K' => Rank.King,
                'A' => Rank.Ace,
                _ => (Rank)(token[0] - '0'),
            };

            Suit suit = token[1] switch
            {
                's' => Suit.Spade,
                'h' => Suit.Heart,
                'd' => Suit.Diamond,
                'c' => Suit.Club,
                _ => throw new ArgumentException($"알 수 없는 카드 표기: {token}"),
            };

            return new Card(rank, suit);
        }
    }
}
