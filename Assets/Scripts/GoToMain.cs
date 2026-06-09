using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToMain : MonoBehaviour
{
    public void LoadMainScene()
    {
        Debug.Log("LoadMainScene called");
        SceneManager.LoadScene("Main");
    }
}
