using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RunStats
     *
     * 한 판의 성과 기록. 결과 화면에만 쓰인다
     * 규칙을 바꾸지 않으므로 StageContext(라이프·웨이브)와 섞지 않고 따로 둔다
     *
     * 메타 성장이 없는 게임에서 "지난번보다 잘했다"를 느끼게 하는 유일한 장치라 패배해도 보여준다
     */
    public sealed class RunStats
    {
        // 이번 판에 소환된 유닛 수. 손패로 소환한 유닛을 센다
        public int Summons { get; private set; }

        // 이번 판에 만든 가장 희귀한 족보. 한 번도 확정하지 않았으면 null
        public HandCategory? BestHand { get; private set; }

        // 이번 판에 가진 가장 센 유닛. 성급과 종류를 함께 반영하려고 DPS로 비교한다
        public UnitInstance BestUnit { get; private set; }


        public void RecordHand(HandCategory category)
        {
            if (BestHand.HasValue && HandRarity.RankOf(BestHand.Value) >= HandRarity.RankOf(category))
            {
                return;
            }

            BestHand = category;
        }

        /// <summary>유닛이 새로 소환됐다. 퇴장했더라도 소환한 사실은 남는다.</summary>
        public void RecordSummon(UnitInstance unit)
        {
            Summons++;
            RecordUnit(unit);
        }

        /// <summary>머지나 Joker로 유닛이 강해졌다. 소환 수는 늘지 않는다.</summary>
        public void RecordUnit(UnitInstance unit)
        {
            if (unit == null)
            {
                return;
            }

            if (BestUnit != null && Dps(BestUnit) >= Dps(unit))
            {
                return;
            }

            BestUnit = unit;
        }



        static float Dps(UnitInstance unit) => unit.AttackPower * unit.AttacksPerSecond;
    }
}
