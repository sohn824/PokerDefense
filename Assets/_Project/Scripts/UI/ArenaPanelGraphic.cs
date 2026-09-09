using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * ArenaPanelGraphic
     *
     * 해상도에 독립적인 절삭 금속 프레임을 메시로 직접 그린다
     * 장식이라 입력은 받지 않는다
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

            Polygon(vh, r, corner, rim, rim * new Color(0.48f, 0.48f, 0.48f, 1f));

            Rect inner = Inset(r, border);
            Polygon(vh, inner, Mathf.Max(0f, corner - border), color, color);

            if (ornaments == false || r.width < 100f || r.height < 48f)
            {
                return;
            }

            // 상하단의 얇은 장식만 쓰고 본문 영역은 비워 둔다
            Color dim = rim * new Color(0.55f, 0.55f, 0.55f, 0.8f);
            Quad(vh, new Rect(r.xMin + 22f, r.yMax - 8f, r.width - 44f, 1f), dim);
            Quad(vh, new Rect(r.xMin + 22f, r.yMin + 7f, r.width - 44f, 1f), dim);

            float span = Mathf.Min(42f, r.width * 0.08f);
            Quad(vh, new Rect(r.xMin + 22f, r.yMax - 3f, span, 2f), rim);
            Quad(vh, new Rect(r.xMax - 22f - span, r.yMax - 3f, span, 2f), rim);
        }

        static Rect Inset(Rect r, float n)
        {
            return new Rect(r.x + n, r.y + n, Mathf.Max(0f, r.width - n * 2f), Mathf.Max(0f, r.height - n * 2f));
        }

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

        static void Add(VertexHelper vh, Vector2 p, Color c)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }
    }
}
