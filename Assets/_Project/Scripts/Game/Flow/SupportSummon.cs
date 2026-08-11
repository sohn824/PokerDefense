using System;
using System.Collections.Generic;

namespace PokerDefense.Game
{
    /**
     * SupportSummon
     *
     * Chip으로 뽑는 랜덤 하위 유닛 (DESIGN §9.3)
     * 포커를 거치지 않는다. 포커는 "고등급 확정 소환", 이쪽은 "머지 재료 수급"이다
     *
     * System.Random을 주입받아 테스트에서 결과를 재현할 수 있다 - Deck과 같은 방식이다
     */
    public sealed class SupportSummon
    {
        readonly EconomyDefinition economy;
        readonly HandUnitTable table;
        readonly Random random;
        readonly int totalWeight;

        public SupportSummon(EconomyDefinition economy, HandUnitTable table, int seed)
        {
            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            this.economy = economy;
            this.table = table;
            random = new Random(seed);

            IReadOnlyList<EconomyDefinition.SupportEntry> pool = economy.SupportPool;

            // 에셋에서 배열을 안 채우면 null로 들어온다
            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                if (pool[i].weight > 0)
                {
                    totalWeight += pool[i].weight;
                }
            }

            if (totalWeight <= 0)
            {
                throw new InvalidOperationException($"{economy.name}의 지원 소환 풀이 비어 있습니다.");
            }
        }

        public UnitDefinition Draw()
        {
            int roll = random.Next(totalWeight);
            IReadOnlyList<EconomyDefinition.SupportEntry> pool = economy.SupportPool;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].weight <= 0)
                {
                    continue;
                }

                roll -= pool[i].weight;

                if (roll < 0)
                {
                    return table.GetDefinition(pool[i].category);
                }
            }

            // 가중치 합을 미리 구해두므로 여기에 닿지 않는다
            throw new InvalidOperationException("지원 소환 추첨에 실패했습니다.");
        }
    }
}
