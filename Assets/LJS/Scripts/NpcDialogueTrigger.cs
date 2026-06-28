using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using System.Runtime.InteropServices;
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
            Debug.LogError($"[NPC2DDialogueTrigger] 대본, 플레이어 정보, 또는 카메라 대화 위치 오브젝트가 누락되었습니다.");
            return;
        }

        _isDialogueActive = true;
        // if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);

        // [수정] 매니저에게 '카메라가 가야 할 빈 오브젝트 위치'와 '플레이어 위치'를 함께 건넵니다.
        await DialogueManager.Instance.PlayDialogueAsync(dialogueJson, startingDialogueId, _cameraDialoguePoint, _playerTransform);

        if (_teleportPoint != null)
        {
            // 1. 플레이어 위치를 목적지 좌표로 즉시 순간이동
            _playerTransform.position = _teleportPoint.position;

            // 2. 2D 물리(Rigidbody2D)로 인한 순간이동 직후 속도 버그 방지 (관성 제거)
            if (_playerTransform.TryGetComponent<Rigidbody2D>(out var rb))
            {
                // 유니티 버전에 따라 rb.velocity = Vector2.zero; 를 사용하셔도 됩니다.
                rb.linearVelocity = Vector2.zero;
            }

            Debug.Log($"[NpcDialogueTrigger] 대화 완료 후 플레이어를 {_teleportPoint.name} 위치로 순간이동 시켰습니다.");
        }

        _isDialogueActive = false;

        if (_hideAfterDialogue)
        {
            // 숨겨지는 경우, 더 이상 상호작용할 수 없도록 오브젝트 자체를 비활성화합니다.
            gameObject.SetActive(false);
            Debug.Log($"[NpcDialogueTrigger] {gameObject.name} 오브젝트가 비활성화되어 화면에서 사라졌습니다.");
        }
        /*if (_isPlayerInRange && _headSpeechBubble != null)
        {
            _headSpeechBubble.SetActive(true);
        }*/
        
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
            DialogueManager.Instance.Stop();
        }
    }
}