using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 플레이어 HUD 옆 칩 위젯 (Figma 29:315, 116×88).
    /// 76×76 타일(칩색 프레임 + 젬 + 모드명 띠) · [B] 키캡 · 오른쪽 보유칩 다이아 4개.
    /// </summary>
    public sealed class V5ChipWidget : MonoBehaviour
    {
        const int ChipCount = 4;

        V5Shape _tileBg, _frame, _gem, _gemCore, _strip, _flash, _keyBg, _arrow;
        TextMeshProUGUI _mode, _key;
        RectTransform _gemRt, _keyRt, _modeRt;
        readonly V5Shape[] _pips = new V5Shape[ChipCount];
        readonly V5Shape[] _pipRings = new V5Shape[ChipCount];
        RectTransform _arrowRt;

        int _shown = -1;
        float _switchT = 99f;     // 전환 연출 경과(초)
        float _pressT = 99f;

        public System.Action<int> onSwitched;

        public static V5ChipWidget Build(Transform parent, float x, float y, V5Settings s)
        {
            var root = V5UI.Node(parent, "ChipWidget", x, y, 116, 88);
            var w = root.gameObject.AddComponent<V5ChipWidget>();
            w.Create(root, s);
            return w;
        }

        void Create(RectTransform root, V5Settings s)
        {
            var tile = V5UI.Node(root, "Tile", 0, 0, 76, 76);
            _tileBg = V5UI.ShapeStretch(tile, "Bg", V5Shape.Kind.Chamfer, V5Theme.A(V5Theme.Ink, 0.88f), 0, 10);
            _tileBg.corners = 1 | 4;
            var hatch = V5UI.ShapeStretch(tile, "Hatch", V5Shape.Kind.Hatch, V5Theme.A(V5Theme.Slate, 0.35f), 4);
            hatch.spacing = 6; hatch.stroke = 1;
            _frame = V5UI.ShapeStretch(tile, "Frame", V5Shape.Kind.Chamfer, V5Theme.ChipPink, 0, 10, 2);
            _frame.corners = 1 | 4;

            _gemRt = V5UI.Node(tile, "Gem", 21, 8, 34, 34);
            _gemRt.pivot = new Vector2(0.5f, 0.5f);
            _gemRt.anchoredPosition = new Vector2(21 + 17, -(8 + 17));
            _gem = V5UI.ShapeStretch(_gemRt, "Fill", V5Shape.Kind.Diamond, V5Theme.ChipPink);
            V5UI.ShapeStretch(_gemRt, "Edge", V5Shape.Kind.Diamond, V5Theme.A(V5Theme.Paper, 0.9f), 0, 0, 2);
            _gemCore = V5UI.Shape(_gemRt, "Core", V5Shape.Kind.Diamond, V5Theme.A(V5Theme.Paper, 0.85f), 12, 8, 10, 10);

            _strip = V5UI.Shape(tile, "ModeStrip", V5Shape.Kind.Rect, V5Theme.ChipPink, 2, 52, 72, 22);
            _modeRt = V5UI.Node(tile, "ModeClip", 2, 52, 72, 22);
            _modeRt.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            _mode = V5UI.Text(_modeRt, "Mode", "일섬", s.krBoldFont, 16, V5Theme.Ink, 0, 0, 72, 22, TextAlignmentOptions.Center);

            _flash = V5UI.ShapeStretch(tile, "Flash", V5Shape.Kind.Chamfer, V5Theme.A(V5Theme.Paper, 0f), 0, 10);
            _flash.corners = 1 | 4;

            // 키캡 [B]
            _keyRt = V5UI.Node(root, "Keycap", 58, -6, 24, 20);
            _keyRt.pivot = new Vector2(0.5f, 0.5f);
            _keyRt.anchoredPosition = new Vector2(58 + 12, -(-6 + 10));
            _keyBg = V5UI.ShapeStretch(_keyRt, "Bg", V5Shape.Kind.Chamfer, V5Theme.Paper, 0, 4);
            _keyBg.corners = 2 | 8;
            _key = V5UI.Text(_keyRt, "Key", KeyLabel(s.chipSwitchKeyLabel), s.numberFont, 14, V5Theme.Ink, 0, 0, 24, 20, TextAlignmentOptions.Center);
            _key.fontStyle = FontStyles.Bold;

            // 보유 칩 다이아 (세로 4개, 20px 간격)
            for (int i = 0; i < ChipCount; i++)
            {
                float py = 8 + i * 20;
                _pips[i] = V5UI.Shape(root, "Pip" + i, V5Shape.Kind.Diamond, V5Theme.Chip(i), 96, py, 12, 12);
                _pipRings[i] = V5UI.Shape(root, "PipRing" + i, V5Shape.Kind.Diamond, V5Theme.Slate, 94, py - 2, 16, 16, 0, 1.5f);
            }
            _arrow = V5UI.Shape(root, "Arrow", V5Shape.Kind.TriRight, V5Theme.Paper, 84, 10, 6, 8);
            _arrowRt = _arrow.rectTransform;
        }

        static string KeyLabel(KeyCode k)
        {
            string s = k.ToString();
            return s.Length > 3 ? s.Substring(0, 3).ToUpperInvariant() : s.ToUpperInvariant();
        }

        public Color CurrentColor => V5Theme.Chip(_shown < 0 ? 0 : _shown);

        public void Tick(int current, List<PlayerChip> owned, float dt)
        {
            if (current != _shown)
            {
                bool first = _shown < 0;
                _shown = current;
                ApplyColor(V5Theme.Chip(current));
                _mode.text = V5Theme.ChipMode(current);
                if (!first) { _switchT = 0f; _pressT = 0f; onSwitched?.Invoke(current); }
            }

            // 보유 / 현재 표시
            for (int i = 0; i < ChipCount; i++)
            {
                bool has = owned != null && owned.Contains((PlayerChip)i);
                bool cur = i == current;
                _pips[i].enabled = has;
                _pips[i].color = V5Theme.Chip(i);
                _pipRings[i].color = cur ? V5Theme.Paper : (has ? V5Theme.A(V5Theme.Chip(i), 0.0f) : V5Theme.Slate);
                _pipRings[i].outline = cur ? 2f : 1.5f;
            }
            float ay = 8 + Mathf.Clamp(current, 0, ChipCount - 1) * 20 + 2;
            var ap = _arrowRt.anchoredPosition;
            ap.y = Mathf.Lerp(ap.y, -ay, 1f - Mathf.Exp(-dt * 30f));
            _arrowRt.anchoredPosition = ap;
            _arrow.enabled = owned != null && owned.Count > 1;

            // ── 전환 연출 (Figma 8 Chip Switch Storyboard) ──
            _pressT += dt; _switchT += dt;
            float press = _pressT < 0.12f ? 1f - Mathf.Sin(_pressT / 0.12f * Mathf.PI) * 0.12f : 1f;
            _keyRt.localScale = new Vector3(press, press, 1f);
            _keyBg.color = _pressT < 0.12f ? V5Theme.Signal : V5Theme.Paper;

            float ft = _switchT - 0.06f;
            float flash = ft < 0f ? 0f : Mathf.Clamp01(1f - ft / 0.18f);
            _flash.color = V5Theme.A(V5Theme.Paper, flash * 0.85f);
            float g = _switchT < 0.06f ? 0.85f : Mathf.LerpUnclamped(1.3f, 1f, V5Theme.OutBack((_switchT - 0.06f) / 0.26f));
            _gemRt.localScale = new Vector3(g, g, 1f);
            float slide = Mathf.Clamp01((_switchT - 0.06f) / 0.12f);
            _mode.rectTransform.anchoredPosition = new Vector2(0f, -(1f - V5Theme.OutCubic(slide)) * 12f);
            _mode.alpha = _switchT < 0.06f ? 0f : 1f;
        }

        void ApplyColor(Color c)
        {
            _frame.color = c;
            _gem.color = c;
            _strip.color = c;
        }
    }
}
