using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 UI 전체를 제어합니다.
    /// Canvas 하위에 배치하고 Inspector에서 각 레퍼런스를 연결하세요.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private CanvasGroup dialoguePanel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject nextArrow;  // 타이핑 완료 시 표시할 ▼ 아이콘

        [Header("Choice Panel")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button choiceButtonPrefab;
        [SerializeField] private Transform choiceContainer;

        [Header("Effects")]
        [SerializeField] private TextEffectAnimator effectAnimator;

        private readonly List<Button> _choiceBtns = new();

        void Awake()
        {
            nextButton.onClick.AddListener(() => DialogueManager.Instance.OnClickNext());
            Hide();
        }

        // ─── Public API ───────────────────────────────────────────

        public void Show()
        {
            dialoguePanel.gameObject.SetActive(true);
            dialoguePanel.alpha          = 1f;
            dialoguePanel.blocksRaycasts = true;
            dialoguePanel.interactable   = true;
        }

        public void Hide()
        {
            dialoguePanel.gameObject.SetActive(false);
            effectAnimator.Clear();
        }

        public void SetSpeaker(string speaker, string portraitName)
        {
            nameText.text = string.IsNullOrEmpty(speaker) ? "" : speaker;

            bool hasPortrait = !string.IsNullOrEmpty(portraitName);
            portraitImage.gameObject.SetActive(hasPortrait);

            if (hasPortrait)
            {
                var sp = Resources.Load<Sprite>($"Portraits/{portraitName}");
                if (sp != null) portraitImage.sprite = sp;
                else Debug.LogWarning($"[DialogueUI] 초상화 없음: Resources/Portraits/{portraitName}");
            }
        }

        public void PrepareText(string tmpText, List<EffectRange> effects)
        {
            bodyText.text                = tmpText;
            bodyText.maxVisibleCharacters = 0;
            nextArrow.SetActive(false);
            bodyText.ForceMeshUpdate();
            effectAnimator.SetEffectRanges(effects, bodyText);
        }

        public void SetVisible(int count)
        {
            bodyText.maxVisibleCharacters = count;
            nextArrow.SetActive(count >= bodyText.textInfo.characterCount);
        }

        public void ShowChoices(List<ChoiceEntry> choices, Action<string> onSelect)
        {
            HideChoices();
            nextArrow.SetActive(false);
            choicePanel.SetActive(true);

            foreach (var c in choices)
            {
                var btn = Instantiate(choiceButtonPrefab, choiceContainer);
                btn.GetComponentInChildren<TextMeshProUGUI>().text = c.Label;
                string id = c.TargetId;
                btn.onClick.AddListener(() => onSelect(id));
                _choiceBtns.Add(btn);
            }
        }

        public void HideChoices()
        {
            foreach (var b in _choiceBtns)
                if (b != null) Destroy(b.gameObject);
            _choiceBtns.Clear();
            choicePanel.SetActive(false);
        }
    }
}
