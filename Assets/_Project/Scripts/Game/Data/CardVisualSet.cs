using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CardVisualSet
     *
     * 카드 한 장을 그리는 데 필요한 스프라이트 묶음
     * 에셋 1개만 두고 전역으로 사용
     *
     * 52장을 개별 생성하지 않고 프레임 + 무늬 아이콘 + rank 텍스트를 런타임 합성
     * 실제 이미지는 프레임·하이라이트·무늬 4종 + 카드 rank는 텍스트
     */
    [CreateAssetMenu(menuName = "PokerDefense/Card Visual Set", fileName = "CardVisualSet")]
    public sealed class CardVisualSet : ScriptableObject
    {
        [SerializeField] Sprite front;
        [SerializeField] Sprite highlight;

        [Header("무늬")]
        [SerializeField] Sprite spade;
        [SerializeField] Sprite heart;
        [SerializeField] Sprite diamond;
        [SerializeField] Sprite club;

        public Sprite Front => front;
        public Sprite Highlight => highlight;

        public Sprite SuitOf(Suit suit) => suit switch
        {
            Suit.Spade => spade,
            Suit.Heart => heart,
            Suit.Diamond => diamond,
            _ => club,
        };
    }
}
