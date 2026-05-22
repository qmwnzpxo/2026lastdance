using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueSystem
{
    /// <summary>
    /// NPC 위에 떠 있는 말풍선 UI.
    ///
    /// ─── 계층 구조 예시 ─────────────────────────────────────────────────────
    ///  Canvas (Screen Space – Overlay)
    ///   └── BubbleRoot  ← 이 컴포넌트를 여기에
    ///        ├── BubblePanel   (Image, 9-Slice 백그라운드)
    ///        │    ├── NameText (TMP_Text)
    ///        │    └── BodyText (TMP_Text)  ← TextEffectAnimator 자동 추가
    ///        ├── TailL         (Image, 꼬리 스프라이트 — 왼쪽용)
    ///        └── TailR         (Image, 꼬리 스프라이트 — 오른쪽용)
    ///
    /// ─── 말풍선 꼬리 방향 ───────────────────────────────────────────────────
    ///   NPC가 화면 왼쪽 절반 → 꼬리 왼쪽 아래
    ///   NPC가 화면 오른쪽 절반 → 꼬리 오른쪽 아래
    ///   NPC가 화면 위쪽에 있으면 꼬리 위로 전환
    ///
    /// ─── 위치 계산 ─────────────────────────────────────────────────────────
    ///   매 프레임 Camera.WorldToScreenPoint → Canvas 내 RectTransform 위치로 변환
    /// </summary>
    public class DialogueBubbleUI : MonoBehaviour
    {
        // ── 인스펙터 ────────────────────────────────────────────────────────────

        [Header("루트 캔버스")]
        [Tooltip("이 말풍선이 속한 Screen Space Canvas")]
        [SerializeField] private Canvas rootCanvas;

        [Header("말풍선 패널")]
        [SerializeField] private RectTransform bubbleRoot;   // 위치 이동 대상
        [SerializeField] private RectTransform bubblePanel;  // 크기 자동 조절
        [SerializeField] private TMP_Text      nameText;
        [SerializeField] private TMP_Text      bodyText;

        [Header("꼬리 이미지")]
        [Tooltip("꼬리가 왼쪽 아래를 향하는 Image")]
        [SerializeField] private Image tailLeft;
        [Tooltip("꼬리가 오른쪽 아래를 향하는 Image")]
        [SerializeField] private Image tailRight;
        [Tooltip("꼬리가 위를 향하는 Image (NPC가 화면 아래쪽일 때)")]
        [SerializeField] private Image tailUp;

        [Header("레이아웃")]
        [Tooltip("텍스트 영역 좌우 내부 패딩")]
        [SerializeField] private float paddingH = 24f;
        [Tooltip("텍스트 영역 상하 내부 패딩")]
        [SerializeField] private float paddingV = 16f;
        [Tooltip("말풍선 최대 가로 폭")]
        [SerializeField] private float maxBubbleWidth = 320f;
        [Tooltip("NPC 앵커에서 말풍선 패널까지 추가 오프셋 (픽셀)")]
        [SerializeField] private Vector2 bubbleOffset = new(0f, 24f);
        [Tooltip("화면 경계 여백 (픽셀)")]
        [SerializeField] private float screenMargin = 16f;

        [Header("애니메이션")]
        [SerializeField] private float popDuration   = 0.12f;
        [SerializeField] private float fadeDuration  = 0.08f;

        [Header("타이프라이터 속도")]
        [SerializeField] private float defaultSpeed   = 0.04f;
        [SerializeField] private float fastMultiplier = 0.25f;
        [SerializeField] private float slowMultiplier = 3f;

        // ── 런타임 상태 ────────────────────────────────────────────────────────

        private TextEffectAnimator _animator;
        private CanvasGroup        _canvasGroup;
        private Camera             _cam;
        private DialogueSpeaker    _currentSpeaker;
        private DialogueManager    _manager;
        private Coroutine          _typeCoroutine;
        private Coroutine          _animCoroutine;
        private ParsedDialogueText _parsed;
        private bool               _typingDone;

        public bool IsTyping => _typeCoroutine != null && !_typingDone;

        // ── 생명주기 ───────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam         = Camera.main;
            _canvasGroup = bubbleRoot.GetComponent<CanvasGroup>()
                        ?? bubbleRoot.gameObject.AddComponent<CanvasGroup>();
            _animator    = bodyText.GetComponent<TextEffectAnimator>()
                        ?? bodyText.gameObject.AddComponent<TextEffectAnimator>();

            _canvasGroup.alpha = 0f;
            bubbleRoot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_currentSpeaker != null && bubbleRoot.gameObject.activeSelf)
                RepositionBubble();
        }

        // ── 공개 API ───────────────────────────────────────────────────────────

        public void ShowBubble(bool show)
        {
            if (!show)
            {
                StopAllCoroutines();
                _currentSpeaker = null;
                bubbleRoot.gameObject.SetActive(false);
                return;
            }
            bubbleRoot.gameObject.SetActive(true);
        }

        public void DisplayEntry(DialogueEntry entry, DialogueSpeaker speaker,
                                  DialogueManager manager)
        {
            _currentSpeaker = speaker;
            _manager        = manager;

            // 화자 이름
            if (nameText != null)
                nameText.text = entry.speaker;

            // 텍스트 파싱 & 이펙트
            _parsed = TextEffectParser.Parse(entry.rawText);
            _animator.SetEffects(_parsed.effects);

            // 크기 먼저 업데이트 (레이아웃 계산용)
            bodyText.text               = _parsed.cleanText;
            bodyText.maxVisibleCharacters = 0;
            ResizeBubble();

            // 위치
            RepositionBubble();

            // 팝 애니메이션
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PopAnim());

            // 타이프라이터
            if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
            _typeCoroutine = StartCoroutine(TypewriterRoutine(entry));
        }

        public void SkipTypewriter()
        {
            if (_typeCoroutine != null) { StopCoroutine(_typeCoroutine); _typeCoroutine = null; }
            bodyText.maxVisibleCharacters = int.MaxValue;
            _typingDone = true;
        }

        // ── 위치 & 크기 ────────────────────────────────────────────────────────

        private void RepositionBubble()
        {
            if (_currentSpeaker == null || _cam == null) return;

            // 월드 → 스크린 → 캔버스 로컬
            var worldPos   = _currentSpeaker.GetAnchorWorldPos();
            var screenPos  = _cam.WorldToScreenPoint(worldPos);

            // 화면 밖이면 숨김
            bool offscreen = screenPos.z < 0f
                          || screenPos.x < 0 || screenPos.x > Screen.width
                          || screenPos.y < 0 || screenPos.y > Screen.height;
            _canvasGroup.alpha = offscreen ? 0f : 1f;
            if (offscreen) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                screenPos, rootCanvas.worldCamera,
                out var localPos);

            // 오프셋 적용
            localPos += bubbleOffset;

            // 화면 경계 클램프
            var half = bubblePanel.rect.size * 0.5f;
            var canvasSize = rootCanvas.GetComponent<RectTransform>().rect.size * 0.5f;
            localPos.x = Mathf.Clamp(localPos.x,
                -canvasSize.x + half.x + screenMargin,
                 canvasSize.x - half.x - screenMargin);
            localPos.y = Mathf.Clamp(localPos.y,
                -canvasSize.y + half.y + screenMargin,
                 canvasSize.y - half.y - screenMargin);

            bubbleRoot.localPosition = localPos;

            // 꼬리 방향 결정
            UpdateTail(screenPos, localPos);
        }

        private void UpdateTail(Vector3 screenPos, Vector2 bubbleLocalPos)
        {
            // NPC가 화면 아래쪽 절반이면 꼬리 위 방향 (말풍선이 NPC 위에 있을 때 기본)
            bool npcBelowCenter = screenPos.y < Screen.height * 0.4f;
            bool npcOnLeft      = screenPos.x < Screen.width * 0.5f;

            if (tailLeft  != null) tailLeft.gameObject.SetActive(false);
            if (tailRight != null) tailRight.gameObject.SetActive(false);
            if (tailUp    != null) tailUp.gameObject.SetActive(false);

            if (npcBelowCenter)
            {
                // 말풍선이 NPC 위에 → 꼬리 아래 (L/R)
                if (npcOnLeft)
                    { if (tailLeft  != null) tailLeft.gameObject.SetActive(true); }
                else
                    { if (tailRight != null) tailRight.gameObject.SetActive(true); }
            }
            else
            {
                // 말풍선이 NPC 아래에 → 꼬리 위
                if (tailUp != null) tailUp.gameObject.SetActive(true);
            }
        }

        private void ResizeBubble()
        {
            // 텍스트 선호 크기로 패널 크기 조정
            bodyText.ForceMeshUpdate();
            var preferred = bodyText.GetPreferredValues(
                _parsed.cleanText, maxBubbleWidth - paddingH * 2, 0f);

            float w = Mathf.Min(preferred.x + paddingH * 2, maxBubbleWidth);
            float h = preferred.y + paddingV * 2
                    + (nameText != null && !string.IsNullOrEmpty(nameText.text)
                        ? nameText.rectTransform.rect.height + 4f : 0f);

            bubblePanel.sizeDelta = new Vector2(w, h);
        }

        // ── 타이프라이터 ───────────────────────────────────────────────────────

        private IEnumerator TypewriterRoutine(DialogueEntry entry)
        {
            _typingDone = false;
            bodyText.text = _parsed.cleanText;
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = 0;

            int total    = bodyText.textInfo.characterCount;
            int pausePtr = 0;

            for (int i = 0; i < total; i++)
            {
                while (pausePtr < _parsed.pauses.Count &&
                       _parsed.pauses[pausePtr].charIdx <= i)
                {
                    yield return new WaitForSeconds(_parsed.pauses[pausePtr].duration);
                    pausePtr++;
                }

                bodyText.maxVisibleCharacters = i + 1;

                // 글자 추가될 때마다 크기 재계산 (천천히 늘어나는 느낌)
                ResizeBubble();

                yield return new WaitForSeconds(GetDelay(i));
            }

            _typingDone    = true;
            _typeCoroutine = null;

            if (entry.autoAdvanceTime > 0f)
            {
                yield return new WaitForSeconds(entry.autoAdvanceTime);
                _manager?.AdvanceDialogue();
            }
        }

        private float GetDelay(int ci)
        {
            foreach (var r in _parsed.effects)
            {
                if (ci >= r.startChar && ci <= r.endChar)
                {
                    if (r.type == TextEffectType.Typefast) return defaultSpeed * fastMultiplier;
                    if (r.type == TextEffectType.Typeslow) return defaultSpeed * slowMultiplier;
                }
            }
            return defaultSpeed;
        }

        // ── 팝 애니메이션 ──────────────────────────────────────────────────────

        private IEnumerator PopAnim()
        {
            // Fade in + 작게 → 원래 크기
            _canvasGroup.alpha = 0f;
            bubblePanel.localScale = Vector3.one * 0.7f;

            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / popDuration);
                _canvasGroup.alpha         = p;
                bubblePanel.localScale     = Vector3.Lerp(Vector3.one * 0.7f, Vector3.one, p);
                yield return null;
            }
            _canvasGroup.alpha     = 1f;
            bubblePanel.localScale = Vector3.one;
        }
    }
}
