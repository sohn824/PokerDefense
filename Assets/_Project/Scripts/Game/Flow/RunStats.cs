using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RunStats
     *
     * 한 Run의 성과를 모으는 객체
     * 모으는 값 - 소환 수, 최고 족보, 최고 유닛
     * 쓰는 곳 - ResultScreen(최종 결과 화면), RunReporter(랭킹 서버 전송)
     */
    public sealed class RunStats
    {
        // 소환된 유닛(Pending)을 보드에 놓은 횟수 (배치·머지·교체 모두 포함)
        // 소환을 포기하면 세지 않고, 퇴장해도 줄지 않음
        public int Summons { get; private set; }

        // 이번 Run에서 확정한 족보 중 가장 희귀한 것 (HandRarity 기준, 확정한 적이 없으면 null)
        public HandCategory? BestHand { get; private set; }

        // 이번 Run에 보드에 올랐던 유닛 중 DPS(공격력 × 초당 공격 횟수)가 가장 높은 유닛 (퇴장한 유닛 포함)
        public UnitInstance BestUnit { get; private set; }

        // 확정한 족보가 지금 기록보다 희귀하면 BestHand를 바꿈
        public void RecordHand(HandCategory category)
        {
            if (BestHand.HasValue && HandRarity.RankOf(BestHand.Value) >= HandRarity.RankOf(category))
            {
                return;
            }

            BestHand = category;
        }

        // Pending을 보드에 놓은 뒤 소환 수를 늘리고, 그 칸의 유닛이 BestUnit보다 DPS가 높으면 BestUnit도 바꿈
        public void RecordSummon(UnitInstance unit)
        {
            Summons++;
            RecordUnit(unit);
        }

        // unit의 DPS가 BestUnit보다 높으면 BestUnit을 바꿈 (소환 수는 그대로)
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
