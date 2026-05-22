using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 UI 컨트롤러.
    /// ─ 타이프라이터 (텍스트 효과 포함)
    /// ─ 화자 이름 / 포트레이트 이미지
    /// ─ 선택지 버튼 (최대 4개)
    ///
    /// DialogueManager와 함께 같은 씬에 두고 인스펙터에서 연결.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        // ── 인스펙터 참조 ───────────────────────────────────────────────────────

        [Header("대화창 루트")]
        [SerializeField] private GameObject dialoguePanel;

        [Header("화자 정보")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image    portraitImage;
        [Tooltip("Resources/ 아래 포트레이트 폴더 경로 (끝에 / 포함)")]
        [SerializeField] private string   portraitFolder = "Portraits/";

        [Header("대화 텍스트")]
        [SerializeField] private TMP_Text dialogueText;

        [Header("선택지 (최대 4개)")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button[]   choiceButtons  = new Button[4];
        [SerializeField] private TMP_Text[] choiceLabels   = new TMP_Text[4];

        [Header("진행 버튼 / 클릭 영역")]
        [Tooltip("클릭 시 AdvanceDialogue() 호출. 빈 투명 버튼도 가능.")]
        [SerializeField] private Button advanceButton;

        [Header("타이프라이터 속도")]
        [Tooltip("글자당 출력 딜레이 (초). 낮을수록 빠름.")]
        [SerializeField] private float defaultSpeed  = 0.04f;
        [Tooltip("[fast] 태그 적용 시 배율 (0~1)")]
        [SerializeField] private float fastMultiplier = 0.25f;
        [Tooltip("[slow] 태그 적용 시 배율 (>1)")]
        [SerializeField] private float slowMultiplier = 3f;

        // ── 런타임 상태 ────────────────────────────────────────────────────────

        private TextEffectAnimator _animator;
        private Coroutine          _typeCoroutine;
        private ParsedDialogueText _parsed;
        private DialogueManager    _manager;
        private bool               _typingDone;

        public bool IsTyping => _typeCoroutine != null && !_typingDone;

        // ── 생명주기 ───────────────────────────────────────────────────────────

        private void Awake()
        {
            // TextEffectAnimator는 dialogueText에 자동 추가
            _animator = dialogueText.GetComponent<TextEffectAnimator>()
                     ?? dialogueText.gameObject.AddComponent<TextEffectAnimator>();

            if (advanceButton != null)
                advanceButton.onClick.AddListener(OnAdvanceClicked);
        }

        // ── 공개 API ───────────────────────────────────────────────────────────

        public void ShowPanel(bool show)
        {
            dialoguePanel?.SetActive(show);
            if (!show) StopAllCoroutines();
        }

        /// <summary>DialogueManager에서 새 엔트리를 보여줄 때 호출</summary>
        public void DisplayEntry(DialogueEntry entry, DialogueManager manager)
        {
            _manager = manager;

            // 이름
            if (nameText != null)
                nameText.text = entry.speaker;

            // 포트레이트
            SetPortrait(entry.portraitKey);

            // 텍스트 파싱 → 타이프라이터 시작
            _parsed = TextEffectParser.Parse(entry.rawText);
            _animator.SetEffects(_parsed.effects);

            if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
            _typeCoroutine = StartCoroutine(TypewriterRoutine(entry));

            // 선택지
            RefreshChoices(entry);
        }

        /// <summary>타이프라이터 재생 중이면 즉시 완성</summary>
        public void SkipTypewriter()
        {
            if (_typeCoroutine != null)
            {
                StopCoroutine(_typeCoroutine);
                _typeCoroutine = null;
            }
            dialogueText.maxVisibleCharacters = int.MaxValue;
            _typingDone = true;
        }

        // ── 타이프라이터 ───────────────────────────────────────────────────────

        private IEnumerator TypewriterRoutine(DialogueEntry entry)
        {
            _typingDone = false;
            dialogueText.text = _parsed.cleanText;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = 0;

            int total    = dialogueText.textInfo.characterCount;
            int pausePtr = 0;

            for (int i = 0; i < total; i++)
            {
                // [pause=N] 체크
                while (pausePtr < _parsed.pauses.Count &&
                       _parsed.pauses[pausePtr].charIdx <= i)
                {
                    yield return new WaitForSeconds(_parsed.pauses[pausePtr].duration);
                    pausePtr++;
                }

                dialogueText.maxVisibleCharacters = i + 1;
                yield return new WaitForSeconds(GetDelay(i));
            }

            _typingDone    = true;
            _typeCoroutine = null;

            // 자동 진행
            if (entry.autoAdvanceTime > 0f)
            {
                yield return new WaitForSeconds(entry.autoAdvanceTime);
                _manager?.AdvanceDialogue();
            }
        }

        private float GetDelay(int charIdx)
        {
            foreach (var r in _parsed.effects)
            {
                if (charIdx >= r.startChar && charIdx <= r.endChar)
                {
                    if (r.type == TextEffectType.Typefast) return defaultSpeed * fastMultiplier;
                    if (r.type == TextEffectType.Typeslow) return defaultSpeed * slowMultiplier;
                }
            }
            return defaultSpeed;
        }

        // ── 선택지 ─────────────────────────────────────────────────────────────

        private void RefreshChoices(DialogueEntry entry)
        {
            bool hasChoices = entry.choices != null && entry.choices.Count > 0;
            choicePanel?.SetActive(hasChoices);

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;

                if (hasChoices && i < entry.choices.Count)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    if (i < choiceLabels.Length && choiceLabels[i] != null)
                        choiceLabels[i].text = entry.choices[i].text;

                    int idx = i; // 클로저 캡처
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => _manager?.SelectChoice(idx));
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // ── 포트레이트 ─────────────────────────────────────────────────────────

        private void SetPortrait(string key)
        {
            if (portraitImage == null || string.IsNullOrEmpty(key)) return;
            var sprite = Resources.Load<Sprite>(portraitFolder + key);
            if (sprite != null)
            {
                portraitImage.sprite  = sprite;
                portraitImage.enabled = true;
            }
            else
            {
                portraitImage.enabled = false;
                Debug.LogWarning($"[DialogueUI] 포트레이트 없음: {portraitFolder}{key}");
            }
        }

        // ── UI 이벤트 ──────────────────────────────────────────────────────────

        private void OnAdvanceClicked()
        {
            _manager?.AdvanceDialogue();
        }
    }
}
