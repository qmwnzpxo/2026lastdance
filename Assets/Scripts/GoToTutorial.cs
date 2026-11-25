using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToTutorial : MonoBehaviour
{
    public void LoadTutorialScene()
    {
        Debug.Log("LoadTutorialScene called");
        SceneManager.LoadScene("Tutorial");
    }
}
