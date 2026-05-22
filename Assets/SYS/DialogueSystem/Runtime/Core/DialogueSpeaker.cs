using UnityEngine;

namespace DialogueSystem
{
    public enum DialogueMode
    {
        /// <summary>항상 하단 패널</summary>
        Panel,
        /// <summary>항상 말풍선</summary>
        Bubble,
        /// <summary>컷씬(Timeline)이면 패널, NPC 상호작용이면 말풍선</summary>
        Auto,
    }

    /// <summary>
    /// 대화가 가능한 NPC에 붙이는 컴포넌트.
    ///
    /// 설정법:
    ///  1. NPC 루트에 DialogueSpeaker 추가
    ///  2. bubbleAnchor → 말풍선이 나올 위치 (머리 위 빈 Transform 권장)
    ///  3. startDialogueId → 이 NPC의 기본 대화 시작 ID
    ///  4. preferredMode → Bubble / Panel / Auto
    ///
    /// 상호작용:
    ///  플레이어 스크립트에서 Interact() 호출 or 아래처럼 직접 호출.
    ///  DialogueManager.Instance.StartDialogue("ID", speakerRef);
    /// </summary>
    public class DialogueSpeaker : MonoBehaviour
    {
        [Header("말풍선 위치")]
        [Tooltip("말풍선이 붙을 월드 위치 (보통 NPC 머리 위 빈 오브젝트).\n" +
                 "없으면 NPC 본체 위쪽으로 자동 오프셋.")]
        public Transform bubbleAnchor;

        [Tooltip("bubbleAnchor가 없을 때 사용하는 월드 Y 오프셋")]
        public float anchorOffsetY = 1.2f;

        [Header("대화 설정")]
        [Tooltip("이 NPC와 대화 시작할 기본 ID")]
        public string startDialogueId;

        [Tooltip("Panel: 하단 패널 고정\nBubble: 말풍선\nAuto: 호출 방식에 따라 자동 결정")]
        public DialogueMode preferredMode = DialogueMode.Bubble;

        /// <summary>
        /// 말풍선이 나올 월드 포지션 반환.
        /// bubbleAnchor가 설정되어 있으면 그 위치, 없으면 본체 + offset.
        /// </summary>
        public Vector3 GetAnchorWorldPos() =>
            bubbleAnchor != null
                ? bubbleAnchor.position
                : transform.position + Vector3.up * anchorOffsetY;

        // ── 편의 메서드 ────────────────────────────────────────────────────────

        /// <summary>플레이어 스크립트에서 상호작용 시 호출</summary>
        public void Interact()
        {
            if (string.IsNullOrEmpty(startDialogueId))
            {
                UnityEngine.Debug.LogWarning($"[DialogueSpeaker] {name}: startDialogueId가 비어 있습니다.");
                return;
            }
            DialogueManager.Instance?.StartDialogue(startDialogueId, this);
        }

        /// <summary>특정 ID부터 이 스피커 기준으로 대화 시작</summary>
        public void StartFrom(string id) =>
            DialogueManager.Instance?.StartDialogue(id, this);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var pos = GetAnchorWorldPos();
            Gizmos.DrawWireSphere(pos, 0.1f);
            Gizmos.DrawLine(transform.position, pos);
        }
#endif
    }
}
