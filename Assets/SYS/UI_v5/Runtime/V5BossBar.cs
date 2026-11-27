using TMPro;
using UnityEngine;

namespace Unmachine.UIv5
{
    public struct V5BossSample
    {
        public bool present;
        public float hp;            // 0~1
        public float phaseSplit;    // 0~1 (2페이즈 경계)
        public int phase;           // 1, 2
        public bool invulnerable;
        public bool groggy, stunned, punishable, dead, engaged;
        public float groggyGauge;   // 0~1
    }

    /// <summary>
    /// 보스 체력바 (Figma 29:1735, 1280x112, 기본 화면 하단 (320,928)).
    /// 엠블럼 · 이름+상태태그 · 해칭 트랙 HP(고스트/팁/파편) · 50% 페이즈 마커 · 그로기 바.
    /// </summary>
    public sealed class V5BossBar : MonoBehaviour
    {
        const float BX = 120f, BY = 68f, BW = 1148f, BH = 24f;

        V5Settings _s;
        RectTransform _root, _slide, _fillRt, _ghostRt, _groggyRt, _tagRt, _tipRt, _emblemRt;
        CanvasGroup _group;
        V5Shape _fill, _fillHi, _fillLo, _fillHatch, _ghost, _tip, _tipGlow, _groggyFill, _rim, _eye, _eyeGlow, _tagBg, _phaseGap, _phaseMark;
        TextMeshProUGUI _name, _sub, _tag, _phaseLabel, _groggyLabel;
        GameObject _lock;
        readonly RectTransform[] _sparks = new RectTransform[4];
        readonly RectTransform[] _chunks = new RectTransform[10];
        readonly V5Shape[] _chunkShapes = new V5Shape[10];
        readonly Vector2[] _chunkVel = new Vector2[10];
        readonly float[] _chunkLife = new float[10];
        int _chunkNext;

        float _hp = 1f, _ghostV = 1f, _ghostHold, _hitT = 99f, _introT = 99f, _deadT, _vis, _visTarget = 1f;
        float _shownGroggy;
        string _tagText = "";
        bool _shown, _wasGroggy;

        public static V5BossBar Build(Transform canvas, V5Settings s)
        {
            // 화면비가 16:9 가 아니어도 하단(또는 상단) 중앙에 붙도록 앵커 지정
            var root = V5UI.Node(canvas, "BossBar", 0, 0, 1280, 112);
            float edge = s.bossBarAtBottom ? 0f : 1f;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, edge);
            root.pivot = new Vector2(0.5f, edge);
            root.anchoredPosition = new Vector2(0f, s.bossBarAtBottom ? 40f : -40f); // Figma: y=928 → 하단 여백 40
            var bar = root.gameObject.AddComponent<V5BossBar>();
            bar.Create(root, s);
            return bar;
        }

        void Create(RectTransform root, V5Settings s)
        {
            _s = s; _root = root;
            _group = V5UI.Group(root.gameObject, 0f);
            _slide = V5UI.Stretch(root, "Slide");

            // ── 엠블럼 92x92 ──
            _emblemRt = V5UI.Node(_slide, "Emblem", 0, 0, 92, 92);
            var eb = V5UI.ShapeStretch(_emblemRt, "Bg", V5Shape.Kind.Chamfer, V5Theme.A(V5Theme.Ink, 0.92f), 0, 14);
            eb.corners = 1 | 4;
            var eh = V5UI.ShapeStretch(_emblemRt, "Hatch", V5Shape.Kind.Hatch, V5Theme.A(V5Theme.Slate, 0.3f), 4);
            eh.spacing = 7; eh.stroke = 1;
            // 메카 머리
            var head = V5UI.Shape(_emblemRt, "Head", V5Shape.Kind.Chamfer, V5Theme.Graphite, 18, 22, 56, 46, 10);
            head.gradient = true; head.gradientBottom = V5Theme.Ink;
            V5UI.Shape(_emblemRt, "HeadEdge", V5Shape.Kind.Chamfer, V5Theme.Steel, 18, 22, 56, 46, 10, 2);
            V5UI.Shape(_emblemRt, "Antenna", V5Shape.Kind.TriUp, V5Theme.Steel, 58, 12, 8, 10);
            V5UI.Shape(_emblemRt, "Visor", V5Shape.Kind.Rect, V5Theme.Ink, 26, 38, 40, 12);
            _eyeGlow = V5UI.Shape(_emblemRt, "EyeGlow", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Danger, 0.35f), 42, 36, 24, 16);
            _eye = V5UI.Shape(_emblemRt, "Eye", V5Shape.Kind.Rect, V5Theme.Danger, 48, 41, 12, 6);
            V5UI.Shape(_emblemRt, "Jaw", V5Shape.Kind.Rect, V5Theme.Slate, 30, 56, 32, 4);
            V5UI.Shape(_emblemRt, "Jaw2", V5Shape.Kind.Rect, V5Theme.Slate, 34, 62, 24, 3);
            // 무적 자물쇠
            var lockRt = V5UI.Node(_emblemRt, "Lock", 0, 0, 92, 92);
            _lock = lockRt.gameObject;
            V5UI.ShapeStretch(lockRt, "Dim", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Ink, 0.6f), 2);
            V5UI.Shape(lockRt, "Shackle", V5Shape.Kind.Chamfer, V5Theme.Paper, 34, 26, 24, 22, 6, 3);
            V5UI.Shape(lockRt, "Body", V5Shape.Kind.Rect, V5Theme.Paper, 30, 42, 32, 24);
            V5UI.Shape(lockRt, "Hole", V5Shape.Kind.Rect, V5Theme.Ink, 44, 50, 4, 8);
            // 그로기 스파크
            for (int i = 0; i < _sparks.Length; i++)
                _sparks[i] = V5UI.Shape(_emblemRt, "Spark" + i, V5Shape.Kind.Diamond, V5Theme.Signal, 0, 0, 8, 8).rectTransform;
            _rim = V5UI.ShapeStretch(_emblemRt, "Rim", V5Shape.Kind.Chamfer, V5Theme.Slate, 0, 14, 2);
            _rim.corners = 1 | 4;

            // ── 이름 / 태그 / 부제 ──
            _name = V5UI.Text(_slide, "Name", s.bossName, s.krBoldFont, 34, V5Theme.Paper, BX, 6, 600, 44, TextAlignmentOptions.BottomLeft);
            _tagRt = V5UI.Node(_slide, "Tag", BX + 300, 22, 120, 24);
            _tagBg = V5UI.ShapeStretch(_tagRt, "Bg", V5Shape.Kind.Parallelogram, V5Theme.Slate, 0, 6);
            _tag = V5UI.Text(_tagRt, "T", "PHASE 1", s.numberFont, 16, V5Theme.Paper, 0, 0, 120, 24, TextAlignmentOptions.Center);
            _tag.fontStyle = FontStyles.Bold;
            _tag.characterSpacing = 4;
            _sub = V5UI.Text(_slide, "Subtitle", s.bossSubtitle, s.numberFont, 14, V5Theme.Steel, BX, 30, BW, 20, TextAlignmentOptions.BottomRight);
            _sub.characterSpacing = 6;

            // ── HP 바 ──
            V5UI.Shape(_slide, "CapL", V5Shape.Kind.Rect, V5Theme.Steel, BX - 10, BY - 4, 4, BH + 8);
            V5UI.Shape(_slide, "CapR", V5Shape.Kind.Rect, V5Theme.Steel, BX + BW + 6, BY - 4, 4, BH + 8);
            var bar = V5UI.Node(_slide, "HpBar", BX, BY, BW, BH);
            V5UI.ShapeStretch(bar, "Track", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Ink, 0.9f));
            var th = V5UI.ShapeStretch(bar, "TrackHatch", V5Shape.Kind.Hatch, V5Theme.A(V5Theme.Slate, 0.55f));
            th.spacing = 8; th.stroke = 2;
            _ghostRt = V5UI.FillNode(bar, "Ghost");
            _ghost = V5UI.ShapeStretch(_ghostRt, "G", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Paper, 0.9f));
            _fillRt = V5UI.FillNode(bar, "Fill");
            _fill = V5UI.ShapeStretch(_fillRt, "F", V5Shape.Kind.Rect, V5Theme.Danger);
            var hi = V5UI.Stretch(_fillRt, "Hi"); hi.anchorMin = new Vector2(0, 1); hi.offsetMin = new Vector2(0, -5); hi.offsetMax = Vector2.zero;
            _fillHi = hi.gameObject.AddComponent<V5Shape>(); _fillHi.raycastTarget = false; _fillHi.color = V5Theme.DangerHi;
            var lo = V5UI.Stretch(_fillRt, "Lo"); lo.anchorMax = new Vector2(1, 0); lo.offsetMin = Vector2.zero; lo.offsetMax = new Vector2(0, 3);
            _fillLo = lo.gameObject.AddComponent<V5Shape>(); _fillLo.raycastTarget = false; _fillLo.color = V5Theme.DangerLo;
            _fillRt.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            _fillHatch = V5UI.Shape(_fillRt, "InvHatch", V5Shape.Kind.Hatch, V5Theme.A(V5Theme.Ink, 0.45f), 0, 0, BW, BH);
            _fillHatch.spacing = 10; _fillHatch.stroke = 4;

            // 팁 (fill 오른쪽 끝)
            _tipRt = V5UI.Node(bar, "Tip", 0, -4, 4, BH + 8);
            _tipRt.anchorMin = _tipRt.anchorMax = new Vector2(1f, 1f); // fill 따라 x 를 매 프레임 지정
            _tipGlow = V5UI.Shape(_tipRt, "Glow", V5Shape.Kind.Rect, V5Theme.A(V5Theme.DangerHi, 0.35f), -6, 0, 16, BH + 8);
            _tip = V5UI.ShapeStretch(_tipRt, "Core", V5Shape.Kind.Rect, V5Theme.Paper);

            // 50% 페이즈 경계
            float px = BW * (1f - 0.5f);
            _phaseGap = V5UI.Shape(bar, "PhaseGap", V5Shape.Kind.Rect, V5Theme.Ink, px - 2, 0, 4, BH);
            _phaseMark = V5UI.Shape(_slide, "PhaseMark", V5Shape.Kind.TriDown, V5Theme.Paper, BX + px - 6, BY - 12, 12, 8);
            _phaseLabel = V5UI.Text(_slide, "PhaseLabel", "II", s.numberFont, 14, V5Theme.Mist, BX + px + 8, BY - 20, 40, 18, TextAlignmentOptions.BottomLeft);
            _phaseLabel.fontStyle = FontStyles.Bold;
            V5UI.ShapeStretch(bar, "Edge", V5Shape.Kind.Rect, V5Theme.Slate, 0, 0, 1);

            // 파편
            for (int i = 0; i < _chunks.Length; i++)
            {
                _chunkShapes[i] = V5UI.Shape(_slide, "Chunk" + i, V5Shape.Kind.Rect, V5Theme.Danger, 0, 0, 6, 6);
                _chunks[i] = _chunkShapes[i].rectTransform;
                _chunkShapes[i].enabled = false;
            }

            // ── 그로기 바 6px ──
            _groggyLabel = V5UI.Text(_slide, "GroggyLabel", "GROGGY", s.numberFont, 12, V5Theme.Signal, 0, 96, 92, 16, TextAlignmentOptions.MidlineRight);
            _groggyLabel.characterSpacing = 6;
            _groggyLabel.fontStyle = FontStyles.Bold;
            var gb = V5UI.Node(_slide, "GroggyBar", BX, 101, BW, 6);
            V5UI.ShapeStretch(gb, "Track", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Slate, 0.85f));
            _groggyRt = V5UI.FillNode(gb, "Fill");
            _groggyFill = V5UI.ShapeStretch(_groggyRt, "F", V5Shape.Kind.Rect, V5Theme.Signal);

            SetPhaseSplit(0.5f);
        }

        float _split = -1f;
        void SetPhaseSplit(float split)
        {
            if (Mathf.Approximately(split, _split)) return;
            _split = split;
            // HP 는 왼쪽부터 차 있으므로 split(=남은 비율) 위치에 마커
            float px = BW * Mathf.Clamp01(split);
            var g = _phaseGap.rectTransform; g.anchoredPosition = new Vector2(px - 2, 0);
            _phaseMark.rectTransform.anchoredPosition = new Vector2(BX + px - 6, -(BY - 12));
            _phaseLabel.rectTransform.anchoredPosition = new Vector2(BX + px + 8, -(BY - 20));
        }

        /// <summary>대화/시네마틱 중 숨김.</summary>
        public void SetVisible(bool v) => _visTarget = v ? 1f : 0f;

        public void ResetState()
        {
            _shown = false; _vis = 0f; _group.alpha = 0f; _deadT = 0f; _hp = 1f; _ghostV = 1f; _introT = 99f;
        }

        public void Tick(V5BossSample b, float dt)
        {
            if (!b.present) { _vis = Mathf.MoveTowards(_vis, 0f, dt * 4f); _group.alpha = _vis; if (_vis <= 0f) _shown = false; return; }

            if (!_shown && b.engaged && !b.dead) { _shown = true; _introT = 0f; _hp = b.hp; _ghostV = b.hp; }
            if (!_shown) { _group.alpha = 0f; return; }

            SetPhaseSplit(b.phaseSplit);

            // 등장: 위로 24px 슬라이드 + 알파, HP 는 0→현재로 스윕
            _introT += dt;
            float intro = V5Theme.OutCubic(_introT / 0.35f);
            _slide.anchoredPosition = new Vector2(0f, (1f - intro) * 24f * (_s.bossBarAtBottom ? -1f : 1f));
            float sweep = V5Theme.OutCubic((_introT - 0.15f) / 0.6f);

            // 사망 → 1.2초 뒤 페이드
            if (b.dead) _deadT += dt; else _deadT = 0f;
            float deadFade = b.dead ? Mathf.Clamp01(1f - (_deadT - 1.2f) / 0.5f) : 1f;
            _vis = Mathf.MoveTowards(_vis, _visTarget, dt * 6f);
            _group.alpha = intro * _vis * deadFade;

            // HP · 고스트 · 피격
            if (b.hp < _hp - 0.0001f)
            {
                SpawnChunks(_hp, b.hp);
                _ghostHold = 0.45f; _hitT = 0f;
            }
            if (b.hp > _ghostV) _ghostV = b.hp;
            _hp = b.hp;
            if (_ghostHold > 0f) _ghostHold -= dt; else _ghostV = Mathf.MoveTowards(_ghostV, _hp, dt * 0.5f);

            float shownHp = _introT < 0.75f ? _hp * Mathf.Clamp01(sweep) : _hp;
            V5UI.SetFill(_fillRt, shownHp);
            V5UI.SetFill(_ghostRt, _introT < 0.75f ? shownHp : _ghostV);
            _tipRt.anchorMin = _tipRt.anchorMax = new Vector2(Mathf.Clamp01(shownHp), 1f);
            _tipRt.anchoredPosition = new Vector2(-2f, 4f);
            _tipRt.gameObject.SetActive(shownHp > 0.001f);

            _hitT += dt;
            float hitFlash = Mathf.Clamp01(1f - _hitT / 0.08f);

            // 상태별 색
            Color fillC = V5Theme.Danger, hiC = V5Theme.DangerHi, loC = V5Theme.DangerLo, rimC = V5Theme.Slate;
            string tag; Color tagBg, tagFg;
            if (b.invulnerable)
            {
                fillC = V5Theme.Steel; hiC = V5Theme.Mist; loC = V5Theme.Slate;
                tag = "INVINCIBLE"; tagBg = V5Theme.Slate; tagFg = V5Theme.Mist;
            }
            else if (b.groggy)
            {
                rimC = V5Theme.Signal;
                tag = "GROGGY x1.5"; tagBg = V5Theme.Signal; tagFg = V5Theme.Ink;
            }
            else if (b.stunned || b.punishable)
            {
                rimC = V5Theme.Paper;
                tag = b.stunned ? "STUN x1.2" : "OPEN x1.2"; tagBg = V5Theme.Paper; tagFg = V5Theme.Ink;
            }
            else if (b.phase >= 2)
            {
                rimC = V5Theme.Danger;
                tag = "PHASE 2"; tagBg = V5Theme.Danger; tagFg = V5Theme.Paper;
            }
            else { tag = "PHASE 1"; tagBg = V5Theme.Slate; tagFg = V5Theme.Paper; }

            _fill.color = Color.Lerp(fillC, Color.white, hitFlash);
            _fillHi.color = hiC; _fillLo.color = loC;
            _fillHatch.enabled = b.invulnerable;
            _tipGlow.color = V5Theme.A(b.invulnerable ? V5Theme.Mist : V5Theme.DangerHi, 0.25f + 0.25f * hitFlash);
            _lock.SetActive(b.invulnerable);
            _rim.color = rimC;
            _rim.outline = b.groggy || b.phase >= 2 ? 3f : 2f;
            float eyePulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * (b.phase >= 2 ? 9f : 4f));
            _eye.color = b.groggy ? V5Theme.Steel : V5Theme.A(V5Theme.Danger, eyePulse);
            _eyeGlow.enabled = !b.groggy;

            if (tag != _tagText)
            {
                _tagText = tag;
                _tag.text = tag;
                float w = _tag.GetPreferredValues(tag).x + 28f;
                float nameW = Mathf.Min(_name.GetPreferredValues(_name.text).x, 600f);
                V5UI.Place(_tagRt, BX + nameW + 16f, 22f, w, 24f);
                ((RectTransform)_tag.transform).sizeDelta = new Vector2(w, 24f);
            }
            _tagBg.color = tagBg; _tag.color = tagFg;

            // 그로기 게이지 (그로기 중엔 꽉 찬 채로 맥동)
            float gTarget = b.groggy ? 1f : Mathf.Clamp01(b.groggyGauge);
            _shownGroggy = Mathf.MoveTowards(_shownGroggy, gTarget, dt * 2.5f);
            V5UI.SetFill(_groggyRt, _shownGroggy);
            float gp = b.groggy ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f)) : 1f;
            _groggyFill.color = V5Theme.A(_shownGroggy >= 0.999f && !b.groggy ? Color.white : V5Theme.Signal, gp);
            _groggyLabel.alpha = b.groggy ? gp : 0.85f;

            // 스파크
            for (int i = 0; i < _sparks.Length; i++)
            {
                _sparks[i].gameObject.SetActive(b.groggy);
                if (!b.groggy) continue;
                float a = Time.unscaledTime * 3.2f + i * Mathf.PI * 0.5f;
                _sparks[i].anchoredPosition = new Vector2(46f + Mathf.Cos(a) * 40f - 4f, -(46f + Mathf.Sin(a) * 40f - 4f));
                float sc = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(a * 2f));
                _sparks[i].localScale = new Vector3(sc, sc, 1f);
            }
            if (b.groggy && !_wasGroggy) _hitT = 0f; // 진입 플래시
            _wasGroggy = b.groggy;

            TickChunks(dt);
        }

        void SpawnChunks(float from, float to)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt((from - to) * 60f), 2, 5);
            for (int k = 0; k < n; k++)
            {
                int i = _chunkNext; _chunkNext = (_chunkNext + 1) % _chunks.Length;
                float x = BX + BW * Mathf.Lerp(to, from, Random.value);
                _chunks[i].anchoredPosition = new Vector2(x, -(BY + Random.Range(2f, BH - 6f)));
                float s = Random.Range(4f, 9f);
                _chunks[i].sizeDelta = new Vector2(s, s);
                _chunkVel[i] = new Vector2(Random.Range(-60f, 90f), Random.Range(80f, 180f) * (_s.bossBarAtBottom ? 1f : 0.6f));
                _chunkLife[i] = 0.55f;
                _chunkShapes[i].enabled = true;
                _chunkShapes[i].color = Random.value > 0.35f ? V5Theme.Danger : V5Theme.Paper;
            }
        }

        void TickChunks(float dt)
        {
            for (int i = 0; i < _chunks.Length; i++)
            {
                if (_chunkLife[i] <= 0f) { if (_chunkShapes[i].enabled) _chunkShapes[i].enabled = false; continue; }
                _chunkLife[i] -= dt;
                _chunkVel[i].y -= 520f * dt;
                _chunks[i].anchoredPosition += _chunkVel[i] * dt;
                _chunks[i].localEulerAngles = new Vector3(0, 0, _chunkLife[i] * 400f);
                var c = _chunkShapes[i].color; c.a = Mathf.Clamp01(_chunkLife[i] / 0.3f); _chunkShapes[i].color = c;
            }
        }
    }
}
