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
        public StageContext(StageDefinition definition, PerkTable perks = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            this.definition = definition;
            Life = definition.StartingLife;
            Chip = definition.StartingChip;
            Perks = perks == null ? PerkSet.Empty : new PerkSet(perks);
        }

        public int Life { get; private set; }

        // 이번 판에 고른 딜러 특전 (특전이 바꾸는 값들은 전부 여기를 거쳐서 계산)
        public PerkSet Perks { get; }

        // 스테이지 안에서만 쓰는 보너스 재화
        public int Chip { get; private set; }

        // 조커를 사용하면 아무 유닛의 성급을 한 단계 올릴 수 있음 (보스 웨이브를 클리어로 지급)
        public int Jokers { get; private set; }

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

            // 이자는 웨이브가 끝나면 받는다. 클리어했는지는 보지 않는다 (Interest 특전)
            Chip += Perks.WaveEndChip(Chip);

            WaveIndex++;
        }

        // 잔여 적이 깎는 라이프 계산
        public int LifeDamageFor(int unresolvedEnemies, int unresolvedBosses)
        {
            if (unresolvedBosses > 0)
            {
                return definition.MaxLifeDamagePerWave;
            }

            return Math.Min(unresolvedEnemies, definition.MaxLifeDamagePerWave);
        }

        // Joker를 한 개 사용함 (없으면 false를 반환하고 실패 처리)
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
