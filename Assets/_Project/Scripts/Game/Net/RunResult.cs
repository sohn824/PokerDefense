using System;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RunResult
     *
     * 게임 한 판이 끝났을 때의 결과 데이터
     * 랭킹 서버로 보내는 단위
     * (보내기 전에 outbox(전송 대기 폴더)에 파일로 저장)
     * 생성 후 값이 바뀌지 않으므로 전송 스레드에 그대로 넘겨도 안전함
     */
    public sealed class RunResult
    {
        // 게임 규칙의 버전 Key
        // 랭킹 서버가 아는 Key만 유효한 결과로 받고, Key에 맞는 값 범위(예: 웨이브 50, 라이프 0~20 등)로 검사
        // 게임 규칙이 바뀌면 Key를 바꾸고 서버 검사도 함께 고쳐야 함
        public const string CurrentRuleset = "hand-loop-v1";

        public RunResult(Guid runId, string ruleset, int wave, int totalWaves, bool cleared, int life, int summons,
            HandCategory? bestHand, int elapsedMs)
        {
            RunId = runId;
            Ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
            Wave = wave;
            TotalWaves = totalWaves;
            Cleared = cleared;
            Life = life;
            Summons = summons;
            BestHand = bestHand;
            ElapsedMs = elapsedMs;
        }

        // 판마다 새로 만드는 고유 ID
        // 서버 응답(ACK)을 못 받아 같은 결과를 다시 보내도, 서버가 이 ID로 알아보고 한 번만 저장함
        public Guid RunId { get; }

        public string Ruleset { get; }   // 규칙 버전 (지금은 hand-loop-v1)
        public int Wave { get; }          // 마지막으로 치른 웨이브 번호 (1부터, 게임 오버를 낸 웨이브 포함)
        public int TotalWaves { get; }    // 전체 웨이브 수 (hand-loop-v1은 50)
        public bool Cleared { get; }      // 모든 웨이브를 치렀는지 (마지막 웨이브에서 라이프가 0이 돼도 true)
        public int Life { get; }          // 남은 라이프 (게임 오버면 0)
        public int Summons { get; }       // 총 소환 수 (라운드당 최대 1, 소환 포기·퇴장한 유닛도 포함)
        public HandCategory? BestHand { get; }  // 확정한 족보 중 가장 희귀한 것 (HandRarity 기준, 한 번도 확정 안 했으면 null)
        public int ElapsedMs { get; }     // 게임 시간 기준 (ms). 일시정지는 빠지고, 배속 중에는 실제 시간보다 빨리 흐름
    }
}
