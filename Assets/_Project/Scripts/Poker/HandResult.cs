using System.Collections.Generic;

namespace PokerDefense.Poker
{
    /**
     * HandResult
     *
     * 족보 판정 결과 (족보 종류 + 그 족보를 만든 카드)
     */
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
