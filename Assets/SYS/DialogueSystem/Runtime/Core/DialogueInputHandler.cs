using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 진행 키 입력 처리기.
    /// DialogueManager와 같은 오브젝트에 추가하거나 씬 아무데나 하나만 두면 됨.
    /// </summary>
    public class DialogueInputHandler : MonoBehaviour
    {
        [Tooltip("대화 진행 / 타이프라이터 스킵 키")]
        public KeyCode advanceKey = KeyCode.Space;

        [Tooltip("보조 진행 키 (Enter)")]
        public KeyCode advanceKeyAlt = KeyCode.Return;

        private void Update()
        {
            if (!DialogueManager.Instance || !DialogueManager.Instance.IsRunning) return;

            if (Input.GetKeyDown(advanceKey) || Input.GetKeyDown(advanceKeyAlt))
                DialogueManager.Instance.AdvanceDialogue();
        }
    }
}
