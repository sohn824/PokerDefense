using System.Collections.Generic;
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
        static readonly Color PlaceableColor = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.15f, 0.45f);

        // 아트는 칸 높이의 85%로 두고 발끝을 칸 바닥에 붙임
        const float ArtScale = 0.85f;
        const float ArtBottomY = -0.46f;

        // 유닛의 별 표시 위치 오프셋
        const float StarTopY = 0.40f;

        // 선택 후 각 칸에 뜨는 행동 문구(이동/머지/교환)의 위치 오프셋
        const float ActionHintY = -0.12f;

        // 폭이 넓은 유닛이 슬롯 타일을 넘지 않도록 이 폭에 맞춰 기준 배율을 낮춤
        const float ArtMaxWidth = 0.9f;

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

        [SerializeField] SpriteRenderer highlight;

        [Tooltip("유닛 아트")]
        [SerializeField] SpriteRenderer art;

        [Tooltip("발사 순간의 총구 화염 이펙트")]
        [SerializeField] SpriteRenderer muzzle;

        [SerializeField] TextMeshPro starLabel;
        [SerializeField] Collider2D hitbox;

        // 선택 후 이 칸을 누르면 무슨 일이 일어나는지 미리 보여주는 문구
        TextMeshPro actionHint;

        UnitInstance current;

        // 현재 아트 스프라이트를 칸에 맞춘 기준 배율 (좁은 유닛은 ArtScale 그대로)
        float artFitScale = ArtScale;

        // 총구가 여럿인 유닛이 쓰는 muzzle 렌더러 풀
        // [0]은 씬에 있는 muzzle이고 나머지는 처음 쓸 때 복제해서 붙임
        readonly List<SpriteRenderer> muzzlePool = new List<SpriteRenderer>();

        // 13종 공용 화염. 유닛 전용 이펙트가 없으면 이것으로 떨어진다
        Sprite sharedMuzzleSprite;

        public int Index { get; private set; }

        void Awake()
        {
            starLabel.outlineColor = new Color32(0, 0, 0, 255);
            starLabel.outlineWidth = 0.25f;
            // 유닛 아트(sortingOrder 3)에 가리지 않도록 더 위로 올림
            starLabel.sortingOrder = 6;
            starLabel.transform.localPosition = new Vector3(0f, StarTopY, 0f);
            sharedMuzzleSprite = muzzle.sprite;
        }

        // BoardScreen.Awake가 Awake 순서와 무관하게 Refresh 전에 불러 준다
        // 프리팹이 없어 starLabel을 복제해 행동 문구용 라벨을 만든다 (폰트·머티리얼을 그대로 물려받음)
        void BuildActionHint()
        {
            actionHint = Instantiate(starLabel, transform);
            actionHint.name = "ActionHint";
            actionHint.transform.localPosition = new Vector3(0f, ActionHintY, 0f);
            actionHint.rectTransform.sizeDelta = new Vector2(1.8f, 0.5f);
            actionHint.fontSize = starLabel.fontSize * 0.9f;
            actionHint.color = new Color(1f, 0.96f, 0.72f);
            actionHint.outlineColor = new Color32(0, 0, 0, 255);
            actionHint.outlineWidth = 0.35f;
            actionHint.alignment = TextAlignmentOptions.Center;
            actionHint.sortingOrder = 8;
            actionHint.enableWordWrapping = false;
            actionHint.text = string.Empty;
            actionHint.gameObject.SetActive(false);
        }

        public void Bind(int index)
        {
            Index = index;

            if (actionHint == null)
            {
                BuildActionHint();
            }
        }

        // 빈 문자열이면 문구를 숨긴다
        public void SetActionHint(string text)
        {
            bool show = string.IsNullOrEmpty(text) == false;

            if (show)
            {
                actionHint.text = text;
            }

            actionHint.gameObject.SetActive(show);
        }

        public void Show(UnitInstance unit)
        {
            current = unit;

            if (unit == null)
            {
                art.enabled = false;
                HideMuzzles();
                starLabel.text = string.Empty;
                return;
            }

            if (unit.Definition.HasArt == false)
            {
                Debug.LogWarning($"{unit.Definition.DisplayName}에 4방향 아트가 없습니다.");
            }

            starLabel.text = new string('★', unit.Star);
            art.enabled = true;
            art.sprite = unit.Definition.ArtFor(AimDirection.Down);
            FitArtToCell();
            ApplyArtTransform(0f);
        }

        // 슬롯에 있는 유닛의 조준 방향과 발사 반동 갱신 메소드 (Update에서 매 프레임 호출)
        public void SetAim(AimDirection direction, float secondsSinceShot, float attacksPerSecond, int shotCount)
        {
            if (current == null)
            {
                return;
            }

            art.sprite = current.Definition.ArtFor(direction);
            FitArtToCell();

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
            ApplyMuzzle(direction, secondsSinceShot, shotCount, artScale / artFitScale);
        }

        void ApplyMuzzle(AimDirection direction, float secondsSinceShot, int shotCount, float artGrowth)
        {
            if (secondsSinceShot < 0f || secondsSinceShot >= MuzzleSeconds)
            {
                HideMuzzles();
                return;
            }

            UnitDefinition definition = current.Definition;
            Vector2[] muzzles = definition.MuzzlesFor(direction);

            if (muzzles == null || muzzles.Length == 0)
            {
                HideMuzzles();
                return;
            }

            float t = secondsSinceShot / MuzzleSeconds;

            // 등 뒤로 쏘면 몸에 가려야 하므로 order를 낮게, 나머지는 몸 앞에 나와야 하므로 높게
            int order = direction == AimDirection.Up ? 2 : 8;

            // 유닛 전용 이펙트가 없으면 13종 공용 화염으로 떨어진다
            Sprite flash = definition.AttackEffect != null ? definition.AttackEffect : sharedMuzzleSprite;

            if (definition.MuzzlesFireTogether)
            {
                // 적을 여럿 동시에 때리는 유닛은 총구가 다 같이 터진다
                for (int i = 0; i < muzzles.Length; i++)
                {
                    DrawMuzzleFlash(MuzzleAt(i), flash, Recoiled(Aimed(muzzles[i], direction), artGrowth), order, t);
                }

                HideMuzzlesFrom(muzzles.Length);
                return;
            }

            // 하나씩 쏜다. 어느 총구인지는 CombatContext가 센 발사 횟수로 정해
            // 0.06초 동안 흔들리지 않으면서 발사마다 바뀐다
            int pick = definition.MuzzleRandomOrder
                ? Scatter(shotCount) % muzzles.Length
                : shotCount % muzzles.Length;

            DrawMuzzleFlash(MuzzleAt(0), flash, Recoiled(Aimed(muzzles[pick], direction), artGrowth), order, t);
            HideMuzzlesFrom(1);
        }

        // 측면은 좌우를 대칭으로 묶으므로 왼쪽을 볼 때만 x를 뒤집는다
        static Vector2 Aimed(Vector2 muzzle, AimDirection direction)
        {
            return direction == AimDirection.Left ? new Vector2(-muzzle.x, muzzle.y) : muzzle;
        }

        // 발사 횟수를 흩뿌린다. 프레임이 아니라 발사에 물려 있어야 화염이 0.06초 동안 안 떨린다
        static int Scatter(int shotCount)
        {
            uint h = (uint)shotCount * 2654435761u;
            h ^= h >> 15;
            return (int)(h & 0x7fffffff);
        }

        // 반동으로 커진 아트에 맞춰 화염 위치를 밀어 준다
        static Vector2 Recoiled(Vector2 muzzleOffset, float artGrowth)
        {
            return new Vector2(muzzleOffset.x * artGrowth,
                               ArtBottomY + (muzzleOffset.y - ArtBottomY) * artGrowth);
        }

        // 총구 화염 그리기 (renderer를 켜고 스프라이트, 위치, 크기, 투명도 조정)
        static void DrawMuzzleFlash(SpriteRenderer renderer, Sprite flash, Vector2 offset, int sortingOrder, float t)
        {
            renderer.enabled = true;
            renderer.sprite = flash;
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
            HideMuzzlesFrom(0);
        }

        // muzzlePool에 있는 muzzle sprite를 from번째부터 끝까지 끈다
        void HideMuzzlesFrom(int from)
        {
            if (from == 0)
            {
                muzzle.enabled = false;
            }

            for (int i = 1; i < muzzlePool.Count; i++)
            {
                if (i >= from)
                {
                    muzzlePool[i].enabled = false;
                }
            }
        }

        // muzzlePool을 index번째까지 채워서 반환 (모자라는 경우 씬의 muzzle을 복제해 채움)
        SpriteRenderer MuzzleAt(int index)
        {
            if (muzzlePool.Count == 0)
            {
                muzzlePool.Add(muzzle);
            }

            while (muzzlePool.Count <= index)
            {
                SpriteRenderer clone = Instantiate(muzzle, muzzle.transform.parent);
                clone.name = muzzle.name + "_" + muzzlePool.Count;
                muzzlePool.Add(clone);
            }

            return muzzlePool[index];
        }

        // 아트 스프라이트가 타일 슬롯을 넘지 않도록 폭에 맞춰 기준 배율을 낮춤
        // 스프라이트가 바뀔 때(Show·SetAim)마다 호출 (좁은 유닛은 ArtScale 그대로)
        void FitArtToCell()
        {
            if (art.sprite == null)
            {
                artFitScale = ArtScale;
                return;
            }

            float spriteWidth = art.sprite.rect.width / art.sprite.pixelsPerUnit;
            artFitScale = Mathf.Min(ArtScale, ArtMaxWidth / spriteWidth);
        }

        // 호흡과 반동을 합친 크기로 아트를 키우고, 그때 쓴 스케일을 반환
        // 화염이 총구에 붙어있게 하기 위해서는 아트가 커진 배율(반환값 / artFitScale)만큼 화염도 밀어야 함
        float ApplyArtTransform(float punch)
        {
            // 호흡 상수를 time에 따라 Sin으로 흔들어서 아트가 살짝 커졌다 작아졌다 하도록 함
            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount;

            // 호흡과 반동(punch)을 합쳐서 스케일을 계산 (둘의 계수를 합쳐서 아트를 그만큼 키움)
            float scale = artFitScale * (1f + punch + breath);
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
