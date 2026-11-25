using UnityEngine;
using UnityEngine.SceneManagement;

public class Chapter_1_BossJump : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneManager.LoadScene("Chapter_1_Boss");
        }
    }
}
