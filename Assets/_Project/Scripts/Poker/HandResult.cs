using System.Collections.Generic;

namespace PokerDefense.Poker
{
    public readonly struct HandResult
    {
        public readonly HandCategory Category;

        /// <summary>
        /// 연출용 하이라이트 대상. 페어면 2장, 스트레이트/플러시류면 5장 전부.
        /// </summary>
        public readonly IReadOnlyList<Card> KeyCards;

        public HandResult(HandCategory category, IReadOnlyList<Card> keyCards)
        {
            Category = category;
            KeyCards = keyCards;
        }

        public override string ToString() => $"{Category} [{string.Join(" ", KeyCards)}]";
    }
}
