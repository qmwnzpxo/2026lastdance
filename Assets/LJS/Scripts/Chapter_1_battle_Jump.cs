using UnityEngine;
using UnityEngine.SceneManagement;

public class Chapter_1_battle_Jump : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneManager.LoadScene("0712_Chapter_1_Test");
        }
    }
}
