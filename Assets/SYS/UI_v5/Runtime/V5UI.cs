using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 코드로 uGUI 를 조립하는 헬퍼. 좌표는 Figma 와 같은 "부모 좌상단 기준 px" 로 받는다.
    /// </summary>
    public static class V5UI
    {
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            return rt;
        }

        /// <summary>좌상단 앵커/피벗으로 배치.</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static RectTransform Stretch(Transform parent, string name, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static V5Shape Shape(Transform parent, string name, V5Shape.Kind kind, Color c,
                                    float x, float y, float w, float h, float cut = 0f, float outline = 0f)
        {
            var rt = Node(parent, name, x, y, w, h);
            return Setup(rt.gameObject.AddComponent<V5Shape>(), kind, c, cut, outline);
        }

        public static V5Shape ShapeStretch(Transform parent, string name, V5Shape.Kind kind, Color c,
                                           float inset = 0f, float cut = 0f, float outline = 0f)
        {
            var rt = Stretch(parent, name, inset, inset, inset, inset);
            return Setup(rt.gameObject.AddComponent<V5Shape>(), kind, c, cut, outline);
        }

        static V5Shape Setup(V5Shape s, V5Shape.Kind kind, Color c, float cut, float outline)
        {
            s.raycastTarget = false;
            s.kind = kind;
            s.color = c;
            if (cut != 0f) s.cut = cut;
            s.outline = outline;
            return s;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color c,
                                           float x, float y, float w, float h,
                                           TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Node(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.margin = Vector4.zero;
            return t;
        }

        public static RawImage Raw(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name, x, y, w, h);
            var r = rt.gameObject.AddComponent<RawImage>();
            r.raycastTarget = false;
            return r;
        }

        /// <summary>왼쪽에서 차는 바: fill 의 anchorMax.x 를 t 로.</summary>
        public static void SetFill(RectTransform fill, float t)
        {
            if (fill == null) return;
            t = Mathf.Clamp01(t);
            var max = fill.anchorMax;
            if (Mathf.Approximately(max.x, t)) return;
            max.x = t;
            fill.anchorMax = max;
        }

        /// <summary>부모 폭 기준 0~1 로 늘어나는 fill 노드 (anchorMin=0,0 / anchorMax=t,1).</summary>
        public static RectTransform FillNode(Transform parent, string name)
        {
            var rt = Stretch(parent, name);
            rt.anchorMax = new Vector2(1f, 1f);
            return rt;
        }

        /// <summary>스프라이트를 RawImage 에 UV 잘라 붙이기 (픽셀아트 얼굴 크롭용).</summary>
        public static void SetSpriteCrop(RawImage img, Sprite sprite, Rect uvInSprite)
        {
            if (img == null) return;
            if (sprite == null || sprite.texture == null) { img.texture = null; img.enabled = false; return; }
            var tex = sprite.texture;
            Rect tr = sprite.textureRect;
            float tw = tex.width, th = tex.height;
            img.texture = tex;
            img.uvRect = new Rect(
                (tr.x + tr.width * uvInSprite.x) / tw,
                (tr.y + tr.height * uvInSprite.y) / th,
                tr.width * uvInSprite.width / tw,
                tr.height * uvInSprite.height / th);
            img.enabled = true;
        }

        public static CanvasGroup Group(GameObject go, float alpha = 1f)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
