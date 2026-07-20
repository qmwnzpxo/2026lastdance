using UnityEngine;

public class SceneBgmPlayer : MonoBehaviour
{
    [Header("이 씬에서 재생할 배경음악")]
    public AudioClip sceneBGM;

    void Start()
    {
        if (sceneBGM != null)
        {
            AudioManager.Instance.PlayBGM(sceneBGM);
        }
    }
}
