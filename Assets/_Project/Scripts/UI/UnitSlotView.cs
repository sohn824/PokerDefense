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

        // 두 총구가 동시에 터지는 유닛에만 필요하다. 씬의 슬롯 15칸에 손으로 하나씩 다는 대신
        // **처음 쓸 때** 복제한다 - 한 칸을 빠뜨릴 일이 없고, Awake에서 만들지 않으므로
        // 다른 컴포넌트의 Awake가 먼저 도는 순서 문제에도 안 걸린다
        SpriteRenderer muzzleSecond;

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
                HideMuzzles();
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
            HideMuzzles();

            // 띄어쓰기를 개행으로 바꿔 단어 단위로만 끊는다
            nameLabel.text = unit.Definition.DisplayName.Replace(' ', '\n');
        }

        // 슬롯에 있는 유닛의 조준 방향과 발사 반동을 갱신한다
        public void SetAim(AimDirection direction, float secondsSinceShot, float attacksPerSecond, int shotCount)
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
                    // 쏘는 순간 튀고 되돌아온다
                    float remain = 1f - (secondsSinceShot / duration);
                    punch = RecoilAmount * remain * remain;
                }
            }

            // 반동으로 커진 만큼 화염도 밀어야 총구에 붙어 있는다
            float artScale = ApplyArtTransform(punch);

            ApplyMuzzle(direction, secondsSinceShot, shotCount, artScale / ArtScale);
        }

        void ApplyMuzzle(AimDirection direction, float secondsSinceShot, int shotCount, float artGrowth)
        {
            if (secondsSinceShot < 0f || secondsSinceShot >= MuzzleSeconds)
            {
                HideMuzzles();
                return;
            }

            UnitDefinition definition = current.Definition;
            float t = secondsSinceShot / MuzzleSeconds;

            // 등 뒤로 쏘면 몸에 가려야 하므로 order를 낮게 주고, 나머지는 몸 앞에 나와야 하므로 order를 높게 줌
            int order = direction == AimDirection.Up ? 2 : 8;

            if (definition.HasSecondMuzzle && definition.MuzzlesFireTogether)
            {
                // 적 둘을 동시에 때리는 유닛은 두 총구가 함께 터져야 함
                Flash(muzzle, Recoiled(MuzzleOffsetFor(definition, direction, false), artGrowth), order, t);
                Flash(SecondMuzzle(), Recoiled(MuzzleOffsetFor(definition, direction, true), artGrowth), order, t);
                return;
            }

            // 나머지는 한 발씩 번갈아 사격
            bool otherHand = definition.HasSecondMuzzle && (shotCount & 1) == 0;

            Flash(muzzle, Recoiled(MuzzleOffsetFor(definition, direction, otherHand), artGrowth), order, t);

            if (muzzleSecond != null)
            {
                muzzleSecond.enabled = false;
            }
        }

        /**
         * 반동으로 커진 아트에 맞춰 화염 위치를 밀어 준다
         *
         * 아트는 발끝(ArtBottomY)을 피벗으로 커지는데 화염 좌표는 슬롯 로컬 고정이라,
         * 그냥 두면 **화염이 보이는 0.06초가 정확히 반동이 가장 큰 구간이라** 늘 어긋나 보인다.
         * 총구가 높을수록 더 벌어진다 - 마크스맨(y 0.29)에서 7.6px이었다
         */
        static Vector2 Recoiled(Vector2 offset, float artGrowth)
        {
            return new Vector2(offset.x * artGrowth,
                               ArtBottomY + (offset.y - ArtBottomY) * artGrowth);
        }

        static Vector2 MuzzleOffsetFor(UnitDefinition definition, AimDirection direction, bool second)
        {
            Vector2 side = definition.MuzzleOffset;

            if (direction == AimDirection.Left || direction == AimDirection.Right)
            {
                // 측면은 좌우 대칭으로 묶는다 - 실측 차이가 1.7~3.6px이라 나눌 값어치가 없다
                Vector2 gun = second ? definition.MuzzleOffsetSecond : side;
                return new Vector2(direction == AimDirection.Left ? -gun.x : gun.x, gun.y);
            }

            bool back = direction == AimDirection.Up;
            Vector2 facing = back ? definition.MuzzleOffsetBack : definition.MuzzleOffsetFacing;

            if (facing == Vector2.zero)
            {
                // 아직 안 잰 유닛은 예전 규칙으로 떨어진다 - 몸 중앙, 후면만 조금 올림
                float y = side.y + (back ? MuzzleBackExtraY : 0f);
                facing = definition.HasSecondMuzzle ? new Vector2(side.x, y) : new Vector2(0f, y);
            }

            // 무기가 둘이면 좌우 대칭이라 부호를 갈라 쓰고, 하나면 잰 자리를 그대로 쓴다
            return definition.HasSecondMuzzle
                ? new Vector2(second ? -Mathf.Abs(facing.x) : Mathf.Abs(facing.x), facing.y)
                : facing;
        }

        static void Flash(SpriteRenderer renderer, Vector2 offset, int sortingOrder, float t)
        {
            renderer.enabled = true;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            float scale = MuzzleScale * (0.7f + 0.5f * t);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = renderer.color;
            c.a = 1f - t * t;
            renderer.color = c;
        }

        void HideMuzzles()
        {
            muzzle.enabled = false;

            if (muzzleSecond != null)
            {
                muzzleSecond.enabled = false;
            }
        }

        // 쌍권총류 유닛은 muzzle 위치를 복제해서 씀 (쌍권총류가 아닌 유닛은 호출 안함)
        SpriteRenderer SecondMuzzle()
        {
            if (muzzleSecond == null)
            {
                muzzleSecond = Instantiate(muzzle, muzzle.transform.parent);
                muzzleSecond.name = muzzle.name + "_Second";
            }

            return muzzleSecond;
        }

        // 적용한 스케일을 돌려준다 - 화염이 같은 값으로 따라와야 총구에 붙어 있는다
        float ApplyArtTransform(float punch)
        {
            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount;
            float scale = ArtScale * (1f + punch + breath);

            art.transform.localPosition = new Vector3(0f, ArtBottomY, 0f);
            art.transform.localScale = new Vector3(scale, scale, 1f);

            return scale;
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
