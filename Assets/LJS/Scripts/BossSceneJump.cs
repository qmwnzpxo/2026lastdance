using UnityEngine;
using UnityEngine.SceneManagement;

public class BossSceneJump : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneManager.LoadScene(1);
        }
    }

}
