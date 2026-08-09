using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * AttackPattern
     *
     * 유닛 13종이 나눠 쓰는 공격 방식 5종 (DESIGN §10.1)
     *
     * Rapid와 Heavy는 둘 다 단일 타겟이고 스탯 프로필로만 갈린다 - 빠르고 약한가, 느리고 강한가
     * 대상을 고르는 방식이 실제로 다른 것은 Multi/Splash/Pierce 셋이다
     */
    public enum AttackPattern
    {
        // 빠른 단일
        Rapid,

        // 느리고 강한 단일
        Heavy,

        // 사거리 안에서 앞선 순으로 여러 적을 동시에
        Multi,

        // 착탄 지점 주변까지
        Splash,

        // 타겟 뒤로 트랙을 따라 관통
        Pierce,
    }

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

        [Header("공격 패턴. 성급 배수를 곱하지 않는다 (사거리와 같은 이유)")]
        [SerializeField] AttackPattern attackPattern = AttackPattern.Rapid;

        [Tooltip("Multi 전용. 한 번에 때리는 최대 적 수")]
        [SerializeField] int multiTargets = 2;

        [Tooltip("Splash 전용. 착탄 지점 반경(월드 단위). 유닛 사거리 밖의 적도 휘말린다")]
        [SerializeField] float splashRadius = 1f;

        [Tooltip("Pierce 전용. 타겟 뒤로 관통하는 트랙 길이(월드 단위)")]
        [SerializeField] float pierceLength = 2f;

        public string Id => id;
        public string DisplayName => displayName;
        public Color PlaceholderColor => placeholderColor;
        public float AttackPower => attackPower;
        public float AttacksPerSecond => attacksPerSecond;
        public float Range => range;
        public AttackPattern Pattern => attackPattern;
        public int MultiTargets => multiTargets;
        public float SplashRadius => splashRadius;
        public float PierceLength => pierceLength;

        public float MultiplierFor(int star)
        {
            int index = Mathf.Clamp(star, 1, MaxStar) - 1;
            return index < starMultipliers.Length ? starMultipliers[index] : 1f;
        }
    }
}
