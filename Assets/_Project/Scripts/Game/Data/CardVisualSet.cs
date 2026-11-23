using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * CardVisualSet
     *
     * 카드 무늬 4종의 스프라이트와 Foil 머티리얼을 담는 ScriptableObject
     * (Data/CardVisualSet.asset 하나를 모든 카드가 함께 씀)
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

        [Header("연출")]
        [Tooltip("스트레이트 이상 족보를 이룬 카드 바탕에 쓸 머티리얼 (CardFoil.mat)\nCardFoil 셰이더용")]
        [SerializeField] Material foil;

        // 없으면 Foil 연출 없이 기본 UI로 그림
        public Material Foil => foil;

        public Sprite SuitOf(Suit suit) => suit switch
        {
            Suit.Spade => spade,
            Suit.Heart => heart,
            Suit.Diamond => diamond,
            _ => club,
        };
    }
}
