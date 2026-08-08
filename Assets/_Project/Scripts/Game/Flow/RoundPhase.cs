namespace PokerDefense.Game
{
    /**
     * RoundPhase
     * 
     * 카드 드로우 -> 카드 교체 -> 족보 판정 -> 유닛 배치
     */
    public enum RoundPhase
    {
        Draw,
        Exchange,
        Evaluate,
        Place,
    }
}
