using System;
using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using UnityEngine;

/// <summary>
/// 자동 대화 트리거와 NPC 키 입력 대화 트리거를 통합한 컴포넌트.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        AutoOnEnter,
        InteractOnKey
    }

    [Header("대본 설정")]
    [SerializeField] private TextAsset dialogueJson;
    [SerializeField] private string startingDialogueId = "intro_01";

    [Header("트리거 방식")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.InteractOnKey;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = false;
    [SerializeField] private bool stopOnExit = true;

    [Header("카메라 설정")]
    [SerializeField] private Transform cameraDialoguePoint;

    [Header("종료 후 연출 설정")]
    [SerializeField] private Transform teleportPoint;
    [SerializeField] private bool hideAfterDialogue = false;

    [Header("안내 UI (선택)")]
    [SerializeField] private GameObject promptUI;

    private bool _isPlayerInRange = false;
    private bool _isDialogueActive = false;
    private bool _hasTriggered = false;
    private Transform _playerTransform;

    private void Awake()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (triggerMode != TriggerMode.InteractOnKey) return;
        if (!_isPlayerInRange || _isDialogueActive) return;
        if (triggerOnce && _hasTriggered) return;

        if (Input.GetKeyDown(interactionKey))
        {
            StartDialogueAsync().Forget();
        }
    }

    /// <summary>
    /// UI 버튼이나 외부 스크립트에서 대화를 강제로 시작할 때 호출합니다.
    /// </summary>
    public void ForceStartDialogue(Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning($"[DialogueTrigger] {name}: ForceStartDialogue에 전달된 player가 null입니다.", this);
            return;
        }

        if (_isDialogueActive || (triggerOnce && _hasTriggered)) return;

        _playerTransform = player;
        _isPlayerInRange = true;
        StartDialogueAsync().Forget();
    }

    private async UniTaskVoid StartDialogueAsync()
    {
        if (dialogueJson == null || _playerTransform == null || cameraDialoguePoint == null)
        {
            Debug.LogError($"[DialogueTrigger] {name}: 대본(dialogueJson), 플레이어, 또는 카메라 대화 위치가 누락되었습니다.", this);
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"[DialogueTrigger] {name}: DialogueManager.Instance가 존재하지 않습니다.", this);
            return;
        }

        if (_isDialogueActive) return;

        Transform cachedPlayer = _playerTransform;
        _isDialogueActive = true;
        _hasTriggered = true;
        UpdatePromptUI(false);

        try
        {
            await DialogueManager.Instance.PlayDialogueAsync(
                dialogueJson,
                startingDialogueId,
                cameraDialoguePoint,
                cachedPlayer,
                this.GetCancellationTokenOnDestroy());

            if (this == null) return;

            TeleportPlayerIfNeeded(cachedPlayer);

            if (hideAfterDialogue)
            {
                gameObject.SetActive(false);
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
            if (this != null && gameObject.activeInHierarchy)
            {
                UpdatePromptUI(_isPlayerInRange && triggerMode == TriggerMode.InteractOnKey && !(triggerOnce && _hasTriggered));
            }
        }
    }

    private void TeleportPlayerIfNeeded(Transform player)
    {
        if (teleportPoint == null || player == null) return;

        player.position = teleportPoint.position;

        if (player.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
        }

        CameraFollow.SnapMainToTarget();
    }

    private void UpdatePromptUI(bool show)
    {
        if (promptUI != null)
        {
            promptUI.SetActive(show);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = true;
        _playerTransform = other.transform;

        if (triggerOnce && _hasTriggered) return;

        if (triggerMode == TriggerMode.AutoOnEnter)
        {
            if (!_isDialogueActive)
            {
                StartDialogueAsync().Forget();
            }
        }
        else if (!_isDialogueActive)
        {
            UpdatePromptUI(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = false;
        _playerTransform = null;
        UpdatePromptUI(false);

        if (stopOnExit && _isDialogueActive)
        {
            _isDialogueActive = false;
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.Stop();
            }
        }
    }
}
