using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerDefense.Poker
{
    public static class HandEvaluator
    {
        public const int HandSize = 5;

        // 백스트레이트 예외처리 (A는 기본적으로 14이지만 백스트레이트에서만 1로 취급)
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
                throw new ArgumentException($"HandSize 오류: 핸드는 정확히 {HandSize}장이어야 함", nameof(hand));
            }

            // 플러쉬, 스트레이트류 먼저 처리
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

            // 같은 랭크끼리 묶고, 큰 묶음 -> 높은 랭크 순으로 정렬
            List<Card[]> groups = hand
                .GroupBy(c => c.Rank)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => (int)g.Key)
                .Select(g => g.ToArray())
                .ToList();

            int largest = groups[0].Length;
            int second = groups.Count > 1 ? groups[1].Length : 0;

            // 포카드
            if (largest == 4)
            {
                return new HandResult(HandCategory.FourOfAKind, groups[0]);
            }

            // 풀 하우스
            if (largest == 3 && second == 2)
            {
                return new HandResult(HandCategory.FullHouse, groups[0].Concat(groups[1]).ToArray());
            }

            // 트리플
            if (largest == 3)
            {
                return new HandResult(HandCategory.ThreeOfAKind, groups[0]);
            }

            // 투페어
            if (largest == 2 && second == 2)
            {
                return new HandResult(HandCategory.TwoPair, groups[0].Concat(groups[1]).ToArray());
            }
            
            // 원페어
            if (largest == 2)
            {
                return new HandResult(HandCategory.OnePair, groups[0]);
            }

            // 하이 카드
            return new HandResult(HandCategory.HighCard, new[] { SortByRankDescending(hand)[0] });
        }

        // 확률 열거에서는 승리 카드 목록이 필요 없다. 배열/정렬 없이 종류만 판정한다.
        public static HandCategory CategoryOf(IReadOnlyList<Card> hand)
        {
            if (hand == null || hand.Count != HandSize)
            {
                throw new ArgumentException("핸드는 정확히 5장이어야 함", nameof(hand));
            }

            int rankMask = 0;      // 등장한 랭크 비트마스크 - 스트레이트 판정용
            int largestGroup = 0;  // 같은 랭크 카드의 최대 묶음 크기
            int pairCards = 0;     // 자기 랭크가 정확히 2장인 카드 수 - 원페어면 2, 투페어면 4
            int minRank = (int)Rank.Ace;
            int maxRank = (int)Rank.Two;
            bool isUnique = true;
            bool isFlush = true;
            // 한 번의 순회로 랭크 집합 · 최대 묶음 크기 · 플러시 여부를 모두 구한다
            for (int i = 0; i < HandSize; i++)
            {
                int rank = (int)hand[i].Rank;
                if ((rankMask & (1 << rank)) != 0)
                {
                    isUnique = false;
                }

                rankMask |= 1 << rank;
                minRank = Math.Min(minRank, rank);
                maxRank = Math.Max(maxRank, rank);
                isFlush &= hand[i].Suit == hand[0].Suit;
                int count = 0;
                for (int j = 0; j < HandSize; j++)
                {
                    if (hand[j].Rank == hand[i].Rank)
                    {
                        count++;
                    }
                }

                largestGroup = Math.Max(largestGroup, count);
                if (count == 2)
                {
                    pairCards++;
                }
            }

            if (isUnique)
            {
                // 백스트레이트(A-2-3-4-5)는 Ace를 1로 봐야 해서 아래의 연속 판정식으로 못 잡는다
                const int BackMask = (1 << 14) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5);
                if (rankMask == BackMask)
                {
                    return StraightCategory(StraightKind.Back, isFlush);
                }

                // 랭크 5개가 전부 다르고 최댓값-최솟값이 4면, 그 사이가 빈틈없이 연속임
                if (maxRank - minRank == 4)
                {
                    return StraightCategory(maxRank == (int)Rank.Ace ? StraightKind.Mountain : StraightKind.Normal, isFlush);
                }
            }

            if (isFlush)
            {
                return HandCategory.Flush;
            }

            if (largestGroup == 4)
            {
                return HandCategory.FourOfAKind;
            }

            if (largestGroup == 3)
            {
                // 세 장을 뺀 나머지 두 장이 페어면 풀 하우스
                return pairCards == 2 ? HandCategory.FullHouse : HandCategory.ThreeOfAKind;
            }

            if (pairCards == 4)
            {
                return HandCategory.TwoPair;
            }

            return pairCards == 2 ? HandCategory.OnePair : HandCategory.HighCard;
        }

        static HandCategory StraightCategory(StraightKind kind, bool isFlush) => kind switch
        {
            StraightKind.Mountain => isFlush ? HandCategory.RoyalStraightFlush : HandCategory.Mountain,
            StraightKind.Back => isFlush ? HandCategory.BackStraightFlush : HandCategory.BackStraight,
            _ => isFlush ? HandCategory.StraightFlush : HandCategory.Straight,
        };

        static StraightKind GetStraightKind(IReadOnlyList<Card> hand)
        {
            // 손패 리스트를 오름차순 정렬 후 배열로 변환
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
                if (ranks[i] != ranks[i - 1] + 1)
                {
                    return StraightKind.None;
                }
            }

            // 오름차순 정렬이므로 가장 마지막이 Ace면 마운틴
            return ranks[^1] == (int)Rank.Ace ? StraightKind.Mountain : StraightKind.Normal;
        }

        // 내림차순 정렬 
        static Card[] SortByRankDescending(IReadOnlyList<Card> hand)
            => hand.OrderByDescending(c => (int)c.Rank).ToArray();

        // 스트레이트 패를 내림차순으로 정렬 (백스트레이트는 예외로 A를 1로 보고 5-4-3-2-A)
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
