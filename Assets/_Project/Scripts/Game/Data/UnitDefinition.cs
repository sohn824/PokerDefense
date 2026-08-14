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

        [Tooltip("정면(아래)을 볼 때의 총구 위치. 비워 두면 예전 규칙(몸 중앙 높이)으로 떨어진다\n" +
                 "무기가 하나면 잰 자리를 부호 그대로, 둘이면 좌우 대칭이므로 양수로 넣는다")]
        [SerializeField] Vector2 muzzleOffsetFacing;

        [Tooltip("후면(위)을 볼 때의 총구 위치. 비워 두면 예전 규칙으로 떨어진다\n" +
                 "총을 어깨에 메는 유닛은 정면과 자리가 크게 다르므로 따로 잰다")]
        [SerializeField] Vector2 muzzleOffsetBack;

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

        /**
         * 정면·후면에서의 총구 위치. 비어 있으면 예전 규칙으로 떨어진다
         *
         * **좌표를 방향마다 두는 대신 셋으로 묶는다** - 측면 / 정면 / 후면.
         * 좌우는 실측 차이가 1.7~3.6px이라 대칭으로 묶어도 되지만,
         * 정면·후면은 무기가 아예 다른 자리에 온다. 마크스맨은 총을 어깨에 메서
         * 후면 총구가 등 한가운데가 아니라 왼쪽 어깨 위에 있고(21.6px), 스카우트도
         * 후면 권총이 오른쪽으로 벗어나 있었다(23px). 하나로 묶으면 이게 안 잡힌다
         */
        public Vector2 MuzzleOffsetFacing => muzzleOffsetFacing;

        public Vector2 MuzzleOffsetBack => muzzleOffsetBack;

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
