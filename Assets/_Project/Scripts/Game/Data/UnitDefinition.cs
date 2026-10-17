using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * AttackPattern
     *
     * 유닛 13종이 나눠 쓰는 공격 방식 5종 (CombatContext.Fire가 AttackPattern에 따라 때릴 적을 고름)
     */
    public enum AttackPattern
    {
        // 단일 공격 - 공격 속도가 빠른 유닛용 (동작은 Heavy와 같고 스탯·발사음으로만 구분)
        Rapid,

        // 단일 공격 - 느리고 강한 유닛용 (동작은 Rapid와 같고 스탯·발사음으로만 구분)
        Heavy,

        // 다중 공격 - 첫 타겟 근처(MultiSpread 안)에 있는 적을 최대 MultiTargets기까지 함께 때림
        Multi,

        // 범위 공격 - 타겟이 맞은 자리에서 SplashRadius 안에 있는 적을 모두 때림
        Splash,

        // 관통 공격 - 타겟과, 그 뒤로 PierceLength 거리 안에서 따라오는 적을 모두 때림
        Pierce,
    }

    /**
     * AimDirection
     *
     * 유닛이 겨누는 방향
     * 적이 걷는 방향에도 쓰며, 아트 4방향 중 무엇을 그릴지 고르는 데 사용
     */
    public enum AimDirection
    {
        // 화면 아래 (정면, 전투 밖 기본값)
        Down,

        // 화면 위 (뒷모습)
        Up,

        // 화면 왼쪽
        Left,

        // 화면 오른쪽
        Right,
    }

    /**
     * UnitDefinition
     *
     * 유닛 종류 하나의 데이터를 담는 ScriptableObject (에셋 파일 하나 = 유닛 종류 하나, Data/Units에 13개)
     * 담는 값 - ★1 스탯, 성급 배수, 공격 패턴, 아트, 연출
     * 족보 13종에 하나씩 연결됨 (연결은 HandUnitTable)
     * 보드 위 유닛 1기는 UnitInstance가 이 에셋과 성급을 묶어 표현
     */
    [CreateAssetMenu(menuName = "PokerDefense/Unit Definition", fileName = "Unit_")]
    public sealed class UnitDefinition : ScriptableObject
    {
        // 최대 성급 (★3)
        public const int MaxStar = 3;

        // 고유 id
        [SerializeField] string id;
        [SerializeField] string displayName;

        [Header("★1 기준 스탯")]
        [SerializeField] float attackPower = 10f;
        [SerializeField] float attacksPerSecond = 1f;
        [SerializeField] float range = 2f;

        [Tooltip("★1 / ★2 / ★3 스탯 배수")]
        [SerializeField] float[] starMultipliers = { 1f, 2f, 4f };

        [Header("공격 패턴")]
        [SerializeField] AttackPattern attackPattern = AttackPattern.Rapid;

        [Tooltip("Multi 전용\n한 번에 때리는 최대 적 수")]
        [SerializeField] int multiTargets = 2;

        [Tooltip("Multi 전용\n첫 타겟에서 이 거리 안에 있는 적만 함께 때림")]
        [SerializeField] float multiSpread = 1f;

        [Tooltip("Splash 전용\n착탄 지점 반경")]
        [SerializeField] float splashRadius = 1f;

        [Tooltip("Pierce 전용\n타겟 뒤로 관통하는 트랙 길이")]
        [SerializeField] float pierceLength = 2f;

        [Header("유닛 아트 (아래·위·왼쪽·오른쪽 4방향 모두 필요)")]
        [SerializeField] Sprite artDown;
        [SerializeField] Sprite artUp;
        [SerializeField] Sprite artLeft;
        [SerializeField] Sprite artRight;

        [Header("총구 화염 위치 (슬롯 로컬 좌표, 방향마다 총구를 전부 적음)")]
        [Tooltip("오른쪽을 볼 때의 총구 오프셋 배열\n왼쪽은 x를 뒤집어서 사용")]
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
        [Tooltip("발사 순간 총구에 뜨는 이펙트")]
        [SerializeField] Sprite attackEffect;

        [Tooltip("적이 맞은 지점에 뜨는 이펙트")]
        [SerializeField] Sprite hitEffect;

        [Tooltip("Splash 전용\n착탄 지점에 splashRadius 크기로 그리는 폭발")]
        [SerializeField] Sprite splashEffect;

        [Tooltip("Pierce 전용\n관통한 트랙 구간을 따라 그리는 궤적")]
        [SerializeField] Sprite pierceTrailEffect;

        public string Id => id;
        public string DisplayName => displayName;
        public float AttackPower => attackPower;
        public float AttacksPerSecond => attacksPerSecond;
        public float Range => range;
        public AttackPattern Pattern => attackPattern;
        public int MultiTargets => multiTargets;
        public float MultiSpread => multiSpread;
        public float SplashRadius => splashRadius;
        public float PierceLength => pierceLength;

        // direction 방향을 볼 때 불을 뿜을 총구 오프셋 배열
        // Left·Right는 같은 muzzlesSide를 돌려줌
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

        // 총구가 여럿일 때 공격 1번에 모든 총구가 함께 쏘는지 여부 (false면 총구 하나씩)
        public bool MuzzlesFireTogether => muzzlesFireTogether;

        // 총구를 하나씩 쏠 때 무작위로 고르는지 여부 (false면 순서대로 돌아감)
        public bool MuzzleRandomOrder => muzzleRandomOrder;

        // 발사 순간 총구 이펙트, 적이 맞은 지점 이펙트
        public Sprite AttackEffect => attackEffect;
        public Sprite HitEffect => hitEffect;

        // Splash 패턴 전용 폭발 이펙트
        public Sprite SplashEffect => splashEffect;

        // Pierce 패턴 전용 관통 궤적 이펙트
        public Sprite PierceTrailEffect => pierceTrailEffect;

        // 4방향 아트가 모두 있으면 true
        public bool HasArt => artDown != null && artUp != null && artLeft != null && artRight != null;

        // direction 방향 아트 (Down이 기본)
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

        // star 성급에 곱할 스탯 배수 (starMultipliers에서 읽음)
        // star가 1~3 밖이거나 배열에 그 칸이 없으면 1을 돌려줌
        public float MultiplierFor(int star)
        {
            if (star < 1 || star > MaxStar || star > starMultipliers.Length)
            {
                return 1f;
            }

            return starMultipliers[star - 1];
        }
    }
}
