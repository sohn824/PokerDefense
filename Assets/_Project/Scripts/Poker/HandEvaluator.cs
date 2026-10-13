using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerDefense.Poker
{
    /**
     * HandEvaluator
     *
     * 5장 손패의 족보를 판정
     * 용도에 따라 무거운 판정(Evaluate)과 가벼운 판정(CategoryOf)을 나눠 쓰며, 두 메소드가 내는 족보는 항상 같아야 함
     *
     * Evaluate - 족보와 KeyCards까지 구함 (손패 확정·미리보기처럼 가끔 호출)
     *   KeyCards는 손패 확정 후 족보를 만든 카드를 화면에 보여 주는 데 사용 (RoundScreen)
     *   LINQ로 랭크별 묶음과 정렬된 배열을 만들므로 호출할 때마다 메모리 할당이 생김
     *
     * CategoryOf - 족보 종류만 구함 (교체 확률 계산 한 번에 최대 약 18만 번 호출)
     *   배열·LINQ 없이 랭크 비트마스크와 정수 변수 몇 개로만 판정하므로 메모리 할당이 없음
     *   랭크 비트마스크 = int의 n번 비트로 "랭크 n이 손패에 있음"을 표시한 값
     *   (Rank enum 값 2~14를 빼기 없이 그대로 비트 번호로 쓰므로 2~14번 비트만 사용)
     *   예) A·K·5·3·3 -> 14·13·5·3번 비트가 켜짐 (3이 두 장이어도 비트는 하나)
     */
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

            // 플러시, 스트레이트류 먼저 처리
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

        // 확률 계산에는 KeyCards가 필요 없으므로
        // Evaluate처럼 랭크별 묶음과 정렬된 카드 배열을 만들지 않고
        // 비트마스크를 활용해 가볍게 족보 종류만 판정
        public static HandCategory CategoryOf(IReadOnlyList<Card> hand)
        {
            if (hand == null || hand.Count != HandSize)
            {
                throw new ArgumentException("핸드는 정확히 5장이어야 함", nameof(hand));
            }

            int rankMask = 0;      // 손패에 있는 랭크 집합 (비트마스크) - 중복·백스트레이트 판정용
            int largestGroup = 0;  // 가장 많이 겹친 랭크의 장 수 - 포카드면 4, 트리플·풀하우스면 3
            int pairCards = 0;     // 정확히 2장 겹친 랭크에 속한 카드 수 - 원페어·풀하우스면 2, 투페어면 4

            // 스트레이트 판정용 최소·최대 랭크 (반대쪽 끝값에서 시작해 좁혀 감)
            int minRank = (int)Rank.Ace;
            int maxRank = (int)Rank.Two;

            bool isUnique = true;
            bool isFlush = true;
            // 카드를 한 장씩 보며 랭크 집합·최소/최대 랭크·플러시 여부를 갱신하고,
            // 그 카드와 같은 랭크가 몇 장인지 세어 largestGroup·pairCards에 반영
            for (int i = 0; i < HandSize; i++)
            {
                int rank = (int)hand[i].Rank;
                // 1 << rank = rank번 비트 하나만 켜진 값
                // & 로 그 비트가 이미 켜져 있는지 확인 (켜져 있으면 같은 랭크가 또 나온 것)
                if ((rankMask & (1 << rank)) != 0)
                {
                    isUnique = false;
                }

                // |= 로 rank번 비트를 켬 (이미 켜져 있으면 그대로)
                rankMask |= 1 << rank;
                minRank = Math.Min(minRank, rank);
                maxRank = Math.Max(maxRank, rank);
                isFlush &= hand[i].Suit == hand[0].Suit;
                // 이 카드와 같은 랭크가 몇 장인지 셈 (자기 자신 포함)
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
                // 백스트레이트(A-2-3-4-5)는 A가 14라 최댓값-최솟값이 12가 되어
                // 아래의 스트레이트용 연속 판정식에 걸리지 않으므로 비트마스크로 따로 확인
                const int BackMask = (1 << 14) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5);
                if (rankMask == BackMask)
                {
                    return StraightCategory(StraightKind.Back, isFlush);
                }

                // 랭크 5개가 전부 다르고 최댓값-최솟값이 4면, 그 사이가 빈틈없이 연속임 (최댓값이 A면 마운틴)
                if (maxRank - minRank == 4)
                {
                    return StraightCategory(maxRank == (int)Rank.Ace ? StraightKind.Mountain : StraightKind.Normal, isFlush);
                }
            }

            // 플러시면 5장이 같은 무늬라 랭크가 모두 다르므로 포카드·풀하우스와 겹칠 수 없어 먼저 확인해도 됨
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
            // 중복 랭크를 뺀 오름차순 배열 (5개가 안 되면 같은 랭크가 있으므로 스트레이트 아님)
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

        // 랭크 내림차순 정렬
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
