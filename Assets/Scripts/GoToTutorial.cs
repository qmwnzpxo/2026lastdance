using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToTutorial : MonoBehaviour
{
    public void LoadTutorialScene()
    {
        Debug.Log("LoadTutorialScene called");
        SectionSave.Clear(); // 새로 시작 → 이전 구역 저장 초기화
        SceneManager.LoadScene("Tutorial");
    }
}
