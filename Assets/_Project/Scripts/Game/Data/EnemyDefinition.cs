using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * EnemyType
     *
     * 적 분류 5종 (UI 이름·설명과 결과 화면 집계에 씀)
     * 특수 능력은 없고 HP·이동속도·수량으로만 성격이 갈림, 단 Boss는 남으면 라이프를 상한만큼 깎음
     * 값이 에셋에 정수로 저장되므로 새로운 타입은 맨 뒤에 추가해야 함
     */
    public enum EnemyType
    {
        // 기본 적
        Normal,

        // HP 낮음 · 수가 많음
        Swarm,

        // HP 매우 높음 (하나라도 남으면 라이프가 상한만큼 깎임)
        Boss,

        // HP 낮음 · 속도 빠름
        Runner,

        // HP 높음 · 속도 느림
        Tank,
    }

    /**
     * EnemyDefinition
     *
     * 적 종류 하나의 데이터를 담는 ScriptableObject (에셋 파일 하나 = 적 종류 하나, Data/Enemies에 7개)
     * 담는 값 - HP, 이동 속도, 분류, 아트, 표시 크기
     * 같은 종류의 적들이 이 에셋 하나를 함께 씀
     * 적 1기마다 달라지는 값(현재 HP, 위치)은 EnemyInstance가 따로 가짐
     */
    [CreateAssetMenu(menuName = "PokerDefense/Enemy Definition", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        // 고유 이름 (CombatScreen이 적 오브젝트 이름에 붙임)
        [SerializeField] string id;
        [SerializeField] string displayName;

        // 아트가 없을 때 대신 그리는 사각형 색
        [SerializeField] Color placeholderColor = new Color(0.85f, 0.30f, 0.30f);

        // 최대 HP (나올 때 이 값으로 시작)
        [SerializeField] float maxHp = 100f;

        // 이동 속도 (초당 트랙 거리, 한 칸 = 1)
        [SerializeField] float moveSpeed = 2f;

        [SerializeField] EnemyType type = EnemyType.Normal;

        // 4방향 아트 (걷는 방향은 GridBoard.TrackDirection이 고름)
        [SerializeField] Sprite artDown;
        [SerializeField] Sprite artUp;
        [SerializeField] Sprite artLeft;
        [SerializeField] Sprite artRight;

        // 몸통 표시 배율
        // CombatScreen의 기본 크기 BodyScale(0.5)에 곱함
        [SerializeField] float visualScale = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public Color PlaceholderColor => placeholderColor;
        public float MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public EnemyType Type => type;

        // 4방향 아트가 모두 있어야 true
        public bool HasArt => artDown != null && artUp != null && artLeft != null && artRight != null;
        public float VisualScale => visualScale;

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
    }
}
