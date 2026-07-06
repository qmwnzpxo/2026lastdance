using UnityEngine;
using UnityEngine.SceneManagement;

public class MurderBoard : MonoBehaviour
{
    [Header("띄워줄 챕터 시작 UI 패널")]
    public GameObject chapterUIPanel;

    private bool isPlayerInRange = false;

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
        // 1. 플레이어가 근처에 있고
        // 2. F키를 눌렀으며
        // 3. UI가 현재 꺼져있는 상태라면 (중복 실행 방지)
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F) && !chapterUIPanel.activeSelf)
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
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;

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
        chapterUIPanel.SetActive(false);

        // TODO: 여기에 씬 이동, 몬스터 소환, 문 열림 등의 코드를 작성하시면 됩니다.
        SceneManager.LoadScene("0630_Chapter_1");
    }

    public void OnClickNo()
    {
        Debug.Log("시작 취소.");

        // 아무 일도 일어나지 않고 UI만 숨깁니다.
        chapterUIPanel.SetActive(false);
    }
}
