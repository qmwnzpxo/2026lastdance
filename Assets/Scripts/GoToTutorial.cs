using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToTutorial : MonoBehaviour
{
    public void LoadTutorialScene()
    {
        SceneManager.LoadScene("0522_Tutorial");
    }
}
