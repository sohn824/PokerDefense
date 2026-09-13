using System;

namespace PokerDefense.Game
{
    // 한 런의 전투 진행과 라이프·승급권만 소유한다. 손패 기회는 RoundContext가 소유한다.
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
