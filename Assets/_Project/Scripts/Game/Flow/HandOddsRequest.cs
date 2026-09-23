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
     * 메인 스레드에서 복사한 교체 확률 계산 입력과 요청 식별 정보
     * 손패·남은 덱·선택 자리를 각각 새 배열로 복사해 읽기 전용으로만 공개
     *
     * 계산이 도는 동안 메인 스레드가 손패나 덱을 바꿔도 이 복사본은 영향을 받지 않음
     * Worker 스레드가 읽는 값이 전부 여기 담겨 있어 양쪽이 같은 데이터를 동시에 건드리지 않음
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

        // Version은 확률 결과가 무효가 될 때마다 올라감 (카드 구성이 같아도 무효화를 지나면 다른 요청)
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

        // ReadOnly로 감싸 계산 쪽이 이 배열을 작업 버퍼로 재사용하지 못하게 막음
        // 병렬 계산은 묶음마다 따로 복사해 쓰는데, 이 배열에 직접 쓰면 여러 스레드가 같은 배열을 건드리게 됨
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
