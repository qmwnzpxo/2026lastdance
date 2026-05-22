using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// NPC에 붙이면 끝. 플레이어가 범위 안에 들어오면 상호작용 가능 상태,
    /// 인터랙트 키(기본 F 또는 E) 누르면 대화 시작.
    /// </summary>
    public class DialogueStarter : MonoBehaviour
    {
        [Header("대화 설정")]
        [Tooltip("시작할 대화 ID (CSV의 ID 컬럼)")]
        public string dialogueId = "NPC001_001";

        [Tooltip("말풍선 / 패널 선택. Auto = speaker 있으면 말풍선, 없으면 패널")]
        public DialogueMode mode = DialogueMode.Auto;

        [Header("상호작용 키")]
        public KeyCode interactKey = KeyCode.F;

        [Header("범위 트리거 (선택)")]
        [Tooltip("체크하면 OnTriggerEnter2D 로 범위 진입 시 자동 대화 시작\n" +
                 "해제하면 interactKey 입력 방식 사용")]
        public bool autoTrigger = false;

        [Header("말풍선 앵커 (선택)")]
        [Tooltip("말풍선이 나올 위치. 비워두면 이 오브젝트 기준 자동 오프셋")]
        public Transform bubbleAnchor;

        // ── 내부 ───────────────────────────────────────────────────────────────
        private DialogueSpeaker _speaker;
        private bool            _playerInRange;

        private void Awake()
        {
            // DialogueSpeaker 자동 구성
            _speaker = GetComponent<DialogueSpeaker>()
                    ?? gameObject.AddComponent<DialogueSpeaker>();

            _speaker.startDialogueId = dialogueId;
            _speaker.preferredMode   = mode;
            _speaker.bubbleAnchor    = bubbleAnchor;
        }

        private void Update()
        {
            if (autoTrigger) return;
            if (_playerInRange && Input.GetKeyDown(interactKey))
                StartDialogue();
        }

        // ── 트리거 방식 ────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            if (autoTrigger) StartDialogue();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _playerInRange = false;
        }

        // ── 대화 시작 ──────────────────────────────────────────────────────────

        public void StartDialogue() => _speaker.Interact();

        // ── 에디터 기즈모 ──────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var col = GetComponent<Collider2D>();
            if (col != null) return; // 콜라이더 있으면 기즈모 생략
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
#endif
    }
}
