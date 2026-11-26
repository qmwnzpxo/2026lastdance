using System;
using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using UnityEngine;

public class NpcDialogueTrigger : MonoBehaviour
{
    [Header("대본 설정")]
    public TextAsset dialogueJson;
    public string startingDialogueId = "intro_01";

    [Header("카메라 설정")]
    [SerializeField] private Transform _cameraDialoguePoint; // [추가] 대화 시 카메라가 위치할 빈 오브젝트 위치

    [Header("종료 후 연출 설정")]
    [SerializeField] private Transform _teleportPoint;       // [추가] 대화 종료 후 플레이어가 이동할 목적지 (비워두면 이동 안 함)
    [SerializeField] private bool _hideAfterDialogue = false; // [추가] 대화 종료 후 이 NPC 오브젝트를 숨길지 여부

    [Header("상호작용 설정")]
    public KeyCode interactionKey = KeyCode.F;

    //[Header("UI 연결")]
    //[SerializeField] private GameObject _headSpeechBubble; 현재 NPC 머리 위 말풍선 용으로 사용하지 않음.
    // NPC 사용시 주석 해제 후 코드 리팩토링 필요

    private bool _isPlayerInRange = false;
    private bool _isDialogueActive = false;
    private Transform _playerTransform;

    private void Awake()
    {
        // if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);
    }

    private void Update()
    {
        if (_isPlayerInRange && !_isDialogueActive && Input.GetKeyDown(interactionKey))
        {
            StartDialogueAsync().Forget();
        }
    }

    private async UniTaskVoid StartDialogueAsync()
    {
        if (dialogueJson == null || _playerTransform == null || _cameraDialoguePoint == null)
        {
            Debug.LogError("[NpcDialogueTrigger] 대본, 플레이어 정보, 또는 카메라 대화 위치 오브젝트가 누락되었습니다.");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[NpcDialogueTrigger] DialogueManager.Instance가 존재하지 않습니다.");
            return;
        }

        if (_isDialogueActive) return;

        Transform cachedPlayer = _playerTransform;
        _isDialogueActive = true;
        // if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);

        try
        {
            // 매니저에게 '카메라가 가야 할 빈 오브젝트 위치'와 '플레이어 위치'를 함께 건넵니다.
            await DialogueManager.Instance.PlayDialogueAsync(
                dialogueJson,
                startingDialogueId,
                _cameraDialoguePoint,
                cachedPlayer,
                this.GetCancellationTokenOnDestroy());

            if (this == null) return;

            if (_teleportPoint != null && cachedPlayer != null)
            {
                // 1. 플레이어 위치를 목적지 좌표로 즉시 순간이동
                cachedPlayer.position = _teleportPoint.position;

                // 2. 2D 물리(Rigidbody2D)로 인한 순간이동 직후 속도 버그 방지 (관성 제거)
                if (cachedPlayer.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity = Vector2.zero;
                }

                // 3. 카메라를 댐핑 없이 즉시 플레이어 위치로
                CameraFollow.SnapMainToTarget();

                Debug.Log($"[NpcDialogueTrigger] 대화 완료 후 플레이어를 {_teleportPoint.name} 위치로 순간이동 시켰습니다.");
            }

            if (_hideAfterDialogue)
            {
                // 숨겨지는 경우, 더 이상 상호작용할 수 없도록 오브젝트 자체를 비활성화합니다.
                gameObject.SetActive(false);
                Debug.Log($"[NpcDialogueTrigger] {gameObject.name} 오브젝트가 비활성화되어 화면에서 사라졌습니다.");
            }
        }
        catch (OperationCanceledException)
        {
            // 오브젝트 파괴 또는 대화 중단 시 정상 취소 처리
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
        finally
        {
            _isDialogueActive = false;
        }
    }

    // UI 버튼 등으로 강제 대화호출 함수
    public void ForceStartDialogue(Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning("[NpcDialogueTrigger] ForceStartDialogue에 전달된 player가 null입니다.");
            return;
        }

        if (_isDialogueActive) return;

        // 1. 대화에 필요한 플레이어 정보를 세팅합니다.
        _playerTransform = player;

        // 2. 플레이어가 범위 안에 있다고 강제 인식시킵니다.
        _isPlayerInRange = true;

        // 3. 비동기 대화 시작 코드를 실행합니다.
        StartDialogueAsync().Forget();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            _playerTransform = other.transform;
            //if (!_isDialogueActive && _headSpeechBubble != null) _headSpeechBubble.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            _isDialogueActive = false;
            _playerTransform = null;
            // if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.Stop();
            }
        }
    }
}