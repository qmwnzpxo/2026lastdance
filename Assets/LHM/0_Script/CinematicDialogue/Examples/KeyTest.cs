using UnityEngine;

public class KeyTest : MonoBehaviour
{
    private void Update()
    {
        // 모든 키 입력 감지
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log("✓ Q 키 감지 성공!");
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("✓ W 키 감지 성공!");
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("✓ E 키 감지 성공!");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("✓ R 키 감지 성공!");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("✓ Space 키 감지 성공!");
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 100),
            "키 감지 테스트\n" +
            "Q, W, E, R, Space 누르기\n" +
            "콘솔 확인!");
    }
}
