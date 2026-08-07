namespace PokerDefense.Game
{
    /// <summary>
    /// 라운드 페이즈. DESIGN §1의 상태 머신 중 M2 범위(Draw~Evaluate)만 구현한다.
    /// Place는 Evaluate가 끝났음을 나타내는 종점으로만 쓰이며, 실제 배치는 M3에서 붙인다.
    /// </summary>
    public enum RoundPhase
    {
        Draw,
        Exchange,
        Evaluate,
        Place,
    }
}
