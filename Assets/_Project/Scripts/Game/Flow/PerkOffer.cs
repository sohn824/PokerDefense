using System;
using System.Collections.Generic;

namespace PokerDefense.Game
{
    /**
     * PerkOffer
     *
     * 보스를 잡았을 때 내놓을 3칸을 뽑는다 (DESIGN §11)
     *
     * 계열을 섞어 뽑는다 - 포커/경제에서 1, 유닛/머지에서 1, 나머지에서 1
     * 셋을 그냥 랜덤으로 뽑으면 비슷한 것만 셋 나오는 판이 생긴다
     *
     * System.Random을 주입받아 테스트에서 결과를 재현할 수 있다 - Deck·SupportSummon과 같은 방식이다
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
         * 이번에 내놓을 특전. 이미 가진 특전과 이번에 뽑힌 특전은 다시 나오지 않는다
         *
         * 남은 특전이 3개보다 적으면 그만큼만 돌려준다.
         * 특전 6종에 한 판 2개라 지금은 닿지 않지만, 표를 줄여도 뽑기가 터지지는 않아야 한다
         */
        public IReadOnlyList<PerkId> Draw(PerkSet owned)
        {
            if (owned == null)
            {
                throw new ArgumentNullException(nameof(owned));
            }

            offered.Clear();

            TakeOne(owned, PerkCategory.Poker, PerkCategory.Economy);
            TakeOne(owned, PerkCategory.Unit, PerkCategory.Merge);
            TakeOne(owned);

            // offered는 다음 뽑기에서 비운다. 호출부가 들고 있어도 되도록 복사해 넘긴다
            return new List<PerkId>(offered);
        }

        // 두 계열에서 하나. 그 계열이 바닥났으면 남은 아무 특전이나 채운다
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
            Collect(owned, false, default, default);
            TakeRandom();
        }

        // byCategory가 false면 계열을 보지 않는다
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
