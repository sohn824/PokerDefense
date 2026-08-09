using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * UnitDefinition
     * 
     * 유닛 종류 하나의 정의
     */
    [CreateAssetMenu(menuName = "PokerDefense/Unit Definition", fileName = "Unit_")]
    public sealed class UnitDefinition : ScriptableObject
    {
        public const int MaxStar = 3;

        [SerializeField] string id;
        [SerializeField] string displayName;

        [Tooltip("M6에서 스프라이트로 교체할 플레이스홀더 색")]
        [SerializeField] Color placeholderColor = Color.gray;

        [Header("★1 기준 스탯. 성급 배수를 곱해 쓴다.")]
        [SerializeField] float attackPower = 10f;
        [SerializeField] float attacksPerSecond = 1f;
        [SerializeField] float range = 2f;

        [Tooltip("★1 / ★2 / ★3 스탯 배수")]
        [SerializeField] float[] starMultipliers = { 1f, 2f, 4f };

        public string Id => id;
        public string DisplayName => displayName;
        public Color PlaceholderColor => placeholderColor;
        public float AttackPower => attackPower;
        public float AttacksPerSecond => attacksPerSecond;
        public float Range => range;

        public float MultiplierFor(int star)
        {
            int index = Mathf.Clamp(star, 1, MaxStar) - 1;
            return index < starMultipliers.Length ? starMultipliers[index] : 1f;
        }
    }
}
