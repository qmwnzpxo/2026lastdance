using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using UnityEngine;

public class NpcDialogueTrigger : MonoBehaviour
{
    [Header("대화 설정")]
    public string startingDialogueId = "intro_01";
    public Transform dialoguePoint;

    private bool _isPlayerInRange = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !_isPlayerInRange)
        {
            _isPlayerInRange = true;
            Debug.Log("플레이어 NPC 대화범위 진입");
            DialogueManager.Instance.PlayDialogueAsync(startingDialogueId, dialoguePoint).Forget();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            Debug.Log("플레이어 NPC 대화범위 진입");
            DialogueManager.Instance.Stop();
        }
    }
}
