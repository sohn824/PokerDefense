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
     * 유닛 아트, 총구 화염, 별 표시
     */
    public sealed class UnitSlotView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.24f, 0.25f, 0.29f);
        static readonly Color PlaceableColor = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.15f, 0.45f);

        // 아트는 칸 높이의 85%로 두고 발끝을 칸 바닥에 붙임
        const float ArtScale = 0.85f;
        const float ArtBottomY = -0.46f;

        // 발사 반동 연출용 상수
        const float RecoilMaxSeconds = 0.10f;
        const float RecoilIntervalRatio = 0.45f;
        const float RecoilAmount = 0.06f;

        // 호흡 연출용 상수
        const float BreathAmount = 0.015f;
        const float BreathSpeed = 2.9f;

        // 총구 화염 연출용 상수
        const float MuzzleSeconds = 0.06f;
        const float MuzzleScale = 0.40f;
        const float MuzzleBackExtraY = 0.04f;

        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer highlight;

        [Tooltip("유닛 아트")]
        [SerializeField] SpriteRenderer art;

        [Tooltip("발사 순간의 총구 화염 이펙트")]
        [SerializeField] SpriteRenderer muzzle;

        [Tooltip("아트가 없는 동안의 대역 (이름 표시)")]
        [SerializeField] TextMeshPro nameLabel;

        [SerializeField] TextMeshPro starLabel;
        [SerializeField] Collider2D hitbox;

        UnitInstance current;

        // 쌍권총류 유닛이 사용할 복제 muzzle
        SpriteRenderer muzzleSecond;

        public int Index { get; private set; }

        void Awake()
        {
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

            nameLabel.text = unit.Definition.DisplayName.Replace(' ', '\n');
        }

        // 슬롯에 있는 유닛의 조준 방향과 발사 반동 갱신 메소드 (Update에서 매 프레임 호출)
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

            // ApplyArtTransform은 호흡 상수와 반동(punch)를 반영해
            // 아트 scale을 계산하고 그 때 쓴 scale을 반환함
            float artScale = ApplyArtTransform(punch);
            // 반동으로 커진 만큼 muzzle 이펙트도 밀어줌 
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
                DrawMuzzleFlash(muzzle, Recoiled(MuzzleOffsetFor(definition, direction, false), artGrowth), order, t);
                DrawMuzzleFlash(SecondMuzzle(), Recoiled(MuzzleOffsetFor(definition, direction, true), artGrowth), order, t);
                return;
            }

            // 나머지는 한 발씩 번갈아 사격
            bool otherHand = definition.HasSecondMuzzle && (shotCount & 1) == 0;

            DrawMuzzleFlash(muzzle, Recoiled(MuzzleOffsetFor(definition, direction, otherHand), artGrowth), order, t);

            if (muzzleSecond != null)
            {
                muzzleSecond.enabled = false;
            }
        }

        
        // 반동으로 커진 아트에 맞춰 화염 위치를 밀어 준다
        static Vector2 Recoiled(Vector2 muzzleOffset, float artGrowth)
        {
            return new Vector2(muzzleOffset.x * artGrowth,
                               ArtBottomY + (muzzleOffset.y - ArtBottomY) * artGrowth);
        }

        // 총구 위치를 유닛 정의와 조준 방향, 쌍권총 여부에 따라 계산
        static Vector2 MuzzleOffsetFor(UnitDefinition definition, AimDirection direction, bool second)
        {
            Vector2 side = definition.MuzzleOffset;

            if (direction == AimDirection.Left || direction == AimDirection.Right)
            {
                // 측면은 좌우 대칭으로 묶는다
                Vector2 gun = second ? definition.MuzzleOffsetSecond : side;
                return new Vector2(direction == AimDirection.Left ? -gun.x : gun.x, gun.y);
            }

            bool back = direction == AimDirection.Up;
            // 유닛 정의를 참고해서 현재 유닛이 정면, 후면을 보고있는지에 따라 총구 위치를 가져옴
            Vector2 facing = back ? definition.MuzzleOffsetBack : definition.MuzzleOffsetFacing;

            // 아직 유닛 정의에 총구 위치가 없는 경우 임시 좌표 사용
            if (facing == Vector2.zero)
            {
                float y = side.y + (back ? MuzzleBackExtraY : 0f);
                facing = definition.HasSecondMuzzle ? new Vector2(side.x, y) : new Vector2(0f, y);
            }

            return definition.HasSecondMuzzle
                ? new Vector2(second ? -Mathf.Abs(facing.x) : Mathf.Abs(facing.x), facing.y)
                : facing;
        }

        // 총구 화염 그리기 (muzzle renderer를 켜고 위치, 크기, 투명도 조정)
        static void DrawMuzzleFlash(SpriteRenderer renderer, Vector2 offset, int sortingOrder, float t)
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

        // 호흡과 반동을 합친 크기로 아트를 키우고, 그때 쓴 스케일을 반환
        // 화염이 총구에 붙어있게 하기 위해서는 아트가 커진 배율(반환값 / ArtScale)만큼 화염도 밀어야 함
        float ApplyArtTransform(float punch)
        {
            // 호흡 상수를 time에 따라 Sin으로 흔들어서 아트가 살짝 커졌다 작아졌다 하도록 함
            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount;

            // 호흡과 반동(punch)을 합쳐서 스케일을 계산 (둘의 계수를 합쳐서 아트를 그만큼 키움)
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
