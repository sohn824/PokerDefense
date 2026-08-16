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

        [SerializeField] Color placeholderColor = Color.gray;

        [Header("★1 기준 스탯")]
        [SerializeField] float attackPower = 10f;
        [SerializeField] float attacksPerSecond = 1f;
        [SerializeField] float range = 2f;

        [Tooltip("★1 / ★2 / ★3 스탯 배수")]
        [SerializeField] float[] starMultipliers = { 1f, 2f, 4f };

        [Header("공격 패턴")]
        [SerializeField] AttackPattern attackPattern = AttackPattern.Rapid;

        [Tooltip("Multi 전용. 한 번에 때리는 최대 적 수")]
        [SerializeField] int multiTargets = 2;

        [Tooltip("Splash 전용. 착탄 지점 반경(월드 단위)")]
        [SerializeField] float splashRadius = 1f;

        [Tooltip("Pierce 전용. 타겟 뒤로 관통하는 트랙 길이(월드 단위)")]
        [SerializeField] float pierceLength = 2f;

        [Header("유닛 아트(위, 아래, 왼쪽, 오른쪽 네 방향 필요)")]
        [SerializeField] Sprite artDown;
        [SerializeField] Sprite artUp;
        [SerializeField] Sprite artLeft;
        [SerializeField] Sprite artRight;

        [Header("총구 화염 위치 (슬롯 로컬 좌표) 방향마다 총구를 전부 적는다")]
        [Tooltip("오른쪽을 볼 때의 총구 오프셋 배열. 왼쪽은 x를 뒤집어서 사용\n")]
        [SerializeField] Vector2[] muzzlesSide;

        [Tooltip("아래를 볼 때의 총구 오프셋 배열")]
        [SerializeField] Vector2[] muzzlesFacing;

        [Tooltip("위를 볼 때의 총구 오프셋 배열")]
        [SerializeField] Vector2[] muzzlesBack;

        [Tooltip("총구 전부가 동시에 불을 뿜는지 여부\n" +
                 "(적을 여럿 동시에 때리는 유닛만 true로 설정)")]
        [SerializeField] bool muzzlesFireTogether;

        [Tooltip("하나씩 쏠 때 총구가 순서대로 돌지 않고 랜덤으로 고를지 여부")]
        [SerializeField] bool muzzleRandomOrder;

        [Header("연출")]
        [Tooltip("발사 순간 총구에 뜨는 이펙트. 비우면 공용 이펙트를 쓴다")]
        [SerializeField] Sprite attackEffect;

        [Tooltip("적이 맞은 지점에 뜨는 이펙트. 비우면 공용 이펙트를 쓴다")]
        [SerializeField] Sprite hitEffect;

        [Tooltip("Pierce 전용. 관통한 트랙 구간을 따라 그리는 궤적. 비우면 안 그린다\n" +
                 "가로로 균일한 띠여야 한다 - 모서리에서 조각내 이어 붙인다")]
        [SerializeField] Sprite pierceTrailEffect;

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
         * 인자로 들어온 방향에서 불을 뿜을 총구 오프셋 배열 반환
         * 좌우 x축 오프셋은 대칭으로 묶는다
         */
        public Vector2[] MuzzlesFor(AimDirection direction)
        {
            switch (direction)
            {
                case AimDirection.Up:
                    return muzzlesBack;

                case AimDirection.Left:
                case AimDirection.Right:
                    return muzzlesSide;

                default:
                    return muzzlesFacing;
            }
        }

        // 총구가 여럿이면 공격 한번 모두 쏠지, 하나씩만 쏠지 여부
        public bool MuzzlesFireTogether => muzzlesFireTogether;

        // 하나씩 쏠 때 순서대로 돌지 무작위로 고를지
        public bool MuzzleRandomOrder => muzzleRandomOrder;

        // 유닛 전용 이펙트. 비어 있으면 뷰가 공용 이펙트로 떨어뜨린다
        public Sprite AttackEffect => attackEffect;
        public Sprite HitEffect => hitEffect;

        // 관통 궤적. 비어 있으면 안 그린다 (공용 대역이 없다 - 방향이 있는 유일한 이펙트다)
        public Sprite PierceTrailEffect => pierceTrailEffect;

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
