using System;

namespace PokerDefense.Game
{
    /**
     * StageContext
     *
     * 한 판(스테이지) 동안 유지되는 상태 - 웨이브 진행, 라이프, Chip
     * 전투 하나보다 오래 살고 스테이지가 끝나면 전부 사라진다 (메타 성장 없음)
     * 클리어하면 다음 웨이브로, 시간 초과면 남은 적 수만큼 라이프가 깎인다 (DESIGN §5.4)
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
            Chip = definition.StartingChip;
        }

        public int Life { get; private set; }

        // 스테이지 안에서만 쓰는 재화 (DESIGN §9.2)
        public int Chip { get; private set; }

        // 아무 유닛의 성급을 한 단계 올린다. 보스 웨이브를 클리어할 때만 들어온다 (DESIGN §5.2.1)
        public int Jokers { get; private set; }

        // 결말이 난 웨이브 수. 클리어든 시간 초과든 다음으로 넘어간다
        public int WaveIndex { get; private set; }

        public int TotalWaves => definition.Waves.Count;

        public bool IsGameOver => Life <= 0;

        public bool IsAllWavesCleared => WaveIndex >= definition.Waves.Count;

        public WaveDefinition CurrentWave
            => IsAllWavesCleared ? null : definition.Waves[WaveIndex];

        /**
         * 전투 결과를 반영하고 다음 웨이브로 넘긴다
         * unresolvedEnemies는 아직 안 나온 적까지 포함한 잔여 수다
         */
        public void ApplyResult(CombatOutcome outcome, int unresolvedEnemies, int unresolvedBosses)
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
                Life = Math.Max(0, Life - LifeDamageFor(unresolvedEnemies, unresolvedBosses));
            }
            else
            {
                Jokers += CurrentWave.JokerReward;
            }

            WaveIndex++;
        }

        /**
         * 잔여 적이 실제로 깎는 라이프
         *
         * 상한이 없으면 Swarm 웨이브(적 10기 이상)를 한 번 놓치는 순간 게임이 끝나고,
         * 실수 한 번이 곧 패배가 되면 보드를 실험해 볼 여지가 사라진다 (DESIGN §5.4)
         *
         * 반대로 보스를 놓치면 잔여가 한 기뿐이라도 상한만큼 친다.
         * 보스 웨이브를 실패하고 라이프 1만 잃으면 보스가 벽으로 기능하지 못한다
         *
         * ApplyResult와 화면 표기가 같은 답을 내도록 규칙을 여기 한 곳에 둔다
         */
        public int LifeDamageFor(int unresolvedEnemies, int unresolvedBosses)
        {
            if (unresolvedBosses > 0)
            {
                return definition.MaxLifeDamagePerWave;
            }

            return Math.Min(unresolvedEnemies, definition.MaxLifeDamagePerWave);
        }

        /// <summary>Joker를 한 개 쓴다. 없으면 false를 돌려주고 아무것도 바뀌지 않는다.</summary>
        public bool TryUseJoker()
        {
            if (Jokers <= 0)
            {
                return false;
            }

            Jokers--;
            return true;
        }

        public void AddChip(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), $"획득 Chip이 음수입니다: {amount}");
            }

            Chip += amount;
        }

        /// <summary>Chip이 모자라면 false를 돌려주고 아무것도 바뀌지 않는다.</summary>
        public bool TrySpendChip(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), $"소비 Chip이 음수입니다: {amount}");
            }

            if (Chip < amount)
            {
                return false;
            }

            Chip -= amount;
            return true;
        }
    }
}
