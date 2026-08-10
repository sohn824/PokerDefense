using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * PerkSet
     *
     * 이번 판에 고른 딜러 특전과, 특전이 바꾸는 값 전부 (DESIGN §11)
     *
     * 특전은 유지 보너스·지원 소환 비용·웨이브 수입·공격력을 가로질러 바꾼다
     * 그 조건을 호출부에 흩으면 곳곳에 if가 생기므로 여기 한 곳에만 둔다
     * 호출부는 "지금 값이 얼마인가"만 묻고 어떤 특전이 걸렸는지는 모른다
     *
     * 특전을 늘릴 때 손대는 곳도 여기와 PerkTable 두 곳뿐이다
     *
     * 스테이지 수명과 정확히 같아 StageContext가 소유한다.
     * 규칙을 바꾸므로 RunStats처럼 따로 떼지 않는다
     */
    public sealed class PerkSet
    {
        /// <summary>특전이 하나도 없고 더 가질 수도 없는 집합. 특전을 모르는 호출부가 쓴다.</summary>
        public static PerkSet Empty { get; } = new PerkSet(null);

        readonly PerkTable table;
        readonly List<PerkId> owned = new List<PerkId>();

        public PerkSet(PerkTable table)
        {
            this.table = table;
        }

        public IReadOnlyList<PerkId> Owned => owned;

        public bool Has(PerkId id) => owned.Contains(id);

        public void Add(PerkId id)
        {
            if (table == null)
            {
                throw new InvalidOperationException("PerkTable이 없는 집합에는 특전을 넣을 수 없습니다.");
            }

            if (Has(id))
            {
                throw new InvalidOperationException($"이미 가진 특전입니다: {id}");
            }

            // 표에 없는 특전이면 여기서 예외가 난다. 규칙이 도는 중에 터지는 것보다 낫다
            table.For(id);
            owned.Add(id);
        }

        // ---------- 특전이 바꾸는 값 ----------

        /// <summary>유지 보너스 (DESIGN §9.1). Patience는 교체를 한 장도 안 썼을 때만 얹는다.</summary>
        public int HoldBonus(int baseBonus, int usedExchanges)
        {
            if (Has(PerkId.Patience) && usedExchanges == 0)
            {
                return baseBonus + (int)Amount(PerkId.Patience);
            }

            return baseBonus;
        }

        /// <summary>확정할 때 유지 보너스와 별도로 받는 Chip. Insurance는 교체를 쏟고도 하이카드로 끝났을 때 준다.</summary>
        public int ConfirmBonusChip(HandCategory category, int usedExchanges)
        {
            if (Has(PerkId.Insurance)
                && category == HandCategory.HighCard
                && usedExchanges >= (int)Limit(PerkId.Insurance))
            {
                return (int)Amount(PerkId.Insurance);
            }

            return 0;
        }

        /// <summary>지원 소환 비용 (DESIGN §9.3).</summary>
        public int SupportSummonCost(int baseCost)
        {
            if (Has(PerkId.Bargain))
            {
                return Math.Max(0, baseCost - (int)Amount(PerkId.Bargain));
            }

            return baseCost;
        }

        /// <summary>웨이브가 끝날 때 보유 Chip에 붙는 이자. 클리어든 시간 초과든 웨이브가 끝나면 받는다.</summary>
        public int WaveEndChip(int chip)
        {
            if (Has(PerkId.Interest) == false)
            {
                return 0;
            }

            int per = (int)Amount(PerkId.Interest);

            if (per <= 0)
            {
                return 0;
            }

            return Math.Min((int)Limit(PerkId.Interest), chip / per);
        }

        /**
         * 특전까지 반영한 공격력
         *
         * UnitInstance는 스테이지를 모르는 불변 객체라 스스로 특전을 반영할 수 없다
         * 전투 계산과 상세 블록 표기가 반드시 같은 수를 내야 하므로(DESIGN §10.3) 두 곳이 이 함수를 부른다
         */
        public float AttackPowerOf(UnitInstance unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return unit.AttackPower * (1f + AttackBonusFor(unit.Star));
        }

        float AttackBonusFor(int star)
        {
            if (star <= 1)
            {
                return Has(PerkId.RookieTraining) ? Amount(PerkId.RookieTraining) : 0f;
            }

            return Has(PerkId.Veteran) ? Amount(PerkId.Veteran) : 0f;
        }

        float Amount(PerkId id) => table.For(id).amount;

        float Limit(PerkId id) => table.For(id).limit;
    }
}
