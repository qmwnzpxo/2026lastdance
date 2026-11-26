using DialogSystem.CinematicDialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MurderBoard : MonoBehaviour
{
    [Header("띄워줄 챕터 시작 UI 패널")]
    public GameObject chapterUIPanel;

    [Header("Yes 버튼 시 실행할 대화 트리거")]
    public NpcDialogueTrigger targetDialogue;

    private bool isPlayerInRange = false;
    private Transform cachedPlayerTransform;

    void Start()
    {
        // 게임이 시작될 때 UI가 실수로 켜져 있다면 자동으로 꺼줍니다.
        if (chapterUIPanel != null)
        {
            chapterUIPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (chapterUIPanel == null) return;

        // 안전장치
        bool isDialoguePlaying = DialogueManager.Instance != null && DialogueManager.Instance.PlaybackCts != null;

        // 1. 플레이어가 근처에 있고
        // 2. F키를 눌렀으며
        // 3. UI가 현재 꺼져있는 상태라면 (중복 실행 방지)
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F) && !chapterUIPanel.activeSelf && !isDialoguePlaying)
        {
            // UI를 화면에 나타나게 합니다.
            chapterUIPanel.SetActive(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            cachedPlayerTransform = other.transform; // 플레이어 접근 기억
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            cachedPlayerTransform = null; // 플레이어 비접근시 기억 삭제
            // 플레이어가 머더보드에서 멀어지면 열려있던 UI도 자동으로 닫아줍니다.
            if (chapterUIPanel != null)
            {
                chapterUIPanel.SetActive(false);
            }
        }
    }

    // ==========================================
    // 아래는 UI 버튼을 클릭했을 때 실행될 함수들입니다.
    // ==========================================

    public void OnClickYes()
    {
        Debug.Log("챕터1 시작! (여기에 이후 기능 추가)");

        // UI를 다시 숨깁니다.
        if (chapterUIPanel != null)
        {
            chapterUIPanel.SetActive(false);
        }

        if (targetDialogue != null && cachedPlayerTransform != null)
        {
            targetDialogue.ForceStartDialogue(cachedPlayerTransform);
        }
        else
        {
            Debug.LogWarning("연결된 대화 트리거가 없거나 플레이어를 찾을 수 없습니다!");
        }
    }

    public void OnClickNo()
    {
        Debug.Log("시작 취소.");

        // 아무 일도 일어나지 않고 UI만 숨깁니다.
        if (chapterUIPanel != null)
        {
            chapterUIPanel.SetActive(false);
        }
    }
}
