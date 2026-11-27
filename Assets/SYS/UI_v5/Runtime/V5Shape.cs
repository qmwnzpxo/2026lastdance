using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 스프라이트 없이 쓰는 벡터 도형. 도형을 텍스처로 래스터(AA)해서 RawImage 로 그린다.
    /// (메시를 직접 만드는 방식은 프로젝트 환경에서 안 보이는 문제가 있어 RawImage 경로로 통일)
    /// Rect(채움, 그라데이션 없음)는 텍스처 없이 흰 쿼드 + 틴트라 비용 0.
    /// 같은 모양·크기 텍스처는 캐시해서 공유한다.
    /// </summary>
    [AddComponentMenu("Unmachine/UI v5/V5 Shape")]
    public class V5Shape : RawImage
    {
        public enum Kind { Rect, Chamfer, Diamond, TriUp, TriDown, TriRight, TriLeft, Parallelogram, Hatch }

        [SerializeField] Kind _kind = Kind.Rect;
        [SerializeField] float _cut = 8f;
        [Tooltip("Chamfer 모서리 비트: 1=TL 2=TR 4=BR 8=BL")]
        [SerializeField] int _corners = 15;
        [SerializeField] float _outline = 0f;
        [SerializeField] float _spacing = 8f;
        [SerializeField] float _stroke = 2f;
        [SerializeField] bool _gradient = false;
        [SerializeField] Color _gradientBottom = Color.black;
        [SerializeField] Color _tint = Color.white;

        bool _dirty = true;
        string _key;

        public Kind kind { get => _kind; set { if (_kind == value) return; _kind = value; MarkDirty(); } }
        public float cut { get => _cut; set { if (_cut == value) return; _cut = value; MarkDirty(); } }
        public int corners { get => _corners; set { if (_corners == value) return; _corners = value; MarkDirty(); } }
        public float outline { get => _outline; set { if (_outline == value) return; _outline = value; MarkDirty(); } }
        public float spacing { get => _spacing; set { if (_spacing == value) return; _spacing = value; MarkDirty(); } }
        public float stroke { get => _stroke; set { if (_stroke == value) return; _stroke = value; MarkDirty(); } }
        public bool gradient { get => _gradient; set { if (_gradient == value) return; _gradient = value; ApplyTint(); MarkDirty(); } }
        public Color gradientBottom { get => _gradientBottom; set { if (_gradientBottom == value) return; _gradientBottom = value; if (_gradient) MarkDirty(); } }

        /// <summary>도형 색. 그라데이션일 땐 윗색.</summary>
        public override Color color
        {
            get => base.color; // RawImage 가 메시 색으로 읽으므로 실제 틴트(그라데이션이면 흰색)를 돌려준다
            set
            {
                if (_tint == value) return;
                _tint = value;
                if (_gradient) MarkDirty(); else base.color = value;
            }
        }

        void ApplyTint() => base.color = _gradient ? Color.white : _tint;

        void MarkDirty() => _dirty = true; // LateUpdate 에서 한 번만 래스터

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyTint();
            _dirty = true;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _dirty = true;
        }

        void LateUpdate() { if (_dirty) Refresh(); }

        void Refresh()
        {
            _dirty = false;
            ApplyTint();
            Rect r = rectTransform.rect;
            if (r.width < 0.5f || r.height < 0.5f) return;

            // 단색 사각형은 텍스처 불필요
            if (_kind == Kind.Rect && _outline <= 0f && !_gradient)
            {
                if (texture != null) texture = null;
                _key = null;
                return;
            }
            // 사각형 + 세로 그라데이션 → 1px 폭 띠 텍스처
            if (_kind == Kind.Rect && _outline <= 0f && _gradient)
            {
                string gk = "g|" + ColorUtility.ToHtmlStringRGBA(_gradientBottom) + "|" + ColorUtility.ToHtmlStringRGBA(_tint);
                if (gk != _key) { _key = gk; texture = V5ShapeRaster.Gradient(gk, _gradientBottom, _tint); uvRect = new Rect(0, 0, 1, 1); }
                return;
            }

            int w = Mathf.Max(1, Mathf.RoundToInt(r.width)), h = Mathf.Max(1, Mathf.RoundToInt(r.height));
            string key = (int)_kind + "|" + w + "x" + h + "|" + _cut + "|" + _corners + "|" + _outline + "|" + _spacing + "|" + _stroke
                       + (_gradient ? "|" + ColorUtility.ToHtmlStringRGBA(_gradientBottom) + ColorUtility.ToHtmlStringRGBA(_tint) : "");
            if (key == _key && texture != null) return;
            _key = key;
            texture = V5ShapeRaster.Shape(key, this, w, h);
            uvRect = new Rect(0, 0, 1, 1);
        }

        // ── 래스터용 정보 (V5ShapeRaster 가 읽음) ──
        internal Kind K => _kind;
        internal float Cut => _cut;
        internal int Corners => _corners;
        internal float Outline => _outline;
        internal float Spacing => _spacing;
        internal float Stroke => _stroke;
        internal bool Grad => _gradient;
        internal Color GradBottom => _gradientBottom;
        internal Color Tint => _tint;

#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); _dirty = true; }
#endif
    }

    /// <summary>도형 → Texture2D (4×4 슈퍼샘플 AA). 키별 캐시.</summary>
    internal static class V5ShapeRaster
    {
        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();
        static readonly List<Vector2> s_Outer = new List<Vector2>(12);
        static readonly List<Vector2> s_Inner = new List<Vector2>(12);

        public static Texture2D Gradient(string key, Color bottom, Color top)
        {
            if (s_Cache.TryGetValue(key, out var t) && t != null) return t;
            const int H = 64;
            t = NewTex(1, H);
            var px = new Color32[H];
            for (int y = 0; y < H; y++) px[y] = Color.Lerp(bottom, top, (y + 0.5f) / H);
            t.SetPixels32(px); t.Apply(false, true);
            s_Cache[key] = t;
            return t;
        }

        public static Texture2D Shape(string key, V5Shape s, int w, int h)
        {
            if (s_Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            // 작은 도형은 2배로 그려서 고해상도 화면에서도 선명하게
            float scale = Mathf.Min(2f, 2048f / Mathf.Max(w, h));
            int tw = Mathf.Max(1, Mathf.RoundToInt(w * scale)), th = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            var tex = NewTex(tw, th);
            var px = new Color32[tw * th];

            bool hatch = s.K == V5Shape.Kind.Hatch;
            if (!hatch)
            {
                Polygon(s, w, h, s_Outer);
                if (s.Outline > 0f) Inset(s_Outer, s.Outline, s_Inner); else s_Inner.Clear();
            }
            float sp = Mathf.Max(2f, s.Spacing), st = Mathf.Clamp(s.Stroke, 0.5f, sp);

            const int SS = 4;
            for (int py = 0; py < th; py++)
            {
                float gy = (py + 0.5f) / th;
                Color rowCol = s.Grad ? Color.Lerp(s.GradBottom, s.Tint, gy) : Color.white;
                for (int pxi = 0; pxi < tw; pxi++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                    {
                        float y = (py + (sy + 0.5f) / SS) / scale;
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float x = (pxi + (sx + 0.5f) / SS) / scale;
                            bool inside;
                            if (hatch)
                            {
                                float u = x - y; // 위로 갈수록 오른쪽으로 기우는 45° 줄 (/)
                                u -= Mathf.Floor(u / sp) * sp;
                                inside = u < st;
                            }
                            else
                            {
                                var p = new Vector2(x, y);
                                inside = InConvex(s_Outer, p) && (s_Inner.Count == 0 || !InConvex(s_Inner, p));
                            }
                            if (inside) hit++;
                        }
                    }
                    var c = rowCol;
                    c.a *= hit / (float)(SS * SS);
                    px[py * tw + pxi] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_Cache[key] = tex;
            return tex;
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>(0,0)=좌하단, (w,h)=우상단 좌표의 시계방향 볼록 다각형.</summary>
        static void Polygon(V5Shape s, float w, float h, List<Vector2> p)
        {
            p.Clear();
            float x0 = 0, x1 = w, y0 = 0, y1 = h, cx = w * 0.5f, cy = h * 0.5f;
            switch (s.K)
            {
                case V5Shape.Kind.Chamfer:
                {
                    float c = Mathf.Min(s.Cut, Mathf.Min(w, h) * 0.5f);
                    int m = s.Corners;
                    if ((m & 1) != 0) { p.Add(new Vector2(x0, y1 - c)); p.Add(new Vector2(x0 + c, y1)); } else p.Add(new Vector2(x0, y1));
                    if ((m & 2) != 0) { p.Add(new Vector2(x1 - c, y1)); p.Add(new Vector2(x1, y1 - c)); } else p.Add(new Vector2(x1, y1));
                    if ((m & 4) != 0) { p.Add(new Vector2(x1, y0 + c)); p.Add(new Vector2(x1 - c, y0)); } else p.Add(new Vector2(x1, y0));
                    if ((m & 8) != 0) { p.Add(new Vector2(x0 + c, y0)); p.Add(new Vector2(x0, y0 + c)); } else p.Add(new Vector2(x0, y0));
                    break;
                }
                case V5Shape.Kind.Diamond:
                    p.Add(new Vector2(cx, y1)); p.Add(new Vector2(x1, cy)); p.Add(new Vector2(cx, y0)); p.Add(new Vector2(x0, cy));
                    break;
                case V5Shape.Kind.TriUp:
                    p.Add(new Vector2(cx, y1)); p.Add(new Vector2(x1, y0)); p.Add(new Vector2(x0, y0));
                    break;
                case V5Shape.Kind.TriDown:
                    p.Add(new Vector2(x0, y1)); p.Add(new Vector2(x1, y1)); p.Add(new Vector2(cx, y0));
                    break;
                case V5Shape.Kind.TriRight:
                    p.Add(new Vector2(x0, y1)); p.Add(new Vector2(x1, cy)); p.Add(new Vector2(x0, y0));
                    break;
                case V5Shape.Kind.TriLeft:
                    p.Add(new Vector2(x1, y1)); p.Add(new Vector2(x1, y0)); p.Add(new Vector2(x0, cy));
                    break;
                case V5Shape.Kind.Parallelogram:
                {
                    float k = Mathf.Clamp(s.Cut, -w, w);
                    if (k >= 0f) { p.Add(new Vector2(x0 + k, y1)); p.Add(new Vector2(x1, y1)); p.Add(new Vector2(x1 - k, y0)); p.Add(new Vector2(x0, y0)); }
                    else { p.Add(new Vector2(x0, y1)); p.Add(new Vector2(x1 + k, y1)); p.Add(new Vector2(x1, y0)); p.Add(new Vector2(x0 - k, y0)); }
                    break;
                }
                default:
                    p.Add(new Vector2(x0, y1)); p.Add(new Vector2(x1, y1)); p.Add(new Vector2(x1, y0)); p.Add(new Vector2(x0, y0));
                    break;
            }
        }

        static bool InConvex(List<Vector2> poly, Vector2 q)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                float cross = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
                if (cross > 0f) return false; // 시계방향 → 안쪽은 cross ≤ 0
            }
            return true;
        }

        /// <summary>시계방향 볼록 다각형을 d 만큼 안쪽으로.</summary>
        static void Inset(List<Vector2> poly, float d, List<Vector2> result)
        {
            result.Clear();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i - 1 + n) % n], cur = poly[i], next = poly[(i + 1) % n];
                Vector2 e0 = (cur - prev).normalized, e1 = (next - cur).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x); // 안쪽 법선
                Vector2 bis = n0 + n1;
                float len = bis.magnitude;
                if (len < 1e-4f) { result.Add(cur + n0 * d); continue; }
                bis /= len;
                float cos = Vector2.Dot(bis, n0);
                result.Add(cur + bis * (d / Mathf.Max(cos, 0.2f)));
            }
        }
    }
}
