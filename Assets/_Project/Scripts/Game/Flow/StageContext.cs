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
        public bool IsGameOver => Life <= 0;
        public bool IsAllWavesCleared => WaveIndex >= TotalWaves;
        public WaveDefinition CurrentWave => IsAllWavesCleared ? null : definition.Waves[WaveIndex];

        // 시간 초과면 라이프를 깎고, 클리어면 승급권 보상을 줌 (어느 쪽이든 다음 웨이브로 넘어감)
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
                Life = Math.Max(0, Life - LifeDamageFor(unresolvedEnemies, unresolvedBosses));
            }
            else
            {
                Jokers += CurrentWave.JokerReward;
            }
            WaveIndex++;
        }

        // 깎을 라이프 - 보스가 하나라도 남으면 상한, 아니면 남은 적 수만큼 (상한까지)
        public int LifeDamageFor(int unresolvedEnemies, int unresolvedBosses)
        {
            return unresolvedBosses > 0 ? definition.MaxLifeDamagePerWave
                : Math.Min(unresolvedEnemies, definition.MaxLifeDamagePerWave);
        }

        public bool TryUseJoker()
        {
            if (Jokers <= 0) return false;
            Jokers--;
            return true;
        }
    }
}
