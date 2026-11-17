using System;
using System.Collections.Generic;

namespace PokerDefense.Game
{
    /**
     * RoundActionType
     *
     * RoundContext가 기록하는 행동의 종류
     * Draw - 손패 5장을 뽑음
     * Exchange - 고른 자리들을 일반 교체함
     * RevealCandidates - 선택 교체 후보를 공개함
     * ChooseCandidate - 공개한 후보 중 한 장으로 바꿈
     * FinishExchange - 교체를 끝냄
     * Evaluate - 족보를 판정함
     */
    public enum RoundActionType
    {
        Draw,
        Exchange,
        RevealCandidates,
        ChooseCandidate,
        FinishExchange,
        Evaluate,
    }

    /**
     * RoundAction
     *
     * RoundContext에서 성공한 행동 하나
     * 같은 시드로 만든 RoundContext에 같은 순서로 재현하면 손패·덱·후보가 원래와 같아짐
     * 이어하기에서 라운드를 복원하는 데 씀
     *
     * Indices - Exchange에서 바꾼 자리들 (다른 종류는 빈 목록)
     * Index - RevealCandidates에서 고른 자리, ChooseCandidate에서 고른 후보 번호 (다른 종류는 -1)
     */
    public readonly struct RoundAction
    {
        RoundAction(RoundActionType type, IReadOnlyList<int> indices, int index)
        {
            Type = type;
            Indices = indices;
            Index = index;
        }

        public RoundActionType Type { get; }
        public IReadOnlyList<int> Indices { get; }
        public int Index { get; }

        public static RoundAction Of(RoundActionType type) => new RoundAction(type, Array.Empty<int>(), -1);

        // 넘겨받은 목록을 복사해 담으므로 나중에 원본이 바뀌어도 기록은 그대로임
        public static RoundAction Exchange(IReadOnlyList<int> indices)
        {
            int[] copy = new int[indices.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = indices[i];
            }

            return new RoundAction(RoundActionType.Exchange, copy, -1);
        }

        public static RoundAction RevealCandidates(int index) => new RoundAction(RoundActionType.RevealCandidates, Array.Empty<int>(), index);

        public static RoundAction ChooseCandidate(int index) => new RoundAction(RoundActionType.ChooseCandidate, Array.Empty<int>(), index);
    }
}
