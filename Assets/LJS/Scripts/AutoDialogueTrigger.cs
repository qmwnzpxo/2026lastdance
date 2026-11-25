using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using UnityEngine;

public class AutoDialogueTrigger : MonoBehaviour
{
    [Header("대본 설정")]
    public TextAsset dialogueJson;
    public string startingDialogueId = "intro_01";

    [Header("카메라 설정")]
    [SerializeField] private Transform _cameraDialoguePoint;

    [Header("종료 후 연출 설정")]
    [SerializeField] private Transform _teleportPoint;
    [SerializeField] private bool _hideAfterDialogue = true;

    private bool _isPlayerInRange = false;
    private bool _isDialogueActive = false;
    private Transform _playerTransform;

    private void Awake()
    {
        // 필요 시 초기화 로직
    }

    private async UniTaskVoid StartDialogueAsync()
    {
        if (dialogueJson == null || _playerTransform == null || _cameraDialoguePoint == null)
        {
            Debug.LogError($"[AutoDialogueTrigger] 대본, 플레이어 정보, 또는 카메라 대화 위치 오브젝트가 누락되었습니다.");
            return;
        }

        _isDialogueActive = true;

        // 매니저에게 카메라가 가야 할 위치와 플레이어 위치를 건네며 대화 시작
        await DialogueManager.Instance.PlayDialogueAsync(dialogueJson, startingDialogueId, _cameraDialoguePoint, _playerTransform);

        if (_teleportPoint != null)
        {
            // 1. 플레이어 위치를 목적지 좌표로 즉시 순간이동
            _playerTransform.position = _teleportPoint.position;

            // 2. 2D 물리(Rigidbody2D) 관성 제거
            if (_playerTransform.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero;
            }

            // 3. 카메라를 댐핑 없이 즉시 플레이어 위치로
            CameraFollow.SnapMainToTarget();

            Debug.Log($"[AutoDialogueTrigger] 대화 완료 후 플레이어를 {_teleportPoint.name} 위치로 순간이동 시켰습니다.");
        }

        _isDialogueActive = false;

        if (_hideAfterDialogue)
        {
            // 숨겨지는 경우, 더 이상 상호작용할 수 없도록 오브젝트 자체를 비활성화
            gameObject.SetActive(false);
            Debug.Log($"[AutoDialogueTrigger] {gameObject.name} 오브젝트가 비활성화되어 화면에서 사라졌습니다.");
        }
    }

    // UI 버튼 등으로 강제 대화호출 함수
    public void ForceStartDialogue(Transform player)
    {
        _playerTransform = player;
        _isPlayerInRange = true;
        StartDialogueAsync().Forget();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            _playerTransform = other.transform;

            // [핵심 변경점] 키 입력을 기다리는 Update문 대신, 
            // 플레이어가 닿자마자 대화 중이 아니라면 곧바로 비동기 대화를 실행합니다.
            if (!_isDialogueActive)
            {
                StartDialogueAsync().Forget();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            _isDialogueActive = false;
            _playerTransform = null;

            // 플레이어가 강제로 범위를 벗어나면 대화 중단
            DialogueManager.Instance.Stop();
        }
    }
}