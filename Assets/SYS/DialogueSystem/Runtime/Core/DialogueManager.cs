using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 시스템의 핵심 싱글톤.
    ///
    /// ─── UI 모드 ─────────────────────────────────────────────────────────────
    ///  • Panel  → 하단 고정 패널 (컷씬, 긴 스토리)
    ///  • Bubble → NPC 위 말풍선 (상호작용 대화)
    ///  • Auto   → StartDialogue에 speaker 전달 시 Bubble, 없으면 Panel
    ///
    /// ─── 호출 방법 ───────────────────────────────────────────────────────────
    ///  // 패널 (컷씬 등)
    ///  DialogueManager.Instance.StartDialogue("ID");
    ///
    ///  // 말풍선 (NPC 상호작용)
    ///  DialogueManager.Instance.StartDialogue("ID", speakerComponent);
    ///
    ///  // NPC 컴포넌트에서 바로
    ///  GetComponent<DialogueSpeaker>().Interact();
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // ── 싱글톤 ─────────────────────────────────────────────────────────────
        public static DialogueManager Instance { get; private set; }

        // ── 인스펙터 ────────────────────────────────────────────────────────────
        [Header("데이터")]
        [SerializeField] private DialogueDatabase database;

        [Header("UI — 패널 (하단 고정)")]
        [SerializeField] private DialogueUI panelUI;

        [Header("UI — 말풍선 (NPC 위)")]
        [SerializeField] private DialogueBubbleUI bubbleUI;

        [Header("기본 모드 (speaker 없이 호출 시)")]
        [SerializeField] private DialogueMode defaultMode = DialogueMode.Panel;

        [Header("CSV 자동 로드")]
        [SerializeField] private bool   autoLoadCSV     = false;
        [SerializeField] private string csvResourcePath = "Dialogue/main";

        [Header("이벤트")]
        public UnityEvent            OnDialogueStart;
        public UnityEvent            OnDialogueEnd;
        public UnityEvent<string>    OnFlagSet;

        /// <summary>대화 종료 시 발생 (Timeline 등 외부 시스템용)</summary>
        public event Action OnDialogueComplete;

        // ── 런타임 상태 ────────────────────────────────────────────────────────
        private DialogueEntry   _current;
        private DialogueSpeaker _currentSpeaker;
        private DialogueMode    _activeMode;
        private bool            _isRunning;

        private readonly Dictionary<string, bool> _flags = new();

        public bool          IsRunning      => _isRunning;
        public DialogueMode  ActiveMode     => _activeMode;
        public DialogueSpeaker CurrentSpeaker => _currentSpeaker;

        // ── 생명주기 ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            database?.RebuildLookup();
        }

        private void Start()
        {
            if (autoLoadCSV && !string.IsNullOrEmpty(csvResourcePath))
                LoadCSVFromResources(csvResourcePath);
        }

        // ── 공개 API : CSV 로드 ────────────────────────────────────────────────

        public void LoadCSVFromResources(string path)
        {
            EnsureDB();
            database.SetEntries(CSVDialogueLoader.LoadFromResources(path));
            database.RebuildLookup();
        }

        public void LoadCSVFromFile(string absolutePath)
        {
            EnsureDB();
            database.SetEntries(CSVDialogueLoader.LoadFromFile(absolutePath));
            database.RebuildLookup();
        }

        public void LoadCSVFromText(string csvText)
        {
            EnsureDB();
            database.SetEntries(CSVDialogueLoader.LoadFromText(csvText));
            database.RebuildLookup();
        }

        // ── 공개 API : 대화 제어 ──────────────────────────────────────────────

        /// <summary>
        /// 대화 시작.
        /// speaker가 null이면 defaultMode 사용.
        /// speaker가 있으면 speaker.preferredMode 사용 (Auto면 Bubble).
        /// </summary>
        public void StartDialogue(string startId, DialogueSpeaker speaker = null)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[DialogueManager] 이미 대화 중입니다.");
                return;
            }

            var entry = database.GetEntry(startId);
            if (entry == null)
            {
                Debug.LogError($"[DialogueManager] ID 없음: '{startId}'");
                return;
            }

            _currentSpeaker = speaker;
            _activeMode     = ResolveMode(speaker);
            _isRunning      = true;

            OnDialogueStart?.Invoke();
            ActivateUI(true);
            ShowEntry(entry);
        }

        /// <summary>
        /// 다음으로 진행.
        /// 타이프라이터 재생 중이면 즉시 완성, 완성 상태면 다음 줄.
        /// </summary>
        public void AdvanceDialogue()
        {
            if (!_isRunning) return;

            // 타이프라이터 스킵
            bool typing = _activeMode == DialogueMode.Bubble
                ? bubbleUI != null && bubbleUI.IsTyping
                : panelUI  != null && panelUI.IsTyping;

            if (typing)
            {
                if (_activeMode == DialogueMode.Bubble) bubbleUI?.SkipTypewriter();
                else                                    panelUI?.SkipTypewriter();
                return;
            }

            // 선택지 대기 중
            if (_current?.choices != null && _current.choices.Count > 0) return;

            MoveToNext(_current?.nextId);
        }

        public void SelectChoice(int index)
        {
            if (_current?.choices == null || index >= _current.choices.Count) return;
            MoveToNext(_current.choices[index].nextId);
        }

        public void EndDialogue()
        {
            _isRunning      = false;
            _current        = null;
            _currentSpeaker = null;

            ActivateUI(false);
            OnDialogueEnd?.Invoke();
            OnDialogueComplete?.Invoke();
        }

        // ── 공개 API : 플래그 ─────────────────────────────────────────────────

        public void SetFlag(string key, bool value = true)
        {
            _flags[key] = value;
            OnFlagSet?.Invoke(key);
        }

        public bool GetFlag(string key) =>
            !string.IsNullOrEmpty(key) && _flags.TryGetValue(key, out var v) && v;

        public void ClearAllFlags() => _flags.Clear();

        // ── 내부 ───────────────────────────────────────────────────────────────

        private void MoveToNext(string nextId)
        {
            if (string.IsNullOrEmpty(nextId)) { EndDialogue(); return; }
            var next = database.GetEntry(nextId);
            if (next == null) { Debug.LogWarning($"[DialogueManager] 다음 ID 없음: '{nextId}'"); EndDialogue(); return; }
            ShowEntry(next);
        }

        private void ShowEntry(DialogueEntry entry)
        {
            // 조건 체크
            if (!string.IsNullOrEmpty(entry.conditionFlag) && !GetFlag(entry.conditionFlag))
            {
                MoveToNext(entry.nextId);
                return;
            }

            // 선택지 필터
            if (entry.choices != null)
                entry.choices.RemoveAll(c =>
                    !string.IsNullOrEmpty(c.conditionFlag) && !GetFlag(c.conditionFlag));

            // 플래그 세팅
            if (!string.IsNullOrEmpty(entry.setFlag))
                SetFlag(entry.setFlag);

            _current = entry;

            // 현재 모드에 맞는 UI에 전달
            if (_activeMode == DialogueMode.Bubble)
                bubbleUI?.DisplayEntry(entry, _currentSpeaker, this);
            else
                panelUI?.DisplayEntry(entry, this);
        }

        private void ActivateUI(bool on)
        {
            if (_activeMode == DialogueMode.Bubble)
            {
                bubbleUI?.ShowBubble(on);
                panelUI?.ShowPanel(false);
            }
            else
            {
                panelUI?.ShowPanel(on);
                bubbleUI?.ShowBubble(false);
            }
        }

        private DialogueMode ResolveMode(DialogueSpeaker speaker)
        {
            if (speaker == null) return defaultMode == DialogueMode.Auto
                                        ? DialogueMode.Panel : defaultMode;

            return speaker.preferredMode == DialogueMode.Auto
                ? DialogueMode.Bubble
                : speaker.preferredMode;
        }

        private void EnsureDB()
        {
            if (database == null)
                Debug.LogError("[DialogueManager] DialogueDatabase가 없습니다. 인스펙터에서 할당하세요.");
        }
    }
}
