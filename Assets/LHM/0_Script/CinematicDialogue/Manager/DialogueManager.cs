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
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // 싱글톤 설정
        public static DialogueManager Instance { get; private set; }

        [Header("UI 연결 (기본)")]
        [SerializeField] private GameObject _dialoguePanel;       // 하단 대화창 패널
        [SerializeField] private TextMeshProUGUI _displayText;    // 텍스트 표시용 TMP
        [SerializeField] private TextMeshProUGUI _speakerNameText; // 화자 이름 표시용

        [Header("시네마틱 연출 설정")]
        [SerializeField] private GameObject _cinematicTopBar;      // 화면 위 검은 바
        [SerializeField] private GameObject _cinematicBottomBar;   // 화면 아래 검은 바
        [SerializeField] private GameObject _playerHUDPanel;       // 플레이어 HUD 체력창 패널

        [Header("카메라 연출 설정")]
        [SerializeField] private Camera _mainCamera;               // 메인 카메라
        [SerializeField] private Camera _dialogueCamera;           // 거점씬용 서브 카메라
        [SerializeField] private GameObject _cinemachineCameraObject; // 시네머신 가상 카메라 오브젝트 (CM_GamePlay)

        private DialogueDatabase _database;
        private DialoguePlaybackState _state;
        private CancellationTokenSource _playbackCts;

        // 타이핑 스킵용 상태변수
        private bool _isTyping = false;
        private bool _skipRequested = false;

        public CancellationTokenSource PlaybackCts => _playbackCts ?? null;

        // 본래 플레이어 위치로 복귀하기 위해 저장해둘 변수
        private Transform _playerTransform;

        // 콜백 및 이벤트 핸들
        public System.Action<string> onEventTriggered;
        public System.Action<string> onJumpRequested;
        public System.Func<string, CancellationToken, UniTask> onActionTriggered;
        public System.Action onDialogueCompleted;

        // 타이핑 설정
        private float _currentTypeSpeed = 0.05f;
        private const float DEFAULT_TYPE_SPEED = 0.05f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _state = new DialoguePlaybackState();

            if (_mainCamera == null) _mainCamera = Camera.main;

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            SetCinematicBars(false);
            if (_playerHUDPanel != null) _playerHUDPanel.SetActive(true);
        }

        // ----------------------------------------------------
        // [추가] 매 프레임 ESC 키 입력을 감지하여 스킵 기능 발동
        // ----------------------------------------------------
        private void Update()
        {
            if (_playbackCts != null)
            {
                // 1. ESC 전체 대화 스킵
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[DialogueManager] ESC 입력 감지: 대화 스킵 발동!");
                    Stop();
                    return;
                }

                // 2. 텍스트가 한 글자씩 나오는 중일 때 좌클릭이나 F키를 누르면 빠른 출력 요청
                if (_isTyping && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F)))
                {
                    _skipRequested = true;
                }
            }
        }

        public async UniTask PlayDialogueAsync(
            TextAsset jsonAsset,
            string startDialogueId,
            Transform cameraDialoguePoint,
            Transform playerTransform,
            CancellationToken externalCt = default)
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _playbackCts.Token;

            LoadDatabase(jsonAsset);
            _playerTransform = playerTransform;

            // UI 제어
            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);
            SetCinematicBars(true);
            if (_playerHUDPanel != null) _playerHUDPanel.SetActive(false);

            /*// 시네머신 가상 카메라 끄기
            if (_cinemachineCameraObject != null)
            {
                _cinemachineCameraObject.SetActive(false);
            }

            // 시네머신 브레인 제어권 탈환 (강제 종료)
            if (_mainCamera != null)
            {
                Behaviour cineBrain = _mainCamera.GetComponent("CinemachineBrain") as Behaviour;
                if (cineBrain != null) cineBrain.enabled = false;
            }


            // 지정한 빈 오브젝트 위치로 메인 카메라 즉시 순간이동
            if (_mainCamera != null && cameraDialoguePoint != null)
            {
                Vector3 targetPos = cameraDialoguePoint.position;
                targetPos.z = _mainCamera.transform.position.z;
                _mainCamera.transform.position = targetPos;
            }*/

            if (_dialogueCamera != null)
            {
                // [거점씬 방식] 서브 카메라가 할당되어 있다면 카메라를 통째로 교체
                if (_mainCamera != null) _mainCamera.gameObject.SetActive(false);
                _dialogueCamera.gameObject.SetActive(true);

                if (cameraDialoguePoint != null)
                {
                    Vector3 targetPos = cameraDialoguePoint.position;
                    targetPos.z = _dialogueCamera.transform.position.z;
                    _dialogueCamera.transform.position = targetPos;
                }
            }
            else
            {
                // [전투씬 방식] 서브 카메라가 없다면, 기존처럼 시네머신을 끄고 메인 카메라를 강제 이동
                if (_cinemachineCameraObject != null)
                {
                    _cinemachineCameraObject.SetActive(false);
                }

                if (_mainCamera != null)
                {
                    Behaviour cineBrain = _mainCamera.GetComponent("CinemachineBrain") as Behaviour;
                    if (cineBrain != null) cineBrain.enabled = false;

                    if (cameraDialoguePoint != null)
                    {
                        Vector3 targetPos = cameraDialoguePoint.position;
                        targetPos.z = _mainCamera.transform.position.z;
                        _mainCamera.transform.position = targetPos;
                    }
                }
            }

            string currentId = startDialogueId;

            try
            {
                while (!string.IsNullOrEmpty(currentId))
                {
                    DialogueEntry entry = _database.FindDialogue(currentId);
                    if (entry == null) break;

                    string textCommandJump = await PlayDialogueInternalAsync(entry, ct);

                    if (!string.IsNullOrEmpty(textCommandJump))
                        currentId = textCommandJump;
                    else if (!string.IsNullOrEmpty(entry.jumpId))
                        currentId = entry.jumpId;
                    else if (entry.nextIds != null && entry.nextIds.Count > 0)
                        currentId = entry.nextIds[0];
                    else
                        currentId = null;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            // ----------------------------------------------------
            // [추가] ESC(Stop)로 인해 취소되었을 때 발생하는 예외를 안전하게 삼킵니다.
            // ----------------------------------------------------
            catch (OperationCanceledException)
            {
                Debug.Log("[DialogueManager] 대화가 성공적으로 스킵 처리되었습니다.");
            }
            finally
            {
                Stop();
            }
        }

        private async UniTask<string> PlayDialogueInternalAsync(DialogueEntry entry, CancellationToken ct)
        {
            _state.currentEntry = entry;
            _state.isWaitingForInput = false;
            _state.isPaused = false;

            if (_speakerNameText != null) _speakerNameText.text = entry.speakerName ?? "Unknown";
            if (_displayText != null) _displayText.text = "";
            _currentTypeSpeed = DEFAULT_TYPE_SPEED;

            List<DialogueCommand> commands = DialogueParser.Parse(entry.text);
            await ExecuteCommandsAsync(commands, ct);

            onDialogueCompleted?.Invoke();

            string jumpTarget = FindJumpTarget(commands);
            if (!string.IsNullOrEmpty(jumpTarget))
            {
                onJumpRequested?.Invoke(jumpTarget);
            }

            return jumpTarget;
        }

        private async UniTask ExecuteCommandsAsync(List<DialogueCommand> commands, CancellationToken ct)
        {
            foreach (var command in commands)
            {
                ct.ThrowIfCancellationRequested();

                switch (command.type)
                {
                    case DialogueCommand.CommandType.Text:
                        await DisplayTextAsync(command.value, ct);
                        break;
                    case DialogueCommand.CommandType.Speed:
                        if (float.TryParse(command.value, out float newSpeed)) _currentTypeSpeed = newSpeed;
                        break;
                    case DialogueCommand.CommandType.Wait:
                        await UniTask.Delay(TimeSpan.FromSeconds(command.duration), cancellationToken: ct);
                        break;
                    case DialogueCommand.CommandType.Click:
                        _state.isWaitingForInput = true;
                        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F), cancellationToken: ct);
                        _state.isWaitingForInput = false;
                        break;
                    case DialogueCommand.CommandType.Event:
                        onEventTriggered?.Invoke(command.value);
                        break;
                    case DialogueCommand.CommandType.Action:
                        if (onActionTriggered != null)
                        {
                            // ActionController 쪽 작업이 완전히 끝날 때까지 여기서 대화 루프가 정지합니다.
                            await onActionTriggered.Invoke(command.value, ct);
                        }
                        else
                        {
                            Debug.LogWarning($"[DialogueManager] Action '{command.value}'을(를) 처리할 구독자가 없습니다.");
                        }
                        break;
                }
            }
        }

        private async UniTask DisplayTextAsync(string text, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(text) || _displayText == null) return;

            // 타이핑 시작 플래그 ON
            _isTyping = true;
            _skipRequested = false;

            for (int i = 0; i < text.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                // [추가] 사용자가 클릭/F키로 스킵을 요청한 경우
                if (_skipRequested)
                {
                    // 남은 텍스트를 한 번에 다 출력합니다.
                    _displayText.text += text.Substring(i);
                    _state.displayedCharacters += (text.Length - i);

                    //  [click] 커맨드 중복현상을 막기 위해 1프레임 대기.
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    break;
                }

                _displayText.text += text[i];
                _state.displayedCharacters++;

                // (기존에 있던 isWaitingForInput 검사 로직은 삭제되었습니다.)

                await UniTask.Delay(TimeSpan.FromSeconds(_currentTypeSpeed), cancellationToken: ct);
            }

            // 타이핑 종료 플래그 OFF
            _isTyping = false;
            _skipRequested = false;
        }

        private string FindJumpTarget(List<DialogueCommand> commands)
        {
            string jumpTarget = null;
            foreach (var cmd in commands)
            {
                if (cmd.type == DialogueCommand.CommandType.Jump) jumpTarget = cmd.value;
            }
            return jumpTarget;
        }

        public void Pause()
        {
            _state.isPaused = true;
            _playbackCts?.Cancel();
        }

        public void Stop()
        {
            // ----------------------------------------------------
            // [수정] 이미 취소/정리된 상태라면 중복 실행되지 않도록 방어
            // ----------------------------------------------------
            if (_playbackCts == null) return;

            _playbackCts.Cancel();
            _playbackCts.Dispose();
            _playbackCts = null;

            if (_displayText != null) _displayText.text = "";

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            SetCinematicBars(false);
            if (_playerHUDPanel != null) _playerHUDPanel.SetActive(true);

            /*// 카메라 복귀
            if (_mainCamera != null && _playerTransform != null)
            {
                Vector3 targetPos = _playerTransform.position;
                targetPos.z = _mainCamera.transform.position.z;
                _mainCamera.transform.position = targetPos;

                // 시네머신 브레인 복구
                Behaviour cineBrain = _mainCamera.GetComponent("CinemachineBrain") as Behaviour;
                if (cineBrain != null) cineBrain.enabled = true;
            }

            // 가상 카메라 복구
            if (_cinemachineCameraObject != null)
            {
                _cinemachineCameraObject.SetActive(true);
            }*/

            if (_dialogueCamera != null)
            {
                // [거점씬 방식 복구]
                _dialogueCamera.gameObject.SetActive(false);
                if (_mainCamera != null) _mainCamera.gameObject.SetActive(true);
            }
            else
            {
                // [전투씬 방식 복구] 
                if (_mainCamera != null && _playerTransform != null)
                {
                    Vector3 targetPos = _playerTransform.position;
                    targetPos.z = _mainCamera.transform.position.z;
                    _mainCamera.transform.position = targetPos;

                    Behaviour cineBrain = _mainCamera.GetComponent("CinemachineBrain") as Behaviour;
                    if (cineBrain != null) cineBrain.enabled = true;
                }

                if (_cinemachineCameraObject != null)
                {
                    _cinemachineCameraObject.SetActive(true);
                }
            }

            _playerTransform = null;
            _state.Reset();
        }

        private void SetCinematicBars(bool isActive)
        {
            if (_cinematicTopBar != null) _cinematicTopBar.SetActive(isActive);
            if (_cinematicBottomBar != null) _cinematicBottomBar.SetActive(isActive);
        }

        private void LoadDatabase(TextAsset jsonAsset)
        {
            if (jsonAsset == null) return;
            try
            {
                string json = jsonAsset.text;
                string wrappedJson = $"{{\"dialogues\":{json}}}";
                _database = JsonUtility.FromJson<DialogueDatabase>(wrappedJson);
            }
            catch (Exception e) { Debug.LogError($"[DialogueManager] 파싱 오류: {e.Message}"); }
        }
    }

    public static class DialoguePlaybackStateExtensions
    {
        public static void Reset(this DialoguePlaybackState state)
        {
            if (state == null) return;
            state.currentEntry = null;
            state.totalCharacters = 0;
            state.displayedCharacters = 0;
            state.elapsedTime = 0f;
            state.isWaitingForInput = false;
            state.isPaused = false;
        }
    }
}

#region regecy
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using Cysharp.Threading.Tasks;
//using TMPro;
//using UnityEngine;

//namespace DialogSystem.CinematicDialogue
//{
//    /// <summary>
//    /// 시네마틱 대화 시스템의 핵심 매니저.
//    /// JSON 데이터를 로드하고, DialogueParser로 텍스트를 파싱한 후,
//    /// UniTask 기반의 비동기 루프로 텍스트를 한 글자씩 출력하며 커맨드를 처리한다.
//    /// </summary>
//    public class DialogueManager : MonoBehaviour
//    {
//        // 싱글톤 설정
//        public static DialogueManager Instance { get; private set; }

//        [SerializeField] private GameObject _dialoguePanel; // 하단 대화창
//        //[SerializeField] private TextAsset _dialogueJsonAsset;  // JSON 리소스. 삭제 예정
//        [SerializeField] private TextMeshProUGUI _displayText;  // 텍스트 표시용 TMP
//        [SerializeField] private TextMeshProUGUI _speakerNameText;  // 화자 이름 표시용

//        private DialogueDatabase _database;
//        private DialoguePlaybackState _state;
//        private CancellationTokenSource _playbackCts;

//        // 콜백 및 이벤트 핸들
//        public System.Action<string> onEventTriggered;  // [event:xxx] 처리
//        public System.Action<string> onJumpRequested;   // [jump:xxx] 처리
//        public System.Action onDialogueCompleted;      // 대화 완료 시

//        // 타이핑 설정
//        private float _currentTypeSpeed = 0.05f;  // 기본 타이핑 속도 (글자당 초)
//        private const float DEFAULT_TYPE_SPEED = 0.05f;

//        private void Awake()
//        {
//            //싱글톤 패턴(선택사항)
//             if (Instance != null && Instance != this)
//            {
//                Destroy(gameObject);
//                return;
//            }
//            Instance = this;

//            _state = new DialoguePlaybackState();
//            //LoadDatabase(); 삭제 예정

//            // 하단 대화창 끄기
//            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
//        }

//        /// <summary>
//        /// JSON 파일에서 대화 데이터베이스를 로드한다.
//        /// </summary>
//        private void LoadDatabase(TextAsset jsonAsset)
//        {
//            if (jsonAsset == null)
//            {
//                Debug.LogError("[DialogueManager] JSON 에셋이 할당되지 않음");
//                return;
//            }

//            try
//            {
//                string json = jsonAsset.text;
//                // JSON 배열을 수용하기 위해 래핑
//                string wrappedJson = $"{{\"dialogues\":{json}}}";
//                _database = JsonUtility.FromJson<DialogueDatabase>(wrappedJson);
//                Debug.Log($"[DialogueManager] 데이터베이스 로드 완료: {_database.dialogues.Count}개 대사");
//            }
//            catch (Exception e)
//            {
//                Debug.LogError($"[DialogueManager] JSON 파싱 오류: {e.Message}");
//            }
//        }

//        /// <summary>
//        /// 주어진 ID의 대사부터 재생을 시작한다.
//        /// </summary>
//        public async UniTask PlayDialogueAsync
//            (TextAsset jsonAsset, string startDialogueId, 
//            /*Transform spawnPoint = null,*/ CancellationToken externalCt = default)
//        {
//            _playbackCts?.Cancel();
//            _playbackCts?.Dispose();
//            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
//            var ct = _playbackCts.Token;

//            LoadDatabase(jsonAsset);

//            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

//            string currentId = startDialogueId;

//            try
//            {
//                // currentId가 존재하는 동안 계속 다음 대사로 넘어가며 반복(단락 이어가기)
//                while (!string.IsNullOrEmpty(currentId))
//                {
//                    DialogueEntry entry = _database.FindDialogue(currentId);
//                    if (entry == null) break;

//                    // 한 단락 재생 완료 대기 (이 안에서 [click] 커맨드를 만나면 E키를 기다립니다)
//                    string textCommandJump = await PlayDialogueInternalAsync(entry, ct);

//                    // 다음으로 이동할 단락 ID 결정 로직
//                    if (!string.IsNullOrEmpty(textCommandJump))
//                    {
//                        currentId = textCommandJump; // 1. 대사 텍스트 내의 [jump:xxx] 커맨드가 최우선
//                    }
//                    else if (!string.IsNullOrEmpty(entry.jumpId))
//                    {
//                        currentId = entry.jumpId;    // 2. JSON에 명시된 jumpId
//                    }
//                    else if (entry.nextIds != null && entry.nextIds.Count > 0) // !!(배열이면 Count 대신 Length 사용)
//                    {
//                        currentId = entry.nextIds[0]; // 3. JSON에 명시된 nextIds의 첫 번째 요소
//                    }
//                    else
//                    {
//                        currentId = null; // 연결된 대사가 없으면 반복문 종료
//                    }

//                    // 중복 키 입력 방지를 위해 1프레임 대기
//                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
//                }
//            }
//            finally
//            {
//                // 모든 대화가 끝나면 UI 닫기
//                Stop();
//            }

//            /*DialogueEntry entry = _database.FindDialogue(currentId);

//            if (entry == null)
//            {
//                Debug.LogError($"[DialogueManager] 대사 ID '{dialogueId}'를 찾을 수 없음");
//                return;
//            }

//            // 대화 시작하면 하단 대화창 오픈
//            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

//            // 말풍선 등장 위치
//            //if (spawnPoint != null && _displayText != null)
//            //{
//            //    Canvas canvas = _displayText.GetComponentInParent<Canvas>();
//            //    if (canvas != null) canvas.transform.position = spawnPoint.position;
//            //}

//            try
//            {
//                await PlayDialogueInternalAsync(entry, ct);
//            }
//            finally
//            {
//                _playbackCts?.Dispose();
//                _playbackCts = null;
//            }*/
//        }

//        /// <summary>
//        /// 대사 재생의 실제 구현.
//        /// DialogueEntry의 텍스트를 파싱하고 커맨드를 순차 실행한다.
//        /// </summary>
//        private async UniTask<string> PlayDialogueInternalAsync(DialogueEntry entry, CancellationToken ct)
//        {
//            _state.currentEntry = entry;
//            _state.isWaitingForInput = false;
//            _state.isPaused = false;

//            // UI에 화자 이름 표시
//            if (_speakerNameText != null) _speakerNameText.text = entry.speakerName ?? "Unknown";

//            // 텍스트 필드 초기화
//            if (_displayText != null)  _displayText.text = "";

//            // 타이핑 속도 리셋
//            _currentTypeSpeed = DEFAULT_TYPE_SPEED;

//            // 텍스트 파싱: 순수 텍스트와 커맨드를 분리
//            List<DialogueCommand> commands = DialogueParser.Parse(entry.text);

//            // 디버그 출력 (필요시 주석 해제)
//            // DialogueParser.DebugPrintCommands(commands);

//            // 커맨드 순차 실행
//            await ExecuteCommandsAsync(commands, ct);

//            // 대화 완료 콜백
//            onDialogueCompleted?.Invoke();

//            // 점프 명령이 있었는지 확인
//            string jumpTarget = FindJumpTarget(commands);
//            if (!string.IsNullOrEmpty(jumpTarget))
//            {
//                onJumpRequested?.Invoke(jumpTarget);
//                // 자동으로 다음 대사 재생 (선택사항)
//                // await PlayDialogueAsync(jumpTarget, ct);
//            }

//            return jumpTarget;
//        }

//        /// <summary>
//        /// 파싱된 커맨드 리스트를 순차적으로 실행한다.
//        /// Text: 글자 하나씩 추가
//        /// Speed: 타이핑 속도 변경
//        /// Wait: 일정 시간 대기
//        /// Click: 입력 대기
//        /// Event: 콜백 호출
//        /// Effect: 텍스트 이펙트 처리 (향후 확장)
//        /// </summary>
//        private async UniTask ExecuteCommandsAsync(List<DialogueCommand> commands, CancellationToken ct)
//        {
//            foreach (var command in commands)
//            {
//                ct.ThrowIfCancellationRequested();

//                switch (command.type)
//                {
//                    case DialogueCommand.CommandType.Text:
//                        // 텍스트를 글자 단위로 출력
//                        await DisplayTextAsync(command.value, ct);
//                        break;

//                    case DialogueCommand.CommandType.Speed:
//                        // 타이핑 속도 변경
//                        if (float.TryParse(command.value, out float newSpeed))
//                        {
//                            _currentTypeSpeed = newSpeed;
//                            Debug.Log($"[DialogueManager] 타이핑 속도 변경: {newSpeed}초/글자");
//                        }
//                        break;

//                    case DialogueCommand.CommandType.Wait:
//                        // 지정된 시간만큼 대기
//                        Debug.Log($"[DialogueManager] {command.duration}초 대기 중...");
//                        await UniTask.Delay(TimeSpan.FromSeconds(command.duration), cancellationToken: ct);
//                        break;

//                    case DialogueCommand.CommandType.Click:
//                        // 마우스 클릭 대기
//                        _state.isWaitingForInput = true;
//                        Debug.Log("[DialogueManager] 클릭 대기 중...");
//                        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F), cancellationToken: ct);
//                        _state.isWaitingForInput = false;
//                        Debug.Log("[DialogueManager] 클릭 감지됨");
//                        break;

//                    case DialogueCommand.CommandType.Event:
//                        // 외부 이벤트 콜백 호출
//                        Debug.Log($"[DialogueManager] 이벤트 트리거: {command.value}");
//                        onEventTriggered?.Invoke(command.value);
//                        break;

//                    case DialogueCommand.CommandType.Jump:
//                        // Jump는 현재 이어서 실행하지 않고 플래그만 설정
//                        // PlayDialogueInternalAsync 완료 후 처리됨
//                        Debug.Log($"[DialogueManager] 점프 예약: {command.value}");
//                        break;

//                    case DialogueCommand.CommandType.Effect:
//                        // 텍스트 이펙트 처리 (향후 확장)
//                        Debug.Log($"[DialogueManager] 텍스트 이펙트: {command.value}");
//                        // 예: await ApplyTextEffectAsync(command.value, ct);
//                        break;

//                    default:
//                        Debug.LogWarning($"[DialogueManager] 알 수 없는 커맨드 타입: {command.type}");
//                        break;
//                }
//            }
//        }

//        /// <summary>
//        /// 주어진 텍스트를 글자 단위로 TMP에 추가한다.
//        /// 각 글자마다 _currentTypeSpeed만큼 대기한다.
//        /// </summary>
//        private async UniTask DisplayTextAsync(string text, CancellationToken ct)
//        {
//            if (string.IsNullOrEmpty(text) || _displayText == null) return;

//            // 글자 하나씩 추가하는 루프
//            for (int i = 0; i < text.Length; i++)
//            {
//                ct.ThrowIfCancellationRequested();

//                // 현재 문자 추가
//                _displayText.text += text[i];
//                _state.displayedCharacters++;

//                // 입력 대기 중이면 그대로 유지하고 진행
//                if (_state.isWaitingForInput)
//                {
//                    // 나머지 텍스트 즉시 출력 (클릭 중복 방지)
//                    _displayText.text += text.Substring(i + 1);
//                    _state.displayedCharacters = text.Length;
//                    return;
//                }

//                // 타이핑 속도만큼 대기
//                await UniTask.Delay(TimeSpan.FromSeconds(_currentTypeSpeed), cancellationToken: ct);
//            }
//        }

//        /// <summary>
//        /// 커맨드 리스트에서 [jump:xxx] 커맨드를 찾아 점프 대상 ID를 반환한다.
//        /// 여러 점프 커맨드가 있으면 마지막 것을 반환한다.
//        /// </summary>
//        private string FindJumpTarget(List<DialogueCommand> commands)
//        {
//            string jumpTarget = null;
//            foreach (var cmd in commands)
//            {
//                if (cmd.type == DialogueCommand.CommandType.Jump) jumpTarget = cmd.value;
//            }
//            return jumpTarget;
//        }

//        /// <summary>
//        /// 현재 재생 일시 정지
//        /// </summary>
//        public void Pause()
//        {
//            _state.isPaused = true;
//            _playbackCts?.Cancel();
//        }

//        /// <summary>
//        /// 재생 중단 및 정리
//        /// </summary>
//        public void Stop()
//        {
//            _playbackCts?.Cancel();
//            _playbackCts?.Dispose();
//            _playbackCts = null;

//            if (_displayText != null) _displayText.text = "";
//            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

//            _state.Reset();
//        }

//        /// <summary>
//        /// 현재 재생 상태 조회
//        /// </summary>
//        public DialoguePlaybackState GetPlaybackState()
//        {
//            return _state;
//        }
//    }

//    // DialoguePlaybackState 확장 메서드
//    public static class DialoguePlaybackStateExtensions
//    {
//        public static void Reset(this DialoguePlaybackState state)
//        {
//            state.currentEntry = null;
//            state.totalCharacters = 0;
//            state.displayedCharacters = 0;
//            state.elapsedTime = 0f;
//            state.isWaitingForInput = false;
//            state.isPaused = false;
//        }
//    }
//}
#endregion
