using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    public enum HandOddsSource
    {
        Exchange,
        Assist,
    }

    /**
     * HandOddsRequest
     *
     * 메인 스레드에서 복사한 교체 확률 계산 입력
     * Card는 불변 값 형식이며 복사한 배열은 읽기 전용으로만 공개
     */
    public sealed class HandOddsRequest
    {
        public HandOddsRequest(int version, HandOddsSource source, IReadOnlyList<Card> hand,
            IReadOnlyList<Card> unseen, IReadOnlyList<int> indices)
        {
            Version = version;
            Source = source;
            Hand = Copy(hand);
            Unseen = Copy(unseen);
            Indices = Copy(indices);
        }

        public int Version { get; }
        public HandOddsSource Source { get; }
        public ReadOnlyCollection<Card> Hand { get; }
        public ReadOnlyCollection<Card> Unseen { get; }
        public ReadOnlyCollection<int> Indices { get; }

        // 손패 버전과 표시 대상, 실제 계산 입력이 모두 같은 요청인지 확인
        public bool Matches(HandOddsRequest other)
        {
            return other != null && Version == other.Version && Source == other.Source
                && Equal(Hand, other.Hand) && Equal(Unseen, other.Unseen) && Equal(Indices, other.Indices);
        }

        static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            T[] result = new T[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = source[i];
            }

            return Array.AsReadOnly(result);
        }

        static bool Equal<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(left[i], right[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
