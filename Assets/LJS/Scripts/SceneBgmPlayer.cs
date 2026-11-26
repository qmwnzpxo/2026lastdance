using UnityEngine;

public class SceneBgmPlayer : MonoBehaviour
{
    [Header("이 씬에서 재생할 배경음악")]
    public AudioClip sceneBGM;

    [Header("BGM 기본 볼륨")]
    [Range(0f, 1f)] public float baseVolume = 1f; // 인스펙터에서 곡마다 볼륨 조절 가능

    void Start()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[SceneBgmPlayer] AudioManager.Instance가 존재하지 않아 BGM을 제어할 수 없습니다.");
            return;
        }

        if (sceneBGM != null)
        {
            AudioManager.Instance.PlayBGM(sceneBGM, baseVolume);
        }
        else
        {
            // 인스펙터에 음악이 비어있다면 현재 재생 중인 음악을 정지시킵니다.
            AudioManager.Instance.StopBGM();
        }
    }
}
