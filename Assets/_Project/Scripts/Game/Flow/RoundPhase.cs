namespace PokerDefense.Game
{
    /**
     * RoundPhase
     *
     * Draw -> Exchange -> Evaluate -> Place
     * (카드 드로우 -> 카드 교체 -> 족보 판정 -> 유닛 배치)
     * Draw Phase와 Evaluate Phase는 바로 다음 Phase로 넘어가므로 실제로 머무는 Phase는 Exchange와 Place뿐임
     * 전투 중에도 Place Phase로 남아 있고, 다음 라운드에서 RoundContext를 새로 만들 때 Draw Phase로 돌아감
     */
    public enum RoundPhase
    {
        Draw,
        Exchange,
        Evaluate,
        Place,
    }
}
