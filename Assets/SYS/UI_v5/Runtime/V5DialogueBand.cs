using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 대화 밴드 (Figma 29:1745, 1920×300, 콘텐츠 블록 x 320~1600).
    /// 팀원 DialogueManager 의 패널/텍스트를 "읽어서 따라 그리는" 미러 방식 →
    /// 타이핑·스킵·이벤트 로직은 원본이 그대로 처리한다.
    /// </summary>
    public sealed class V5DialogueBand : MonoBehaviour
    {
        V5Settings _s;
        RectTransform _root, _content, _chevron, _tagRt;
        CanvasGroup _group;
        RawImage _portrait;
        TextMeshProUGUI _monogram, _name, _tag, _body;
        V5Shape _tick, _tagBg, _portraitFrame;

        // 원본
        GameObject _srcPanel;
        TMP_Text _srcBody, _srcName;
        object _srcManager;

        string _lastName, _lastBody;
        float _stableT, _openT, _vis, _nameFlashT = 99f;
        bool _open;

        public bool IsOpen => _open;

        public static V5DialogueBand Build(Transform canvas, V5Settings s)
        {
            var root = V5UI.Node(canvas, "DialogueBand", 0, 780, 1920, 300);
            root.anchorMin = new Vector2(0f, 0f); root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, 300f);
            var band = root.gameObject.AddComponent<V5DialogueBand>();
            band.Create(root, s);
            return band;
        }

        void Create(RectTransform root, V5Settings s)
        {
            _s = s; _root = root;
            _group = V5UI.Group(root.gameObject, 0f);

            var bg = V5UI.ShapeStretch(root, "Gradient", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Ink, 0f));
            bg.gradient = true; bg.gradientBottom = V5Theme.A(V5Theme.Ink, 0.94f);
            var bg2 = V5UI.Stretch(root, "Solid"); bg2.anchorMax = new Vector2(1f, 0.55f);
            var solid = bg2.gameObject.AddComponent<V5Shape>(); solid.raycastTarget = false;
            solid.color = V5Theme.A(V5Theme.Ink, 0f);
            solid.gradient = true; solid.gradientBottom = V5Theme.A(V5Theme.Ink, 0.6f);

            // 1280 콘텐츠 블록 — 화면 가운데 고정
            _content = V5UI.Node(root, "Content", 0, 0, 1920, 300);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0f);
            _content.pivot = new Vector2(0.5f, 0f);
            _content.anchoredPosition = Vector2.zero;

            // 초상 168 @ (320,96)
            var pf = V5UI.Node(_content, "Portrait", 320, 96, 168, 168);
            V5UI.ShapeStretch(pf, "Bg", V5Shape.Kind.Rect, V5Theme.Graphite);
            var ph = V5UI.ShapeStretch(pf, "Hatch", V5Shape.Kind.Hatch, V5Theme.A(V5Theme.Slate, 0.4f), 2);
            ph.spacing = 10; ph.stroke = 1;
            _monogram = V5UI.Text(pf, "Monogram", "", s.krBoldFont, 72, V5Theme.Steel, 0, 0, 168, 168, TextAlignmentOptions.Center);
            _portrait = V5UI.Raw(pf, "Face", 4, 4, 160, 160);
            _portrait.enabled = false;
            var pShade = V5UI.ShapeStretch(pf, "Shade", V5Shape.Kind.Rect, new Color(0, 0, 0, 0), 4);
            pShade.gradient = true; pShade.gradientBottom = V5Theme.A(V5Theme.Ink, 0.45f);
            _portraitFrame = V5UI.ShapeStretch(pf, "Frame", V5Shape.Kind.Rect, V5Theme.Paper, 0, 0, 2);
            V5UI.Shape(pf, "CornerTL", V5Shape.Kind.Rect, V5Theme.Signal, -4, -4, 14, 3);
            V5UI.Shape(pf, "CornerTL2", V5Shape.Kind.Rect, V5Theme.Signal, -4, -4, 3, 14);

            // 이름 줄
            _tick = V5UI.Shape(_content, "NameTick", V5Shape.Kind.Rect, V5Theme.Signal, 528, 102, 4, 26);
            _name = V5UI.Text(_content, "Name", "", s.krBoldFont, 28, V5Theme.Paper, 544, 96, 700, 36, TextAlignmentOptions.MidlineLeft);
            _tagRt = V5UI.Node(_content, "Tag", 700, 104, 70, 22);
            _tagBg = V5UI.ShapeStretch(_tagRt, "Bg", V5Shape.Kind.Rect, V5Theme.Steel, 0, 0, 1);
            _tag = V5UI.Text(_tagRt, "T", "", s.numberFont, 14, V5Theme.Mist, 0, 0, 70, 22, TextAlignmentOptions.Center);
            _tag.characterSpacing = 6;

            // 룰
            V5UI.Shape(_content, "Rule", V5Shape.Kind.Rect, V5Theme.A(V5Theme.Steel, 0.6f), 528, 146, 1072, 1);
            V5UI.Shape(_content, "RuleCap", V5Shape.Kind.Rect, V5Theme.Paper, 528, 145, 48, 3);

            // 본문 1072 폭
            _body = V5UI.Text(_content, "Body", "", s.krBodyFont, 30, V5Theme.Paper, 528, 162, 1072, 100, TextAlignmentOptions.TopLeft);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.lineSpacing = 24f;

            // 진행 셰브론
            var ch = V5UI.Shape(_content, "Chevron", V5Shape.Kind.TriDown, V5Theme.Signal, 1584, 254, 16, 10);
            _chevron = ch.rectTransform;
        }

        /// <summary>팀원 DialogueManager 를 연결 (private 필드는 읽기만).</summary>
        public void Bind(Component manager)
        {
            if (ReferenceEquals(_srcManager, manager)) return;
            _srcManager = manager;
            _srcPanel = V5Reflect.Get<GameObject>(manager, "_dialoguePanel");
            _srcBody = V5Reflect.Get<TMP_Text>(manager, "_displayText");
            _srcName = V5Reflect.Get<TMP_Text>(manager, "_speakerNameText");
            if (manager != null && (_srcPanel == null || _srcBody == null))
                Debug.LogWarning("[UI v5] DialogueManager 필드(_dialoguePanel/_displayText)를 못 찾아 대화 밴드를 끕니다. 기존 대화창이 그대로 보입니다.");
        }

        public bool IsBound => _srcPanel != null && _srcBody != null;
        public GameObject SourcePanel => _srcPanel;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool open = IsBound && _srcPanel.activeInHierarchy;
            if (open && !_open) { _openT = 0f; _lastName = null; _lastBody = null; }
            _open = open;
            _openT += dt;

            _vis = Mathf.MoveTowards(_vis, open ? 1f : 0f, dt * (open ? 6.5f : 8f));
            _group.alpha = _vis;
            float slide = V5Theme.OutCubic(_openT / 0.22f);
            _content.anchoredPosition = new Vector2(0f, open ? -(1f - slide) * 14f : 0f);
            if (!open) return;

            // 이름
            string nm = _srcName != null ? _srcName.text : "";
            if (nm != _lastName) { _lastName = nm; ApplySpeaker(nm); _nameFlashT = 0f; }
            _nameFlashT += dt;
            float tickH = 26f * V5Theme.OutCubic(_nameFlashT / 0.18f);
            _tick.rectTransform.sizeDelta = new Vector2(4f, tickH);

            // 본문 (원본 문자열 그대로 — 리치텍스트 태그 유지)
            string body = _srcBody.text;
            if (body != _lastBody) { _lastBody = body; _body.text = body; _stableT = 0f; }
            else _stableT += dt;

            // 입력 대기: 원본 _state.isWaitingForInput 을 읽고, 못 읽으면 "0.25초간 글자 변화 없음"으로 판단
            bool waiting;
            var state = V5Reflect.Get<object>(_srcManager, "_state");
            if (!V5Reflect.TryGetBool(state, "isWaitingForInput", out waiting))
                waiting = _stableT > 0.25f && !string.IsNullOrEmpty(body);
            _chevron.gameObject.SetActive(waiting);
            if (waiting) _chevron.anchoredPosition = new Vector2(1584f, -(254f + 3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f))));
        }

        void ApplySpeaker(string speaker)
        {
            _name.text = speaker;
            var style = _s.FindSpeaker(speaker) ?? DefaultStyle(speaker);
            Sprite sp = style != null ? style.portrait : null;
            if (sp != null) V5UI.SetSpriteCrop(_portrait, sp, style.portraitUV);
            else _portrait.enabled = false;
            _monogram.text = sp == null && !string.IsNullOrEmpty(speaker) ? speaker.Substring(0, 1) : "";

            string tag = style != null ? style.tag : "";
            _tagRt.gameObject.SetActive(!string.IsNullOrEmpty(tag));
            if (!string.IsNullOrEmpty(tag))
            {
                _tag.text = tag;
                float w = _tag.GetPreferredValues(tag).x + 20f;
                float nameW = _name.GetPreferredValues(speaker).x;
                V5UI.Place(_tagRt, 544f + nameW + 14f, 103f, w, 22f);
                ((RectTransform)_tag.transform).sizeDelta = new Vector2(w, 22f);
            }
            Color accent = style != null && style.accent.a > 0f ? style.accent : V5Theme.Signal;
            _tick.color = accent;
        }

        V5SpeakerStyle _tmpStyle;
        V5SpeakerStyle DefaultStyle(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            if (_tmpStyle == null) _tmpStyle = new V5SpeakerStyle();
            _tmpStyle.portrait = null;
            _tmpStyle.accent = new Color(0, 0, 0, 0);
            _tmpStyle.tag = "";
            if (speaker.Contains("제니스") || speaker == "플레이어")
            {
                _tmpStyle.portrait = _s.playerPortrait;
                _tmpStyle.portraitUV = _s.playerPortraitUV;
            }
            else if (speaker.Contains("용병")) { _tmpStyle.tag = "MERC"; _tmpStyle.accent = V5Theme.Danger; }
            else if (speaker.Contains("SYSTEM") || speaker.Contains("시스템")) { _tmpStyle.tag = "SYS"; _tmpStyle.accent = V5Theme.ChipBlue; }
            else if (speaker.Contains("?")) { _tmpStyle.tag = "UNKNOWN"; _tmpStyle.accent = V5Theme.Steel; }
            return _tmpStyle;
        }
    }
}
