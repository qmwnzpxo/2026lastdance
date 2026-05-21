using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// NPC 오브젝트에 붙이는 대화 트리거.
    /// Collider2D Is Trigger 체크, 플레이어 Tag = "Player" 필요.
    /// 범위 진입 후 E 키 → 대화 시작.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NPCDialogueTrigger : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] private DialogueDatabase database;
        [SerializeField] private string startDialogueId;

        [Header("Interact Hint")]
        [Tooltip("'E 키를 눌러 대화' 같은 UI 오브젝트 (없어도 됨)")]
        [SerializeField] private GameObject interactHint;

        [Tooltip("대화 중 재진입 방지")]
        [SerializeField] private bool lockDuringDialogue = true;

        private bool _playerInRange;
        private bool _isTalking;

        void Start()
        {
            // Awake/OnEnable 시점에는 DialogueManager.Instance가 초기화 안 됐을 수 있어서 Start에서 구독
            if (lockDuringDialogue && DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueEnd.AddListener(OnDialogueEnd);
        }

        void OnEnable()
        {
            // 재활성화 시 재구독 (Start 이후에만 동작)
            if (lockDuringDialogue && DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueEnd.AddListener(OnDialogueEnd);
        }

        void OnDisable()
        {
            if (lockDuringDialogue && DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueEnd.RemoveListener(OnDialogueEnd);
        }

        void Update()
        {
            if (!_playerInRange || _isTalking) return;
            if (Input.GetKeyDown(KeyCode.E)) StartTalk();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            interactHint?.SetActive(true);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            interactHint?.SetActive(false);
        }

        void StartTalk()
        {
            _isTalking = true;
            interactHint?.SetActive(false);
            DialogueManager.Instance.StartDialogue(database, startDialogueId);
        }

        void OnDialogueEnd()
        {
            _isTalking = false;
            if (_playerInRange) interactHint?.SetActive(true);
        }
    }
}
