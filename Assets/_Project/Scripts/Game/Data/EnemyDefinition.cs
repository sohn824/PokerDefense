using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EnemyType
     *
     * 특수 능력은 없고 HP·이동속도·수량만 다름
     */
    public enum EnemyType
    {
        Normal,
        Swarm,
        Boss,

        // HP 적음 + 속도 빠름
        Runner,

        // HP 높음 + 속도 느림
        Tank,
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

        [SerializeField] Color placeholderColor = new Color(0.85f, 0.30f, 0.30f);

        [SerializeField] float maxHp = 100f;

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
