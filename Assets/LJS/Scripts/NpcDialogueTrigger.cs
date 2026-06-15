using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using UnityEngine;

public class NpcDialogueTrigger : MonoBehaviour
{
    /// <summary>
    /// NPC 오브젝트에 부착하여 플레이어와의 2D 충돌 및 상호작용 키 입력을 처리하는 스크립트
    /// </summary>
    [Header("대본 설정")]
    public TextAsset dialogueJson;
    public string startingDialogueId = "intro_01"; 
    // 질문. 각 npc의 json의 시작점을 intro로 통일해야 하나. 두번째 접촉시 대화가 달라질 순 없나?

    [Header("상호작용 설정")]
    public KeyCode interactionKey = KeyCode.F;

    /*[Header("대화 설정")]
    public string startingDialogueId = "intro_01";
    public KeyCode interactionKey = KeyCode.F; // 상호작용 키 : F
    //public Transform dialoguePoint;*/ // 삭제 예정

    [Header("UI 연결")]
    [SerializeField] private GameObject _headSpeechBubble; // NPC 머리 말풍선

    private bool _isPlayerInRange = false;
    private bool _isDialogueActive = false;

    private void Awake()
    {
        if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);
    }
    private void Update()
    {
        // 플레이어가 범위 내에 있고, 아직 대화창이 안 켜졌는데, 상호작용 키를 누른 경우
        if (_isPlayerInRange && !_isDialogueActive && Input.GetKeyDown(interactionKey))
        {
            StartDialogue();
        }
    }

    private void StartDialogue()
    {
        if (dialogueJson == null)
        {
            Debug.LogError($"[NPC2DDialogueTrigger] {gameObject.name}에 할당된 JSON 대본이 없습니다!");
            return;
        }
        _isDialogueActive = true;
        if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);
        // 2. 하단 고정 대화창을 켜고 대화를 재생합니다.
        DialogueManager.Instance.PlayDialogueAsync(dialogueJson, startingDialogueId).Forget();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            Debug.Log("플레이어 NPC 대화범위 진입");
            if (!_isDialogueActive && _headSpeechBubble != null) _headSpeechBubble.SetActive(true);
            //DialogueManager.Instance.PlayDialogueAsync(startingDialogueId, dialoguePoint).Forget();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            _isDialogueActive = false;
            Debug.Log("플레이어 NPC 대화범위 진입");
            if (_headSpeechBubble != null) _headSpeechBubble.SetActive(false);
            DialogueManager.Instance.Stop();
        }
    }
}
