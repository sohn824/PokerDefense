using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ArenaPanelGraphic
     *
     * 모서리를 비스듬히 깎은 패널을 텍스처 없이 메시로 그리는 UI 컴포넌트 (해상도에 영향 받지 않음)
     */
    [AddComponentMenu("UI/Arena Panel Graphic")]
    public sealed class ArenaPanelGraphic : MaskableGraphic
    {
        [SerializeField] Color rim = new Color(0.30f, 0.22f, 0.12f, 1f);
        [SerializeField] float corner = 12f;
        [SerializeField] float border = 2f;
        [SerializeField] bool ornaments = true;

        public void Configure(Color fill, Color edge, bool decorate)
        {
            color = fill;
            rim = edge;
            ornaments = decorate;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();

            if (r.width < 2f || r.height < 2f)
            {
                return;
            }

            // 바깥 팔각형 만들기
            Polygon(vh, r, corner, rim, rim * new Color(0.48f, 0.48f, 0.48f, 1f));

            // 안쪽 채우기
            Rect inner = Inset(r, border);
            Polygon(vh, inner, Mathf.Max(0f, corner - border), color, color);

            if (ornaments == false || r.width < 100f || r.height < 48f)
            {
                return;
            }

            // 상하단의 얇은 장식
            Color dim = rim * new Color(0.55f, 0.55f, 0.55f, 0.8f);
            Quad(vh, new Rect(r.xMin + 22f, r.yMax - 8f, r.width - 44f, 1f), dim);
            Quad(vh, new Rect(r.xMin + 22f, r.yMin + 7f, r.width - 44f, 1f), dim);

            // 상단 좌우의 짧은 선
            float span = Mathf.Min(42f, r.width * 0.08f);
            Quad(vh, new Rect(r.xMin + 22f, r.yMax - 3f, span, 2f), rim);
            Quad(vh, new Rect(r.xMax - 22f - span, r.yMax - 3f, span, 2f), rim);
        }

        // 사각형 r을 n만큼 안쪽으로 줄인 사각형 반환
        static Rect Inset(Rect r, float n)
        {
            return new Rect(r.x + n, r.y + n, Mathf.Max(0f, r.width - n * 2f), Mathf.Max(0f, r.height - n * 2f));
        }

        // 중심점 기준 삼각형 8개로 네 모서리를 cut만큼 깎은 팔각형을 만듦 (위는 top, 아래는 bottom 색)
        static void Polygon(VertexHelper vh, Rect r, float cut, Color top, Color bottom)
        {
            float c = Mathf.Min(cut, Mathf.Min(r.width, r.height) * 0.25f);
            int start = vh.currentVertCount;

            Add(vh, r.center, Color.Lerp(bottom, top, 0.5f));
            Add(vh, new Vector2(r.xMin + c, r.yMin), bottom);
            Add(vh, new Vector2(r.xMax - c, r.yMin), bottom);
            Add(vh, new Vector2(r.xMax, r.yMin + c), bottom);
            Add(vh, new Vector2(r.xMax, r.yMax - c), top);
            Add(vh, new Vector2(r.xMax - c, r.yMax), top);
            Add(vh, new Vector2(r.xMin + c, r.yMax), top);
            Add(vh, new Vector2(r.xMin, r.yMax - c), top);
            Add(vh, new Vector2(r.xMin, r.yMin + c), bottom);

            for (int i = 0; i < 8; i++)
            {
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
            }
        }

        // 사각형 r을 색 c로 채움
        static void Quad(VertexHelper vh, Rect r, Color c)
        {
            int n = vh.currentVertCount;

            Add(vh, new Vector2(r.xMin, r.yMin), c);
            Add(vh, new Vector2(r.xMax, r.yMin), c);
            Add(vh, new Vector2(r.xMax, r.yMax), c);
            Add(vh, new Vector2(r.xMin, r.yMax), c);

            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }

        // vh에 점 p를 색 c로 추가
        static void Add(VertexHelper vh, Vector2 p, Color c)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }
    }
}
