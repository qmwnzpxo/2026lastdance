using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DialogSystem.CinematicDialogue
{
    /// <summary>
    /// DialogueManager 사용 예제.
    /// 이 스크립트를 씬의 UI Canvas와 함께 배치하여 테스트할 수 있다.
    /// </summary>
    public class DialogueExample : MonoBehaviour
    {
        [SerializeField] private DialogueManager _dialogueManager;
        private CancellationTokenSource _exampleCts;

        private void Update()
        {
            // 테스트 키 입력
            if (Input.GetKeyDown(KeyCode.Space))
            {
                StartDialogueSequence().Forget();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopDialogue();
            }
        }

        /// <summary>
        /// 대화 시퀀스 시작
        /// </summary>
        private async UniTask StartDialogueSequence()
        {
            _exampleCts?.Cancel();
            _exampleCts?.Dispose();
            _exampleCts = new CancellationTokenSource();

            Debug.Log("[DialogueExample] 스페이스바를 눌러 대화를 시작합니다. ESC로 중단.");

            // DialogueManager의 이벤트 구독
            _dialogueManager.onEventTriggered += OnDialogueEvent;
            _dialogueManager.onJumpRequested += OnDialogueJump;
            _dialogueManager.onDialogueCompleted += OnDialogueCompleted;

            try
            {
                // "intro_01" 부터 재생 시작
                await _dialogueManager.PlayDialogueAsync("intro_01", externalCt: _exampleCts.Token);
            }
            finally
            {
                // 이벤트 구독 해제
                _dialogueManager.onEventTriggered -= OnDialogueEvent;
                _dialogueManager.onJumpRequested -= OnDialogueJump;
                _dialogueManager.onDialogueCompleted -= OnDialogueCompleted;
            }
        }

        /// <summary>
        /// 대화 중단
        /// </summary>
        private void StopDialogue()
        {
            Debug.Log("[DialogueExample] 대화 중단");
            _dialogueManager.Stop();
            _exampleCts?.Cancel();
        }

        /// <summary>
        /// [event:xxx] 커맨드 처리
        /// 외부 게임 이벤트를 트리거할 때 사용
        /// </summary>
        private void OnDialogueEvent(string eventName)
        {
            Debug.Log($"[DialogueExample] 이벤트 발생: {eventName}");

            // 예제: 카메라 흔들기, 사운드 재생 등
            switch (eventName)
            {
                case "CameraShake":
                    Debug.Log(">>> 카메라 흔들림 효과 재생 (구현 필요)");
                    break;

                case "PlaySound":
                    Debug.Log(">>> 사운드 재생 (구현 필요)");
                    break;

                default:
                    Debug.Log($">>> 알 수 없는 이벤트: {eventName}");
                    break;
            }
        }

        /// <summary>
        /// [jump:xxx] 커맨드 처리
        /// 분기 및 점프 로직
        /// </summary>
        private void OnDialogueJump(string targetId)
        {
            Debug.Log($"[DialogueExample] 점프 요청됨: {targetId}");
            // 필요시 자동으로 다음 대사 재생
            // _dialogueManager.PlayDialogueAsync(targetId, _exampleCts.Token).Forget();
        }

        /// <summary>
        /// 대화 완료 시 처리
        /// </summary>
        private void OnDialogueCompleted()
        {
            Debug.Log("[DialogueExample] 현재 대사 완료.");
            // 다음 시퀀스 처리, UI 업데이트 등
        }

        private void OnDestroy()
        {
            _exampleCts?.Cancel();
            _exampleCts?.Dispose();
        }
    }
}
