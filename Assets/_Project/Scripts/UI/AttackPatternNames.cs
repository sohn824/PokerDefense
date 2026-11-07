using PokerDefense.Game;

namespace PokerDefense.UI
{
    /**
     * AttackPatternNames
     *
     * 유닛의 공격 패턴명 (화면 표기용)
     */
    public static class AttackPatternNames
    {
        public static string Of(AttackPattern pattern) => pattern switch
        {
            AttackPattern.Rapid => "연사",
            AttackPattern.Heavy => "강타",
            AttackPattern.Multi => "다중",
            AttackPattern.Splash => "범위",
            AttackPattern.Pierce => "관통",
            _ => pattern.ToString(),
        };
    }
}
