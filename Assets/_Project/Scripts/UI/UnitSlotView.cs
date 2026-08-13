using PokerDefense.Game;
using TMPro;
using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * UnitSlotView
     *
     * 보드 슬롯 한 칸의 월드 스페이스 표현
     *
     * 칸에는 **무엇인지와 몇 성인지만** 둔다. 수치는 유닛을 고르면 상세 패널이 보여준다 (DESIGN §10.3)
     * 칸이 1080 기준 145px이라 네 가지를 밀어 넣으면 아트가 들어갈 자리가 없어진다.
     * 칸 자체는 더 못 키운다 - 트랙이 이미 화면 가로를 거의 다 쓴다
     *
     * **이름은 아트가 없는 동안의 대역이다** (DESIGN §10.3). 아트가 들어온 유닛은 이름 라벨을 끈다
     * M11이 13종을 한 번에 채우지 않으므로 아트가 있는 칸과 색 사각형인 칸이 섞인다
     */
    public sealed class UnitSlotView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.24f, 0.25f, 0.29f);
        static readonly Color PlaceableColor = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.15f, 0.45f);

        // 아트는 칸 높이의 85%로 두고 발끝을 칸 바닥에 붙인다.
        // 남는 아래쪽 띠가 ★ 자리다 - 칸이 134px뿐이라 모서리로는 못 뺀다
        const float ArtScale = 0.85f;
        const float ArtBottomY = -0.46f;

        // 발사 반동. 공격속도가 초당 0.4~6.75회로 갈리므로(성급 배수 포함)
        // 길이를 발사 간격에 물리되 상한을 둔다. 안 그러면 최속 유닛이 복귀를 못 하고 계속 떤다
        const float RecoilMaxSeconds = 0.10f;
        const float RecoilIntervalRatio = 0.45f;
        const float RecoilAmount = 0.06f;

        // 느린 유닛은 발사 사이가 2.5초라 반동만으로는 멈춰 보인다. 호흡은 항상 돈다
        const float BreathAmount = 0.015f;
        const float BreathSpeed = 2.9f;

        // 총구 화염. 최속 유닛이 초당 6.75발이라 다음 발과 겹치지 않으려면 짧아야 한다
        const float MuzzleSeconds = 0.06f;
        const float MuzzleScale = 0.40f;

        // 총구 위치를 유닛마다 잡으면 13종 x 4방향 = 52개 좌표가 된다.
        // 124px에서는 그 차이가 안 보이므로 가슴 높이를 기준으로 방향만 반영한다
        //
        // 좌우는 팔을 뻗으므로 옆으로 밀면 되지만, 정면·후면은 총이 카메라 축으로 겨눠져
        // **총구가 그대로 가슴 높이에 있다.** 방향 벡터를 그대로 밀면 발밑이나 등 한가운데에 뜬다
        // 총구 위치는 유닛마다 다르므로 UnitDefinition.MuzzleOffset이 들고 있다.
        // 후면 보정만 공용이다 - 몸에 가려 안 보이므로 머리 윤곽 밖으로 비치게 조금 더 올린다
        const float MuzzleBackExtraY = 0.04f;

        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer highlight;

        [Tooltip("유닛 아트. 아트가 없는 유닛은 background 색으로 떨어진다")]
        [SerializeField] SpriteRenderer art;

        [Tooltip("발사 순간의 총구 화염. 아트가 있는 유닛만 쓴다")]
        [SerializeField] SpriteRenderer muzzle;

        [Tooltip("아트가 없는 동안의 대역. 아트가 들어오면 끈다")]
        [SerializeField] TextMeshPro nameLabel;

        [SerializeField] TextMeshPro starLabel;
        [SerializeField] Collider2D hitbox;

        UnitInstance current;

        public int Index { get; private set; }

        void Awake()
        {
            // 아트 위에 겹치는 자리라 외곽선이 없으면 부츠에 묻힌다
            starLabel.outlineColor = new Color32(0, 0, 0, 255);
            starLabel.outlineWidth = 0.25f;
        }

        public void Bind(int index)
        {
            Index = index;
        }

        public void Show(UnitInstance unit)
        {
            current = unit;

            if (unit == null)
            {
                background.color = EmptyColor;
                art.enabled = false;
                muzzle.enabled = false;
                nameLabel.text = string.Empty;
                starLabel.text = string.Empty;
                return;
            }

            starLabel.text = new string('★', unit.Star);

            if (unit.Definition.HasArt)
            {
                // 아트가 칸을 채우므로 배경은 빈 칸과 같은 타일로 두고 이름 라벨은 물러난다
                background.color = EmptyColor;
                art.enabled = true;
                art.sprite = unit.Definition.ArtFor(AimDirection.Down);
                nameLabel.text = string.Empty;
                ApplyArtTransform(0f);
                return;
            }

            background.color = unit.Definition.PlaceholderColor;
            art.enabled = false;
            muzzle.enabled = false;

            // 띄어쓰기를 개행으로 바꿔 단어 단위로만 끊는다
            nameLabel.text = unit.Definition.DisplayName.Replace(' ', '\n');
        }

        /**
         * 조준 방향과 발사 반동을 갱신한다
         *
         * 매 프레임 BoardScreen이 부른다. 전투가 없으면 secondsSinceShot에 -1을 넘긴다
         */
        public void SetAim(AimDirection direction, float secondsSinceShot, float attacksPerSecond)
        {
            if (current == null || current.Definition.HasArt == false)
            {
                return;
            }

            art.sprite = current.Definition.ArtFor(direction);

            float punch = 0f;

            if (secondsSinceShot >= 0f && attacksPerSecond > 0f)
            {
                float duration = Mathf.Min(RecoilMaxSeconds, RecoilIntervalRatio / attacksPerSecond);

                if (secondsSinceShot < duration)
                {
                    // 쏘는 순간 튀고 되돌아온다. 총구가 카메라를 향하므로 스케일이 가장 잘 읽힌다
                    float remain = 1f - (secondsSinceShot / duration);
                    punch = RecoilAmount * remain * remain;
                }
            }

            ApplyArtTransform(punch);
            ApplyMuzzle(direction, secondsSinceShot);
        }

        void ApplyMuzzle(AimDirection direction, float secondsSinceShot)
        {
            bool firing = secondsSinceShot >= 0f && secondsSinceShot < MuzzleSeconds;

            muzzle.enabled = firing;

            if (firing == false)
            {
                return;
            }

            Vector2 gun = current.Definition.MuzzleOffset;
            Vector2 offset;

            switch (direction)
            {
                case AimDirection.Up:
                    // 등 뒤로 쏘므로 몸에 가려야 자연스럽다
                    offset = new Vector2(0f, gun.y + MuzzleBackExtraY);
                    muzzle.sortingOrder = 2;
                    break;

                case AimDirection.Left:
                    offset = new Vector2(-gun.x, gun.y);
                    muzzle.sortingOrder = 8;
                    break;

                case AimDirection.Right:
                    offset = new Vector2(gun.x, gun.y);
                    muzzle.sortingOrder = 8;
                    break;

                default:
                    // 카메라 쪽으로 쏘므로 몸 앞에 온다
                    offset = new Vector2(0f, gun.y);
                    muzzle.sortingOrder = 8;
                    break;
            }

            muzzle.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            // 터졌다가 사그라든다. 방사 대칭이라 방향에 따라 돌릴 필요가 없다
            float t = secondsSinceShot / MuzzleSeconds;
            float scale = MuzzleScale * (0.7f + 0.5f * t);
            muzzle.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = muzzle.color;
            c.a = 1f - t * t;
            muzzle.color = c;
        }

        void ApplyArtTransform(float punch)
        {
            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount;
            float scale = ArtScale * (1f + punch + breath);

            // 피벗이 발끝이라 커져도 접지선이 안 흔들린다
            art.transform.localPosition = new Vector3(0f, ArtBottomY, 0f);
            art.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // 머지 상대로 고른 칸은 노랑, 그냥 놓을 수 있는 칸은 흰색
        public void SetHighlight(bool on, bool selected)
        {
            highlight.enabled = on;
            highlight.color = selected ? SelectedColor : PlaceableColor;
        }

        // 누를 수 없는 칸은 hitbox를 꺼서 Physics2D 검사에서 아예 빠지게 함
        public void SetInteractable(bool interactable)
        {
            hitbox.enabled = interactable;
        }
    }
}
