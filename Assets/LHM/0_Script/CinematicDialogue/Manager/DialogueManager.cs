using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace DialogSystem.CinematicDialogue
{
    /// <summary>
    /// 시네마틱 대화 시스템의 핵심 매니저.
    /// JSON 데이터를 로드하고, DialogueParser로 텍스트를 파싱한 후,
    /// UniTask 기반의 비동기 루프로 텍스트를 한 글자씩 출력하며 커맨드를 처리한다.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private TextAsset _dialogueJsonAsset;  // JSON 리소스
        [SerializeField] private TextMeshProUGUI _displayText;  // 텍스트 표시용 TMP
        [SerializeField] private TextMeshProUGUI _speakerNameText;  // 화자 이름 표시용

        private DialogueDatabase _database;
        private DialoguePlaybackState _state;
        private CancellationTokenSource _playbackCts;

        // 콜백 및 이벤트 핸들
        public System.Action<string> onEventTriggered;  // [event:xxx] 처리
        public System.Action<string> onJumpRequested;   // [jump:xxx] 처리
        public System.Action onDialogueCompleted;      // 대화 완료 시

        // 타이핑 설정
        private float _currentTypeSpeed = 0.05f;  // 기본 타이핑 속도 (글자당 초)
        private const float DEFAULT_TYPE_SPEED = 0.05f;

        // 싱글톤 설정
        public static DialogueManager Instance { get; private set; }

        private void Awake()
        {
            //싱글톤 패턴(선택사항)
             if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _state = new DialoguePlaybackState();
            LoadDatabase();
        }

        /// <summary>
        /// JSON 파일에서 대화 데이터베이스를 로드한다.
        /// </summary>
        private void LoadDatabase()
        {
            if (_dialogueJsonAsset == null)
            {
                Debug.LogError("[DialogueManager] Dialogue JSON 에셋이 할당되지 않음");
                return;
            }

            try
            {
                string json = _dialogueJsonAsset.text;
                // JSON 배열을 수용하기 위해 래핑
                string wrappedJson = $"{{\"dialogues\":{json}}}";
                _database = JsonUtility.FromJson<DialogueDatabase>(wrappedJson);
                Debug.Log($"[DialogueManager] 데이터베이스 로드 완료: {_database.dialogues.Count}개 대사");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueManager] JSON 파싱 오류: {e.Message}");
            }
        }

        /// <summary>
        /// 주어진 ID의 대사부터 재생을 시작한다.
        /// </summary>
        public async UniTask PlayDialogueAsync
            (string dialogueId, Transform spawnPoint = null, CancellationToken externalCt = default)
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _playbackCts.Token;

            // 말풍선 등장 위치
            if (spawnPoint != null && _displayText != null)
            {
                Canvas canvas = _displayText.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.transform.position = spawnPoint.position;
            }

            DialogueEntry entry = _database.FindDialogue(dialogueId);
            if (entry == null)
            {
                Debug.LogError($"[DialogueManager] 대사 ID '{dialogueId}'를 찾을 수 없음");
                return;
            }

            try
            {
                await PlayDialogueInternalAsync(entry, ct);
            }
            finally
            {
                _playbackCts?.Dispose();
                _playbackCts = null;
            }
        }

        /// <summary>
        /// 대사 재생의 실제 구현.
        /// DialogueEntry의 텍스트를 파싱하고 커맨드를 순차 실행한다.
        /// </summary>
        private async UniTask PlayDialogueInternalAsync(DialogueEntry entry, CancellationToken ct)
        {
            _state.currentEntry = entry;
            _state.isWaitingForInput = false;
            _state.isPaused = false;

            // UI에 화자 이름 표시
            if (_speakerNameText != null)
            {
                _speakerNameText.text = entry.speakerName ?? "Unknown";
            }

            // 텍스트 필드 초기화
            if (_displayText != null)
            {
                _displayText.text = "";
            }

            // 타이핑 속도 리셋
            _currentTypeSpeed = DEFAULT_TYPE_SPEED;

            // 텍스트 파싱: 순수 텍스트와 커맨드를 분리
            List<DialogueCommand> commands = DialogueParser.Parse(entry.text);

            // 디버그 출력 (필요시 주석 해제)
            // DialogueParser.DebugPrintCommands(commands);

            // 커맨드 순차 실행
            await ExecuteCommandsAsync(commands, ct);

            // 대화 완료 콜백
            onDialogueCompleted?.Invoke();

            // 점프 명령이 있었는지 확인
            string jumpTarget = FindJumpTarget(commands);
            if (!string.IsNullOrEmpty(jumpTarget))
            {
                onJumpRequested?.Invoke(jumpTarget);
                // 자동으로 다음 대사 재생 (선택사항)
                // await PlayDialogueAsync(jumpTarget, ct);
            }
        }

        /// <summary>
        /// 파싱된 커맨드 리스트를 순차적으로 실행한다.
        /// Text: 글자 하나씩 추가
        /// Speed: 타이핑 속도 변경
        /// Wait: 일정 시간 대기
        /// Click: 입력 대기
        /// Event: 콜백 호출
        /// Effect: 텍스트 이펙트 처리 (향후 확장)
        /// </summary>
        private async UniTask ExecuteCommandsAsync(List<DialogueCommand> commands, CancellationToken ct)
        {
            foreach (var command in commands)
            {
                ct.ThrowIfCancellationRequested();

                switch (command.type)
                {
                    case DialogueCommand.CommandType.Text:
                        // 텍스트를 글자 단위로 출력
                        await DisplayTextAsync(command.value, ct);
                        break;

                    case DialogueCommand.CommandType.Speed:
                        // 타이핑 속도 변경
                        if (float.TryParse(command.value, out float newSpeed))
                        {
                            _currentTypeSpeed = newSpeed;
                            Debug.Log($"[DialogueManager] 타이핑 속도 변경: {newSpeed}초/글자");
                        }
                        break;

                    case DialogueCommand.CommandType.Wait:
                        // 지정된 시간만큼 대기
                        Debug.Log($"[DialogueManager] {command.duration}초 대기 중...");
                        await UniTask.Delay(TimeSpan.FromSeconds(command.duration), cancellationToken: ct);
                        break;

                    case DialogueCommand.CommandType.Click:
                        // 마우스 클릭 대기
                        _state.isWaitingForInput = true;
                        Debug.Log("[DialogueManager] 클릭 대기 중...");
                        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0), cancellationToken: ct);
                        _state.isWaitingForInput = false;
                        Debug.Log("[DialogueManager] 클릭 감지됨");
                        break;

                    case DialogueCommand.CommandType.Event:
                        // 외부 이벤트 콜백 호출
                        Debug.Log($"[DialogueManager] 이벤트 트리거: {command.value}");
                        onEventTriggered?.Invoke(command.value);
                        break;

                    case DialogueCommand.CommandType.Jump:
                        // Jump는 현재 이어서 실행하지 않고 플래그만 설정
                        // PlayDialogueInternalAsync 완료 후 처리됨
                        Debug.Log($"[DialogueManager] 점프 예약: {command.value}");
                        break;

                    case DialogueCommand.CommandType.Effect:
                        // 텍스트 이펙트 처리 (향후 확장)
                        Debug.Log($"[DialogueManager] 텍스트 이펙트: {command.value}");
                        // 예: await ApplyTextEffectAsync(command.value, ct);
                        break;

                    default:
                        Debug.LogWarning($"[DialogueManager] 알 수 없는 커맨드 타입: {command.type}");
                        break;
                }
            }
        }

        /// <summary>
        /// 주어진 텍스트를 글자 단위로 TMP에 추가한다.
        /// 각 글자마다 _currentTypeSpeed만큼 대기한다.
        /// </summary>
        private async UniTask DisplayTextAsync(string text, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(text) || _displayText == null)
                return;

            // 글자 하나씩 추가하는 루프
            for (int i = 0; i < text.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                // 현재 문자 추가
                _displayText.text += text[i];
                _state.displayedCharacters++;

                // 입력 대기 중이면 그대로 유지하고 진행
                if (_state.isWaitingForInput)
                {
                    // 나머지 텍스트 즉시 출력 (클릭 중복 방지)
                    _displayText.text += text.Substring(i + 1);
                    _state.displayedCharacters = text.Length;
                    return;
                }

                // 타이핑 속도만큼 대기
                await UniTask.Delay(TimeSpan.FromSeconds(_currentTypeSpeed), cancellationToken: ct);
            }
        }

        /// <summary>
        /// 커맨드 리스트에서 [jump:xxx] 커맨드를 찾아 점프 대상 ID를 반환한다.
        /// 여러 점프 커맨드가 있으면 마지막 것을 반환한다.
        /// </summary>
        private string FindJumpTarget(List<DialogueCommand> commands)
        {
            string jumpTarget = null;
            foreach (var cmd in commands)
            {
                if (cmd.type == DialogueCommand.CommandType.Jump)
                {
                    jumpTarget = cmd.value;
                }
            }
            return jumpTarget;
        }

        /// <summary>
        /// 현재 재생 일시 정지
        /// </summary>
        public void Pause()
        {
            _state.isPaused = true;
            _playbackCts?.Cancel();
        }

        /// <summary>
        /// 재생 중단 및 정리
        /// </summary>
        public void Stop()
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = null;
            
            if (_displayText != null)
            {
                _displayText.text = "";
            }

            _state.Reset();
        }

        /// <summary>
        /// 현재 재생 상태 조회
        /// </summary>
        public DialoguePlaybackState GetPlaybackState()
        {
            return _state;
        }
    }

    // DialoguePlaybackState 확장 메서드
    public static class DialoguePlaybackStateExtensions
    {
        public static void Reset(this DialoguePlaybackState state)
        {
            state.currentEntry = null;
            state.totalCharacters = 0;
            state.displayedCharacters = 0;
            state.elapsedTime = 0f;
            state.isWaitingForInput = false;
            state.isPaused = false;
        }
    }
}
