using UnityEngine;

namespace PokerDefense.Game
{
    public enum EnemyType
    {
        Normal,
        Swarm,
        Boss,
    }

    /**
     * EnemyDefinition
     *
     * 적 종류 하나의 정의
     */
    [CreateAssetMenu(menuName = "PokerDefense/Enemy Definition", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;

        [Tooltip("M6에서 스프라이트로 교체할 플레이스홀더 색")]
        [SerializeField] Color placeholderColor = new Color(0.85f, 0.30f, 0.30f);

        [SerializeField] float maxHp = 100f;

        [Tooltip("초당 이동 거리. 트랙 길이와 같은 월드 단위다")]
        [SerializeField] float moveSpeed = 2f;

        [SerializeField] EnemyType type = EnemyType.Normal;

        public string Id => id;
        public string DisplayName => displayName;
        public Color PlaceholderColor => placeholderColor;
        public float MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public EnemyType Type => type;
    }
}
