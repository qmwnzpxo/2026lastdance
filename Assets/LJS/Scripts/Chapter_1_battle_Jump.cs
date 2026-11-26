using UnityEngine;
using UnityEngine.SceneManagement;

public class Chapter_1_battle_Jump : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (!Application.CanStreamedLevelBeLoaded("Chapter_1"))
            {
                Debug.LogError("[Chapter_1_battle_Jump] 'Chapter_1' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.");
                return;
            }
            SceneManager.LoadScene("Chapter_1");
        }
    }
}
