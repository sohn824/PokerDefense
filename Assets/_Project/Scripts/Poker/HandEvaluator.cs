using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerDefense.Poker
{
    public static class HandEvaluator
    {
        public const int HandSize = 5;

        /// <summary>A-2-3-4-5를 오름차순 랭크 값으로 나열한 것.</summary>
        static readonly int[] BackStraightRanks = { 2, 3, 4, 5, (int)Rank.Ace };

        enum StraightKind
        {
            None,
            Normal,
            Back,     // A-2-3-4-5
            Mountain, // A-K-Q-J-10
        }

        public static HandResult Evaluate(IReadOnlyList<Card> hand)
        {
            if (hand == null || hand.Count != HandSize)
            {
                throw new ArgumentException($"핸드는 정확히 {HandSize}장이어야 한다.", nameof(hand));
            }

            bool isFlush = hand.All(c => c.Suit == hand[0].Suit);
            StraightKind straight = GetStraightKind(hand);

            if (straight != StraightKind.None)
            {
                return new HandResult(StraightCategory(straight, isFlush), SortStraight(hand, straight));
            }

            if (isFlush)
            {
                return new HandResult(HandCategory.Flush, SortByRankDescending(hand));
            }

            // 같은 랭크끼리 묶고, 큰 묶음 -> 높은 랭크 순으로 정렬한다.
            List<Card[]> groups = hand
                .GroupBy(c => c.Rank)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => (int)g.Key)
                .Select(g => g.ToArray())
                .ToList();

            int largest = groups[0].Length;
            int second = groups.Count > 1 ? groups[1].Length : 0;

            if (largest == 4)
            {
                return new HandResult(HandCategory.FourOfAKind, groups[0]);
            }

            if (largest == 3 && second == 2)
            {
                return new HandResult(HandCategory.FullHouse, groups[0].Concat(groups[1]).ToArray());
            }

            if (largest == 3)
            {
                return new HandResult(HandCategory.ThreeOfAKind, groups[0]);
            }

            if (largest == 2 && second == 2)
            {
                return new HandResult(HandCategory.TwoPair, groups[0].Concat(groups[1]).ToArray());
            }

            if (largest == 2)
            {
                return new HandResult(HandCategory.OnePair, groups[0]);
            }

            return new HandResult(HandCategory.HighCard, new[] { SortByRankDescending(hand)[0] });
        }

        static HandCategory StraightCategory(StraightKind kind, bool isFlush) => kind switch
        {
            StraightKind.Mountain => isFlush ? HandCategory.RoyalStraightFlush : HandCategory.Mountain,
            StraightKind.Back => isFlush ? HandCategory.BackStraightFlush : HandCategory.BackStraight,
            _ => isFlush ? HandCategory.StraightFlush : HandCategory.Straight,
        };

        static StraightKind GetStraightKind(IReadOnlyList<Card> hand)
        {
            int[] ranks = hand.Select(c => (int)c.Rank).Distinct().OrderBy(v => v).ToArray();
            if (ranks.Length != HandSize)
            {
                return StraightKind.None;
            }

            if (ranks.SequenceEqual(BackStraightRanks))
            {
                return StraightKind.Back;
            }

            for (int i = 1; i < ranks.Length; i++)
            {
                // 랩어라운드는 허용하지 않는다. K-A-2-3-4는 스트레이트가 아니다.
                if (ranks[i] != ranks[i - 1] + 1)
                {
                    return StraightKind.None;
                }
            }

            return ranks[^1] == (int)Rank.Ace ? StraightKind.Mountain : StraightKind.Normal;
        }

        static Card[] SortByRankDescending(IReadOnlyList<Card> hand)
            => hand.OrderByDescending(c => (int)c.Rank).ToArray();

        /// <summary>백스트레이트는 A를 1로 보고 5-4-3-2-A 순으로 늘어놓는다.</summary>
        static Card[] SortStraight(IReadOnlyList<Card> hand, StraightKind kind)
        {
            if (kind != StraightKind.Back)
            {
                return SortByRankDescending(hand);
            }

            return hand.OrderByDescending(c => c.Rank == Rank.Ace ? 1 : (int)c.Rank).ToArray();
        }
    }
}
