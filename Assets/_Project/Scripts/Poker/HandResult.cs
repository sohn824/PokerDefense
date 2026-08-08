using System.Collections.Generic;

namespace PokerDefense.Poker
{
    public readonly struct HandResult
    {
        public readonly HandCategory Category;

        // 족보로 결정된 key 카드 리스트 (ex - 원 페어면 2장 / 스트레이트면 5장)
        public readonly IReadOnlyList<Card> KeyCards;

        public HandResult(HandCategory category, IReadOnlyList<Card> keyCards)
        {
            Category = category;
            KeyCards = keyCards;
        }

        public override string ToString() => $"{Category} [{string.Join(" ", KeyCards)}]";
    }
}
