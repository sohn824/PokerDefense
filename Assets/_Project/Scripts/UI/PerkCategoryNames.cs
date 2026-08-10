using PokerDefense.Game;

namespace PokerDefense.UI
{
    /// <summary>PerkCategory의 화면 표기. 계열 이름은 규칙이 아니라 표기라 UI에 둔다.</summary>
    public static class PerkCategoryNames
    {
        public static string Of(PerkCategory category)
        {
            switch (category)
            {
                case PerkCategory.Poker: return "포커";
                case PerkCategory.Economy: return "경제";
                case PerkCategory.Unit: return "유닛";
                case PerkCategory.Merge: return "머지";
                default: return category.ToString();
            }
        }
    }
}
