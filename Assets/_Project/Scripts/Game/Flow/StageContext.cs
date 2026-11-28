using System;

namespace PokerDefense.Game
{
    /**
     * StageContext
     *
     * 한 Run의 웨이브 진행(WaveIndex)과 라이프·승급권을 소유함
     * Joker는 보스 보상 승급권의 내부 이름
     * 손패와 교체 상태는 라운드마다 새로 만드는 RoundContext가 소유함
     */
    public sealed class StageContext
    {
        readonly StageDefinition definition;

        public StageContext(StageDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Life = definition.StartingLife;
        }

        public int Life { get; private set; }
        public int Jokers { get; private set; }
        public int WaveIndex { get; private set; }
        public int TotalWaves => definition.Waves.Count;

        // 라이프 위험 기준 (CombatScreen의 Vignette 기준)
        public int DangerLife => definition.DangerLife;

        // 마지막 전투 결과로 깎인 라이프 (클리어면 0)
        public int LastLifeLost { get; private set; }

        public bool IsGameOver => Life <= 0;
        public bool IsAllWavesCleared => WaveIndex >= TotalWaves;
        public WaveDefinition CurrentWave => IsAllWavesCleared ? null : definition.Waves[WaveIndex];

        // 시간 초과면 라이프를 깎고, 클리어면 승급권 보상을 줌 (어느 쪽이든 다음 웨이브로 넘어감)
        // 깎는 라이프 - 처치하지 못한 적 수만큼 (상한 없음), 보스가 하나라도 남으면 남은 라이프 전부 (패배)
        public void ApplyResult(CombatOutcome outcome, int unresolvedEnemies, int unresolvedBosses)
        {
            if (outcome == CombatOutcome.InProgress || IsGameOver || IsAllWavesCleared)
            {
                throw new InvalidOperationException("종료된 전투의 결과만 한 번 적용할 수 있습니다");
            }
            if (unresolvedEnemies < 0 || unresolvedBosses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unresolvedEnemies));
            }
            if (outcome == CombatOutcome.TimedOut)
            {
                LastLifeLost = unresolvedBosses > 0 ? Life : Math.Min(unresolvedEnemies, Life);
                Life -= LastLifeLost;
            }
            else
            {
                LastLifeLost = 0;
                Jokers += CurrentWave.JokerReward;
            }
            WaveIndex++;
        }

        public bool TryUseJoker()
        {
            if (Jokers <= 0) return false;
            Jokers--;
            return true;
        }

        // 이어하기 - 저장해 둔 라이프·승급권·웨이브 번호로 되돌림
        // 끝난 Run(라이프 0, 모든 웨이브 클리어)은 저장하지 않으므로 그런 값이 오면 예외
        public void Restore(int life, int jokers, int waveIndex)
        {
            if (life <= 0 || life > definition.StartingLife)
            {
                throw new ArgumentOutOfRangeException(nameof(life), $"라이프는 1~{definition.StartingLife}이어야 함: {life}");
            }
            if (jokers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jokers), $"승급권은 0 이상이어야 함: {jokers}");
            }
            if (waveIndex < 0 || waveIndex >= TotalWaves)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex), $"웨이브 번호는 0~{TotalWaves - 1}이어야 함: {waveIndex}");
            }

            Life = life;
            Jokers = jokers;
            WaveIndex = waveIndex;
            LastLifeLost = 0;
        }
    }
}
