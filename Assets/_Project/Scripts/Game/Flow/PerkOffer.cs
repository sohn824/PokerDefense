using System;
using System.Collections.Generic;

namespace PokerDefense.Game
{
    /**
     * PerkOffer
     *
     * 보스를 잡았을 때 내놓을 특전 3칸을 뽑는 역할
     *
     * 계열을 섞어 뽑음
     * (포커/경제에서 1, 유닛/머지에서 1, 올랜덤 1)
     * 같은 계열이 셋 나오는 판이 생기지 않도록 함
     */
    public sealed class PerkOffer
    {
        public const int OfferCount = 3;

        readonly PerkTable table;
        readonly Random random;
        readonly List<PerkId> candidates = new List<PerkId>();
        readonly List<PerkId> offered = new List<PerkId>();

        public PerkOffer(PerkTable table, int seed)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            this.table = table;
            random = new Random(seed);
        }

        /**
         * 특전 리스트 뽑기
         * 이미 가진 특전과 이번에 뽑힌 특전은 다시 나오지 않음
         *
         * 남은 특전이 3개보다 적으면 그만큼만 반환함
         */
        public IReadOnlyList<PerkId> DrawPerks(PerkSet owned)
        {
            if (owned == null)
            {
                throw new ArgumentNullException(nameof(owned));
            }

            offered.Clear();

            TakeOne(owned, PerkCategory.Poker, PerkCategory.Economy);
            TakeOne(owned, PerkCategory.Unit, PerkCategory.Merge);
            TakeOne(owned);

            return new List<PerkId>(offered);
        }

        // 두 계열 중 하나로 후보를 뽑음
        // (계열에 있는 특전이 바닥났으면 남은 아무 특전이나 후보로 채움)
        void TakeOne(PerkSet owned, PerkCategory a, PerkCategory b)
        {
            Collect(owned, true, a, b);

            if (candidates.Count == 0)
            {
                Collect(owned, false, a, b);
            }

            TakeRandom();
        }

        void TakeOne(PerkSet owned)
        {
            // byCategory가 false면 뒤의 두 인자는 의미 없음
            Collect(owned, false, default, default);
            TakeRandom();
        }

        // byCategory가 false면 계열을 보지 않고 모든 특전 중에 후보를 뽑음
        void Collect(PerkSet owned, bool byCategory, PerkCategory a, PerkCategory b)
        {
            candidates.Clear();

            IReadOnlyList<PerkTable.PerkEntry> entries = table.Entries;

            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                PerkTable.PerkEntry entry = entries[i];

                if (owned.Has(entry.id) || offered.Contains(entry.id))
                {
                    continue;
                }

                if (byCategory && entry.category != a && entry.category != b)
                {
                    continue;
                }

                candidates.Add(entry.id);
            }
        }

        void TakeRandom()
        {
            if (candidates.Count == 0)
            {
                return;
            }

            offered.Add(candidates[random.Next(candidates.Count)]);
        }
    }
}
