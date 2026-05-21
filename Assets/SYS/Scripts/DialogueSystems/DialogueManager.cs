using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 시스템 핵심 컨트롤러 (Singleton).
    /// CommandHandler 기능 통합 — Inspector에서 각 Command 이벤트를 연결하세요.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────

        public static DialogueManager Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────

        [Header("Settings")]
        [Tooltip("글자당 타이핑 딜레이 (초). 낮을수록 빠름.")]
        [SerializeField] private float defaultTypeSpeed = 0.035f;

        [Header("References")]
        [SerializeField] private DialogueUI ui;

        [Header("Dialogue Events")]
        public UnityEvent OnDialogueStart = new();
        public UnityEvent OnDialogueEnd   = new();

        // ─── Command Events ───────────────────────────────────────
        // CSV command 컬럼에 아래 명령어를 입력하면 해당 이벤트가 발동됩니다.
        //
        //   FADE_IN          OnFadeIn 발동
        //   FADE_OUT         OnFadeOut 발동
        //   SHAKE_CAM        OnShakeCam 발동
        //   PLAY_SFX:이름    OnPlaySFX("이름") 발동
        //   SET_FLAG:이름    OnSetFlag("이름") 발동
        //   LOAD_SCENE:이름  OnLoadScene("이름") 발동

        [Header("Command Events")]
        public UnityEvent           OnFadeIn;
        public UnityEvent           OnFadeOut;
        public UnityEvent           OnShakeCam;
        public UnityEvent<string>   OnPlaySFX;
        public UnityEvent<string>   OnSetFlag;
        public UnityEvent<string>   OnLoadScene;
        public UnityEvent<string>   OnCustomCommand; // 위에 없는 명령어는 여기로

        // ─── State ────────────────────────────────────────────────

        private DialogueDatabase _db;
        private DialogueEntry    _current;
        private Coroutine        _typeRoutine;
        private bool             _isTyping;
        private bool             _skipRequested;

        // ─── Lifecycle ────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>지정한 id부터 대화를 시작합니다.</summary>
        public void StartDialogue(DialogueDatabase db, string startId)
        {
            if (db == null) { Debug.LogError("[DialogueManager] database가 null입니다."); return; }
            _db = db;
            OnDialogueStart.Invoke();
            ui.Show();
            ShowEntry(startId);
        }

        /// <summary>DB 첫 번째 항목부터 시작합니다.</summary>
        public void StartDialogue(DialogueDatabase db) => StartDialogue(db, db.FirstId());

        /// <summary>다음 버튼 클릭 / E키 입력 시 호출. 타이핑 중이면 스킵.</summary>
        public void OnClickNext()
        {
            if (_isTyping) { _skipRequested = true; return; }
            Advance();
        }

        // ─── Dialogue Flow ────────────────────────────────────────

        void ShowEntry(string id)
        {
            _current = _db.Get(id);
            if (_current == null) { EndDialogue(); return; }

            if (!string.IsNullOrEmpty(_current.Command))
                ExecuteCommand(_current.Command);

            ui.SetSpeaker(_current.Speaker, _current.Portrait);

            float speed = _current.SpeedOverride > 0 ? _current.SpeedOverride : defaultTypeSpeed;
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = StartCoroutine(TypeRoutine(_current.RawText, speed));
        }

        IEnumerator TypeRoutine(string rawText, float speed)
        {
            _isTyping      = true;
            _skipRequested = false;

            var processed = DialogueParser.ProcessTags(rawText);
            ui.PrepareText(processed.TmpText, processed.EffectRanges);

            int total = processed.VisibleLength;
            for (int i = 1; i <= total; i++)
            {
                // 스킵 요청 시 즉시 전체 표시 후 루프 탈출 (yield 대기 없음)
                if (_skipRequested) { ui.SetVisible(total); break; }

                ui.SetVisible(i);

                if (processed.PauseAt.TryGetValue(i, out float pauseSec))
                    yield return new WaitForSeconds(pauseSec);
                else
                    yield return new WaitForSeconds(speed);
            }

            ui.SetVisible(total);
            _isTyping = false;

            if (_current.Choices is { Count: > 0 })
            {
                ui.ShowChoices(_current.Choices, OnChoiceSelected);
                yield break;
            }

            if (_current.AutoDelay > 0)
            {
                yield return new WaitForSeconds(_current.AutoDelay);
                Advance();
            }
        }

        void Advance()
        {
            if (string.IsNullOrEmpty(_current.NextId)) EndDialogue();
            else ShowEntry(_current.NextId);
        }

        void OnChoiceSelected(string targetId)
        {
            ui.HideChoices();
            ShowEntry(targetId);
        }

        void EndDialogue()
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = null;
            ui.Hide();
            OnDialogueEnd.Invoke();
        }

        // ─── Command 처리 (구 CommandHandler) ─────────────────────

        void ExecuteCommand(string command)
        {
            var idx = command.IndexOf(':');
            string cmd = (idx >= 0 ? command.Substring(0, idx) : command).Trim().ToUpper();
            string arg = idx >= 0 ? command.Substring(idx + 1).Trim() : "";

            switch (cmd)
            {
                case "FADE_IN":    OnFadeIn?.Invoke();        break;
                case "FADE_OUT":   OnFadeOut?.Invoke();       break;
                case "SHAKE_CAM":  OnShakeCam?.Invoke();      break;
                case "PLAY_SFX":   OnPlaySFX?.Invoke(arg);    break;
                case "SET_FLAG":   OnSetFlag?.Invoke(arg);    break;
                case "LOAD_SCENE": OnLoadScene?.Invoke(arg);  break;
                default:
                    Debug.LogWarning($"[DialogueManager] 처리되지 않은 command: '{command}'");
                    OnCustomCommand?.Invoke(command);
                    break;
            }
        }
    }
}
