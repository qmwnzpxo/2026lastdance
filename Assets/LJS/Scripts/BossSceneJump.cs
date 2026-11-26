using UnityEngine;
using UnityEngine.SceneManagement;

public class BossSceneJump : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (1 >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError("[BossSceneJump] Build Settings에 인덱스 1번 씬이 등록되지 않았습니다.");
                return;
            }
            SceneManager.LoadScene(1);
        }
    }

}
