using System;

namespace PokerDefense.Game
{
    /**
     * StageContext
     *
     * 웨이브 진행과 라이프. 전투 하나보다 오래 사는 상태다
     * 클리어하면 다음 웨이브로, 시간 초과면 남은 적 수만큼 라이프가 깎인다 (DESIGN §5.4)
     * 잔여 적은 다음 웨이브로 넘기지 않는다 - CombatContext를 새로 만들기 때문에 자연히 사라진다
     */
    public sealed class StageContext
    {
        readonly StageDefinition definition;

        public StageContext(StageDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            this.definition = definition;
            Life = definition.StartingLife;
        }

        public int Life { get; private set; }

        public int WaveIndex { get; private set; }

        public bool IsGameOver => Life <= 0;

        public bool IsAllWavesCleared => WaveIndex >= definition.Waves.Count;

        public WaveDefinition CurrentWave
            => IsAllWavesCleared ? null : definition.Waves[WaveIndex];

        /**
         * 전투 결과를 반영하고 다음 웨이브로 넘긴다
         * unresolvedEnemies는 아직 안 나온 적까지 포함한 잔여 수다
         */
        public void ApplyResult(CombatOutcome outcome, int unresolvedEnemies)
        {
            if (outcome == CombatOutcome.InProgress)
            {
                throw new InvalidOperationException("전투가 끝나지 않았습니다");
            }

            if (IsGameOver)
            {
                throw new InvalidOperationException("이미 게임 오버입니다");
            }

            if (unresolvedEnemies < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unresolvedEnemies), $"잔여 적 수가 음수입니다: {unresolvedEnemies}");
            }

            if (outcome == CombatOutcome.TimedOut)
            {
                Life = Math.Max(0, Life - unresolvedEnemies);
            }

            WaveIndex++;
        }
    }
}
