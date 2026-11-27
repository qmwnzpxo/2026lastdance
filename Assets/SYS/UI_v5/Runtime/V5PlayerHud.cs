using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 플레이어 HUD (Figma 29:476, 600×108, 화면 (40,32)).
    /// 초상 · HP 숫자 · HP 바(고스트/눈금) · SP 바 · 검게이지 5칸 · 칩 위젯.
    /// PlayerState 를 읽기만 한다.
    /// </summary>
    public sealed class V5PlayerHud : MonoBehaviour
    {
        const float BarX = 104f, BarW = 330f;
        const int CellCount = 5;

        V5Settings _s;
        PlayerState _player;

        RectTransform _root, _shakeRt;
        CanvasGroup _group;
        V5Shape _portraitFrame, _portraitStrip, _hpFill, _hpHi, _hpGhost, _spFill, _hpTick;
        RectTransform _hpFillRt, _hpGhostRt, _spFillRt;
        RawImage _portrait;
        TextMeshProUGUI _hpText, _cellCount;
        readonly V5Shape[] _cellBg = new V5Shape[CellCount];
        readonly V5Shape[] _cellFill = new V5Shape[CellCount];
        readonly RectTransform[] _cellFillRt = new RectTransform[CellCount];
        readonly Color[] _cellColor = new Color[CellCount];
        readonly float[] _cellDelay = new float[CellCount];
        V5ChipWidget _chip;

        float _hpShown = -1f, _ghost = 1f, _ghostHold, _shakeT = 99f, _hitFlashT = 99f;
        int _lastHpInt = int.MinValue, _lastCells = -1;
        float _visible = 1f, _visibleTarget = 1f;

        public static V5PlayerHud Build(Transform canvas, V5Settings s)
        {
            var root = V5UI.Node(canvas, "PlayerHud", 40, 32, 600, 108);
            var hud = root.gameObject.AddComponent<V5PlayerHud>();
            hud.Create(root, s);
            return hud;
        }

        void Create(RectTransform root, V5Settings s)
        {
            _s = s; _root = root;
            _group = V5UI.Group(root.gameObject);
            _shakeRt = V5UI.Stretch(root, "Shake");

            // ── 초상 88 ──
            var pf = V5UI.Node(_shakeRt, "Portrait", 0, 0, 88, 88);
            V5UI.ShapeStretch(pf, "Bg", V5Shape.Kind.Rect, V5Theme.Graphite);
            _portrait = V5UI.Raw(pf, "Face", 2, 2, 84, 84);
            V5UI.SetSpriteCrop(_portrait, s.playerPortrait, s.playerPortraitUV);
            var shade = V5UI.ShapeStretch(pf, "Shade", V5Shape.Kind.Rect, new Color(0, 0, 0, 0), 2);
            shade.gradient = true; shade.gradientBottom = V5Theme.A(V5Theme.Ink, 0.55f);
            _portraitStrip = V5UI.Shape(pf, "ChipStrip", V5Shape.Kind.Rect, V5Theme.ChipPink, 2, 82, 84, 4);
            _portraitFrame = V5UI.ShapeStretch(pf, "Frame", V5Shape.Kind.Rect, V5Theme.Paper, 0, 0, 2);

            // ── HP 숫자 ──
            _hpText = V5UI.Text(_shakeRt, "HpText", "100", s.numberFont, 42, V5Theme.Paper, BarX, -8, 320, 50, TextAlignmentOptions.BottomLeft);
            _hpText.fontStyle = FontStyles.Bold;

            // ── HP 바 330×14 ──
            var hp = V5UI.Node(_shakeRt, "HpBar", BarX, 44, BarW, 14);
            V5UI.ShapeStretch(hp, "Track", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Ink, 0.85f));
            _hpGhostRt = V5UI.FillNode(hp, "Ghost");
            _hpGhost = V5UI.ShapeStretch(_hpGhostRt, "G", V5Shape.Kind.Rect, V5Theme.Danger);
            _hpFillRt = V5UI.FillNode(hp, "Fill");
            _hpFill = V5UI.ShapeStretch(_hpFillRt, "F", V5Shape.Kind.Rect, V5Theme.Paper);
            var hi = V5UI.Stretch(_hpFillRt, "Hi");
            hi.anchorMin = new Vector2(0, 1); hi.offsetMin = new Vector2(0, -2); hi.offsetMax = Vector2.zero;
            _hpHi = hi.gameObject.AddComponent<V5Shape>(); _hpHi.raycastTarget = false; _hpHi.color = Color.white;
            for (int i = 1; i < 10; i++)
                V5UI.Shape(hp, "Notch" + i, V5Shape.Kind.Rect, V5Theme.Ink, BarW * i / 10f - 1f, 0, 2, 14);
            V5UI.ShapeStretch(hp, "Edge", V5Shape.Kind.Rect, V5Theme.Slate, 0, 0, 1);
            _hpTick = V5UI.Shape(_shakeRt, "EndTick", V5Shape.Kind.Rect, V5Theme.Paper, BarX + BarW + 4, 41, 2, 20);

            // ── SP 바 330×3 ──
            var sp = V5UI.Node(_shakeRt, "SpBar", BarX, 62, BarW, 3);
            V5UI.ShapeStretch(sp, "Track", V5Shape.Kind.Rect, V5Theme.Slate);
            _spFillRt = V5UI.FillNode(sp, "Fill");
            _spFill = V5UI.ShapeStretch(_spFillRt, "F", V5Shape.Kind.Rect, V5Theme.Mist);

            // ── 검게이지 5칸 30×10, step 34 ──
            for (int i = 0; i < CellCount; i++)
            {
                var c = V5UI.Node(_shakeRt, "Cell" + i, BarX + i * 34, 74, 30, 10);
                _cellBg[i] = V5UI.ShapeStretch(c, "Bg", V5Shape.Kind.Parallelogram, V5Theme.A(V5Theme.Slate, 0.7f), 0, 4);
                var f = V5UI.FillNode(c, "Fill");
                f.gameObject.AddComponent<RectMask2D>(); // 칸 모양은 고정, 마스크 폭만 줄인다
                _cellFillRt[i] = f;
                _cellFill[i] = V5UI.Shape(f, "F", V5Shape.Kind.Parallelogram, V5Theme.ChipPink, 0, 0, 30, 10, 4);
                _cellColor[i] = V5Theme.ChipPink;
            }
            _cellCount = V5UI.Text(_shakeRt, "CellCount", "0/5", s.numberFont, 16, V5Theme.Mist,
                                   BarX + CellCount * 34 + 4, 70, 60, 18, TextAlignmentOptions.MidlineLeft);

            // ── 칩 위젯 ──
            _chip = V5ChipWidget.Build(_shakeRt, 466, 8, s);
            _chip.onSwitched = OnChipSwitched;
        }

        public void Bind(PlayerState p)
        {
            if (_player == p) return;
            _player = p;
            _hpShown = -1f;
            _lastHpInt = int.MinValue;
        }

        public bool HasPlayer => _player != null;

        /// <summary>대화/시네마틱 중엔 숨김.</summary>
        public void SetVisible(bool v) => _visibleTarget = v ? 1f : 0f;

        void OnChipSwitched(int chip)
        {
            // 140ms 부터 30ms 간격으로 칸 색이 바뀐다
            for (int i = 0; i < CellCount; i++) _cellDelay[i] = 0.14f + i * 0.03f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _visible = Mathf.MoveTowards(_visible, _player != null ? _visibleTarget : 0f, dt * 6f);
            _group.alpha = _visible;
            if (_player == null) return;

            float maxHp = Mathf.Max(1f, _player.maxHP);
            float hp = Mathf.Clamp(_player.currentHP, 0f, maxHp);
            float ratio = hp / maxHp;
            bool low = ratio <= _s.lowHpRatio;

            // 피격/회복 감지
            if (_hpShown < 0f) { _hpShown = ratio; _ghost = ratio; }
            if (ratio < _hpShown - 0.0001f) { _ghostHold = 0.35f; _shakeT = 0f; _hitFlashT = 0f; }
            if (ratio > _ghost) _ghost = ratio;
            _hpShown = ratio;
            if (_ghostHold > 0f) _ghostHold -= dt;
            else _ghost = Mathf.MoveTowards(_ghost, ratio, dt * 0.6f);

            V5UI.SetFill(_hpFillRt, ratio);
            V5UI.SetFill(_hpGhostRt, _ghost);

            _hitFlashT += dt;
            float flash = Mathf.Clamp01(1f - _hitFlashT / 0.12f);
            Color baseFill = low ? V5Theme.Danger : V5Theme.Paper;
            _hpFill.color = Color.Lerp(baseFill, Color.white, flash);
            _hpHi.color = low ? V5Theme.DangerHi : Color.white;
            _hpGhost.color = low ? V5Theme.A(V5Theme.Paper, 0.8f) : V5Theme.Danger;
            float pulse = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
            _hpTick.color = low ? V5Theme.A(V5Theme.Danger, pulse) : V5Theme.Paper;
            _portraitFrame.color = low ? V5Theme.A(V5Theme.Danger, pulse) : V5Theme.Paper;

            int hpInt = Mathf.CeilToInt(hp);
            if (hpInt != _lastHpInt)
            {
                _lastHpInt = hpInt;
                _hpText.text = hpInt + "<size=20><color=" + V5UI.Hex(V5Theme.Mist) + ">  / " + Mathf.CeilToInt(maxHp) + "</color></size>";
            }
            _hpText.color = Color.Lerp(low ? V5Theme.Danger : V5Theme.Paper, Color.white, flash);

            // 흔들림
            _shakeT += dt;
            if (_shakeT < 0.22f)
            {
                float a = (1f - _shakeT / 0.22f) * 4f;
                _shakeRt.anchoredPosition = new Vector2(Random.Range(-a, a), Random.Range(-a, a) * 0.5f);
            }
            else _shakeRt.anchoredPosition = Vector2.zero;

            // SP
            float sp = _player.maxStamina > 0f ? Mathf.Clamp01(_player.stamina / _player.maxStamina) : 0f;
            V5UI.SetFill(_spFillRt, sp);
            _spFill.color = _player.isExhausted
                ? V5Theme.A(V5Theme.Danger, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)))
                : V5Theme.Mist;

            // 칩
            int chip = (int)_player.currentChip;
            _chip.Tick(chip, _player.ownedChips, dt);
            Color chipColor = V5Theme.Chip(chip);
            _portraitStrip.color = chipColor;

            // 검게이지 칸
            float cost = Mathf.Max(0.0001f, _player.DashCost);
            float gauge = Mathf.Max(0f, _player.DashGauge);
            int cells = Mathf.Clamp(Mathf.RoundToInt(_player.MaxDashGauge / cost), 1, CellCount);
            int full = Mathf.Min(cells, Mathf.FloorToInt(gauge / cost + 0.0001f));
            for (int i = 0; i < CellCount; i++)
            {
                bool exists = i < cells;
                _cellBg[i].enabled = exists;
                _cellFill[i].enabled = exists;
                if (!exists) continue;

                if (_cellDelay[i] > 0f)
                {
                    _cellDelay[i] -= dt;
                    if (_cellDelay[i] <= 0f) _cellColor[i] = chipColor;
                }
                else _cellColor[i] = chipColor;

                float f = Mathf.Clamp01(gauge / cost - i);
                V5UI.SetFill(_cellFillRt[i], f);
                bool isFull = f >= 0.999f;
                // 덜 찬 칸은 반투명 → 다 차면 칩색 그대로
                _cellFill[i].color = isFull ? _cellColor[i] : V5Theme.A(_cellColor[i], 0.45f);
            }
            int key = full * 10 + cells;
            if (key != _lastCells) { _lastCells = key; _cellCount.text = full + "/" + cells; }
        }
    }
}
