using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * AttackPattern
     *
     * 유닛 13종이 나눠 쓰는 공격 방식 5종
     *
     */
    public enum AttackPattern
    {
        // 빠른 단일 공격
        Rapid,

        // 느리고 강한 단일 공격
        Heavy,

        // 다중 공격 (사거리 안에서 앞선 적 우선으로)
        Multi,

        // 착탄 지점 주변 스플래쉬
        Splash,

        // 타겟 뒤로 관통
        Pierce,
    }

    /**
     * AimDirection
     *
     * 유닛이 겨누는 방향
     *
     * 적은 트랙을 360도로 돌지만 유닛은 회전하지 않는다 (DESIGN §5.3)
     * 그래서 방향별 스프라이트를 바꿔 끼워 표현한다
     */
    public enum AimDirection
    {
        Down,
        Up,
        Left,
        Right,
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

        [Header("★1 기준 스탯")]
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

        [Header("M11 아트. 네 방향을 다 채워야 아트로 그린다")]
        [SerializeField] Sprite artDown;
        [SerializeField] Sprite artUp;
        [SerializeField] Sprite artLeft;
        [SerializeField] Sprite artRight;

        [Tooltip("총구 화염 위치. x = 좌우로 미는 거리, y = 총구 높이 (슬롯 로컬 좌표)\n")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.24f, 0.26f);

        [Tooltip("쌍무기 전용. 두 번째 총구 위치. (0,0)이면 총이 하나로 그려진다\n" +
                 "측면 스프라이트에서 아래쪽 총을 재서 넣는다")]
        [SerializeField] Vector2 muzzleOffsetSecond;

        [Tooltip("두 총구가 동시에 불을 뿜는지 여부\n false면 한 발씩 번갈아 쏜다\n" +
                 "적을 여럿 동시에 때리는 유닛만 켠다 (AttackPattern.Multi)")]
        [SerializeField] bool muzzlesFireTogether;

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

        /**
         * 총구 화염을 띄울 위치
         *
         * 좌우는 대칭으로 묶는다
         */
        public Vector2 MuzzleOffset => muzzleOffset;

        /**
         * 두 번째 총구. 쌍권총처럼 무기를 둘 든 유닛만 채움
         * 채워져 있으면 발사할 때마다 두 총구가 번갈아 muzzle 이펙트가 나옴
         */
        public Vector2 MuzzleOffsetSecond => muzzleOffsetSecond;

        public bool HasSecondMuzzle => muzzleOffsetSecond != Vector2.zero;

        public bool MuzzlesFireTogether => muzzlesFireTogether;

        // 한 방향이라도 비면 플레이스홀더 색 사각형으로 폴백 (폴백하지 않으려면 아트 리소스 필수)
        public bool HasArt => artDown != null && artUp != null && artLeft != null && artRight != null;

        public Sprite ArtFor(AimDirection direction)
        {
            switch (direction)
            {
                case AimDirection.Up:
                    return artUp;

                case AimDirection.Left:
                    return artLeft;

                case AimDirection.Right:
                    return artRight;

                default:
                    return artDown;
            }
        }

        public float MultiplierFor(int star)
        {
            int index = Mathf.Clamp(star, 1, MaxStar) - 1;
            return index < starMultipliers.Length ? starMultipliers[index] : 1f;
        }
    }
}
