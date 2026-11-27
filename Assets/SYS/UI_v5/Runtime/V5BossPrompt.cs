using TMPro;
using UnityEngine;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 그로기 중 보스 머리 위 "낙섬!" 입력 안내 (Figma 29:1736, 180×64 + 포인터).
    /// </summary>
    public sealed class V5BossPrompt : MonoBehaviour
    {
        RectTransform _canvasRt, _rt, _pop;
        CanvasGroup _group;
        Transform _target;
        Renderer[] _renderers;
        float _t = 99f;
        bool _on;

        public static V5BossPrompt Build(RectTransform canvasRt, V5Settings s)
        {
            var rt = V5UI.Node(canvasRt, "RakusenPrompt", 0, 0, 180, 80);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f); // 포인터 끝이 머리 위 기준점
            var p = rt.gameObject.AddComponent<V5BossPrompt>();
            p.Create(canvasRt, rt, s);
            return p;
        }

        void Create(RectTransform canvasRt, RectTransform rt, V5Settings s)
        {
            _canvasRt = canvasRt; _rt = rt;
            _group = V5UI.Group(rt.gameObject, 0f);
            _pop = V5UI.Node(rt, "Pop", 0, 0, 180, 80);
            _pop.pivot = new Vector2(0.5f, 0f);
            _pop.anchorMin = _pop.anchorMax = new Vector2(0.5f, 0f);
            _pop.anchoredPosition = Vector2.zero;

            var plate = V5UI.Shape(_pop, "Plate", V5Shape.Kind.Chamfer, V5Theme.Signal, 0, 0, 180, 64, 12);
            plate.corners = 1 | 4;
            V5UI.Shape(_pop, "PlateEdge", V5Shape.Kind.Chamfer, V5Theme.Ink, 4, 4, 172, 56, 9, 2).corners = 1 | 4;
            V5UI.Shape(_pop, "Pointer", V5Shape.Kind.TriDown, V5Theme.Signal, 80, 63, 20, 14);

            // 키 칩
            V5UI.Shape(_pop, "KeyBg", V5Shape.Kind.Chamfer, V5Theme.Ink, 14, 16, 52, 32, 6).corners = 2 | 8;
            var k = V5UI.Text(_pop, "Key", s.rakusenKeyLabel, s.numberFont, 18, V5Theme.Signal, 14, 16, 52, 32, TextAlignmentOptions.Center);
            k.fontStyle = FontStyles.Bold;
            V5UI.Text(_pop, "Label", s.rakusenLabel, s.krBoldFont, 30, V5Theme.Ink, 74, 8, 100, 48, TextAlignmentOptions.MidlineLeft);
        }

        public void SetTarget(Transform t)
        {
            if (_target == t) return;
            _target = t;
            _renderers = t != null ? t.GetComponentsInChildren<Renderer>(true) : null;
        }

        public void Tick(bool show, float dt)
        {
            if (show && !_on) _t = 0f;
            _on = show;
            _t += dt;

            if (!_on || _target == null) { _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt * 8f); return; }

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 head = _target.position;
            bool found = false;
            if (_renderers != null)
            {
                float top = float.MinValue;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var r = _renderers[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (!(r is SpriteRenderer)) continue;
                    var b = r.bounds;
                    if (b.max.y > top) { top = b.max.y; head.x = b.center.x; found = true; }
                }
                if (found) head.y = top;
            }
            head.y += 0.25f;

            Vector2 screen = cam.WorldToScreenPoint(head);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screen, null, out var local))
            {
                // 화면 밖으로 안 나가게
                var size = _canvasRt.rect.size;
                local.x = Mathf.Clamp(local.x, -size.x * 0.5f + 100f, size.x * 0.5f - 100f);
                local.y = Mathf.Clamp(local.y, -size.y * 0.5f + 20f, size.y * 0.5f - 100f);
                _rt.anchoredPosition = local + new Vector2(0f, 4f * Mathf.Sin(Time.unscaledTime * 6f));
            }

            float pop = V5Theme.OutBack(_t / 0.22f);
            _pop.localScale = new Vector3(pop, pop, 1f);
            _group.alpha = Mathf.Clamp01(_t / 0.08f);
        }
    }
}
