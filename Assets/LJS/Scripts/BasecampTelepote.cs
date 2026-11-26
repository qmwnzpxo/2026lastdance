using UnityEngine;

public class BasecampTelepote : MonoBehaviour
{
    [Header("순간이동 도착 지점 (도착할 위치에 있는 빈 오브젝트 삽입)")]
    public Transform destinationPoint;

    [Header("페이드 매니저 연결")]
    public Cam_FadeOutManager fadeoutManager;

    private bool isPlayerInRange = false;
    private GameObject playerObject;

    void Update()
    {
        // 플레이어가 트리거 안에 있고 F키를 눌렀다면
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            if (destinationPoint == null)
            {
                Debug.LogWarning($"[BasecampTelepote] {name}의 destinationPoint가 연결되지 않았습니다.");
                return;
            }

            if (playerObject != null)
            {
                // 플레이어의 위치를 목적지 오브젝트의 위치로 순간이동
                playerObject.transform.position = destinationPoint.position;

                // 만약 FadeManager가 연결되어 있다면, 검은 화면 연출을 실행합니다!
                if (fadeoutManager != null)
                {
                    fadeoutManager.PlayFadeEffect();
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("닿음");
            isPlayerInRange = true;
            playerObject = other.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("나감");
            isPlayerInRange = false;
            playerObject = null;
        }
    }
}
