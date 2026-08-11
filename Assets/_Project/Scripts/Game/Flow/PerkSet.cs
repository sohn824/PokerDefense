using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * PerkSet
     *
     * 이번 판에 고른 딜러 특전과, 특전이 바꾸는 값 전부의 집합
     *
     * 특전은 유지 보너스·지원 소환 비용·웨이브 수입·공격력을 가로질러 바꾼다
     * 그 조건을 호출부에 흩으면 곳곳에 if가 생기므로 여기 한 곳으로 모음
     * 호출부는 "지금 값이 얼마인가"만 묻고 어떤 특전이 걸렸는지는 모름 (여기서 알아서 계산)
     */
    public sealed class PerkSet
    {
        // 특전이 하나도 없고 더 가질 수도 없는 집합을 뜻함 (특전이 없는 상태 표현)
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

            table.GetEntry(id);
            owned.Add(id);
        }

        ///특전이 바꾸는 값들

        // 유지 보너스 Chip 체크
        // (Patience 특전은 교체를 한 장도 안 썼을 때 보너스 Chip을 줌)
        public int HoldBonus(int baseBonus, int usedExchanges)
        {
            if (Has(PerkId.Patience) && usedExchanges == 0)
            {
                return baseBonus + (int)Amount(PerkId.Patience);
            }

            return baseBonus;
        }

        // 확정할 때 유지 보너스와 별도로 받는 보너스 Chip 체크
        // (Insurance 특전은 교체를 쏟고도 손패가 하이카드로 끝났을 때 보너스 Chip을 줌)
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

        // 지원 소환 비용 보너스
        public int SupportSummonCost(int baseCost)
        {
            if (Has(PerkId.Bargain))
            {
                return Math.Max(0, baseCost - (int)Amount(PerkId.Bargain));
            }

            return baseCost;
        }

        // 웨이브가 끝날 때 보유 Chip에 붙는 이자 보너스
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

        // 특전까지 반영한 공격력 구하기
        // (UnitInstance는 스테이지 정보를 모르는 객체이므로 스스로 특전을 반영할 수 없으므로 여기서 계산)
        public float AttackPowerOf(UnitInstance unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return unit.AttackPower * (1f + AttackBonusFor(unit.Star));
        }

        // 성급에 따른 특전들의 공격력 증가율 구하기
        // (RookieTraining은 ★1만, Veteran은 ★2 이상의 유닛에 보너스 공격력 적용)
        float AttackBonusFor(int star)
        {
            if (star <= 1)
            {
                return Has(PerkId.RookieTraining) ? Amount(PerkId.RookieTraining) : 0f;
            }

            return Has(PerkId.Veteran) ? Amount(PerkId.Veteran) : 0f;
        }

        float Amount(PerkId id) => table.GetEntry(id).amount;

        float Limit(PerkId id) => table.GetEntry(id).limit;
    }
}
