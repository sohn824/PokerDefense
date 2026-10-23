using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CardVisualSet
     *
     * 카드 무늬 4종의 스프라이트를 담는 ScriptableObject (Data/CardVisualSet.asset 하나를 모든 카드가 함께 씀)
     * 52장을 따로 그리지 않고, 카드 프리팹 바탕 위에 무늬 스프라이트와 숫자 텍스트를 얹어 그림
     */
    [CreateAssetMenu(menuName = "PokerDefense/Card Visual Set", fileName = "CardVisualSet")]
    public sealed class CardVisualSet : ScriptableObject
    {
        [Header("무늬")]
        [SerializeField] Sprite spade;
        [SerializeField] Sprite heart;
        [SerializeField] Sprite diamond;
        [SerializeField] Sprite club;

        public Sprite SuitOf(Suit suit) => suit switch
        {
            Suit.Spade => spade,
            Suit.Heart => heart,
            Suit.Diamond => diamond,
            _ => club,
        };
    }
}
