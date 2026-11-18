using System;
using System.Collections.Generic;

namespace PokerDefense.Game
{
    /**
     * RunSaveData
     *
     * 이어하기용으로 진행 중인 Run을 파일에 저장할 때 쓰는 값 묶음 (JsonUtility로 읽고 씀)
     * JsonUtility가 읽고 쓸 수 있도록 public 필드만 두고, null 대신 빈 문자열·-1·0으로 "없음"을 나타냄
     *
     * 라운드는 손패·덱을 저장하지 않고 시드와 행동 기록만 저장함 (RoundContext.Replay로 재현)
     * 유닛은 UnitDefinition.Id와 성급으로 저장함
     * 저장 형식을 바꾸면 FormatVersion을 올림 (옛 저장은 변환하지 않고 버림)
     */
    [Serializable]
    public sealed class RunSaveData
    {
        public const int FormatVersion = 1;

        public int version = FormatVersion;
        public string ruleset = RunResult.CurrentRuleset;

        // 스테이지 진행
        public int life;
        public int jokers;
        public int waveIndex;

        // Run 진행
        public int roundNumber;
        public float elapsedSeconds;
        public bool usedDevSkip;
        public bool inCombat;

        // 라운드
        public int roundSeed;
        public List<RoundActionData> roundActions = new List<RoundActionData>();

        // 보드 - 칸 번호 순서, 빈 칸은 빈 문자열과 성급 0
        public string[] boardUnitIds = NewIds();
        public int[] boardStars = new int[GridBoard.SlotCount];

        // 배치 대기 유닛 - 없으면 빈 문자열과 성급 0
        public string pendingUnitId = string.Empty;
        public int pendingStar;

        // 결과 기록 - 최고 족보가 없으면 -1, 최고 유닛이 없으면 빈 문자열과 성급 0
        public int summons;
        public int bestHand = -1;
        public string bestUnitId = string.Empty;
        public int bestUnitStar;

        static string[] NewIds()
        {
            string[] ids = new string[GridBoard.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = string.Empty;
            }

            return ids;
        }
    }

    /**
     * RoundActionData
     *
     * RoundAction 하나를 저장용으로 옮긴 값
     * type은 RoundActionType의 정수 값이므로 RoundActionType 순서를 바꾸면 옛 저장을 잘못 읽음
     */
    [Serializable]
    public sealed class RoundActionData
    {
        public int type;
        public int[] indices = Array.Empty<int>();
        public int index = -1;

        public static RoundActionData From(RoundAction action)
        {
            int[] copy = new int[action.Indices.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = action.Indices[i];
            }

            return new RoundActionData { type = (int)action.Type, indices = copy, index = action.Index };
        }

        // 저장한 값을 RoundAction으로 되돌림
        // 알 수 없는 종류면 예외 (자리·후보 번호가 맞는지는 RoundContext.Replay가 확인함)
        public RoundAction ToAction()
        {
            switch ((RoundActionType)type)
            {
                case RoundActionType.Draw:
                case RoundActionType.FinishExchange:
                case RoundActionType.Evaluate:
                    return RoundAction.Of((RoundActionType)type);
                case RoundActionType.Exchange:
                    return RoundAction.Exchange(indices ?? Array.Empty<int>());
                case RoundActionType.RevealCandidates:
                    return RoundAction.RevealCandidates(index);
                case RoundActionType.ChooseCandidate:
                    return RoundAction.ChooseCandidate(index);
                default:
                    throw new InvalidOperationException($"알 수 없는 행동 종류: {type}");
            }
        }
    }
}
