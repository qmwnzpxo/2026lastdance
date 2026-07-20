using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    // 싱글톤
    public static AudioManager Instance { get; private set; }

    [Header("오디오 채널 (자동 생성됨)")]
    private AudioSource bgmSource;
    private List<AudioSource> sfxPool = new List<AudioSource>();

    [Header("풀링 설정")]
    [Tooltip("게임 시작 시 미리 만들어둘 효과음 채널의 개수")]
    public int initialPoolSize = 10;

    void Awake()
    {
        // 싱글톤 세팅
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSystem(); // 오디오 세팅
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 2. 인스펙터 설정 없이 스크립트가 스스로 오디오 환경을 구축하는 함수
    private void InitializeAudioSystem()
    {
        // --- BGM 소스 세팅 ---
        GameObject bgmObject = new GameObject("BGM_Source");
        bgmObject.transform.SetParent(this.transform);

        bgmSource = bgmObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;       // 배경음악은 기본적으로 무한 반복
        bgmSource.spatialBlend = 0f; // 2D 사운드 고정

        // --- SFX 풀링 세팅 ---
        GameObject sfxContainer = new GameObject("SFX_Pool");
        sfxContainer.transform.SetParent(this.transform);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewSFXSource(sfxContainer.transform);
        }
    }

    // 새로운 효과음 채널(AudioSource)을 생성하고 풀(Pool)에 넣는 함수
    private AudioSource CreateNewSFXSource(Transform parent)
    {
        GameObject sfxObject = new GameObject($"SFX_Source_{sfxPool.Count}");
        sfxObject.transform.SetParent(parent);

        AudioSource source = sfxObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D 액션 게임이므로 2D 사운드 고정

        sfxPool.Add(source);
        return source;
    }

    // ==========================================
    // [외부에서 호출해 사용할 오디오 재생 함수들]
    // ==========================================

    /// <summary>
    /// 배경음악을 재생합니다. 씬이 넘어가도 동일한 곡이면 끊기지 않습니다.
    /// </summary>
    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        // 이미 같은 곡이 재생 중이라면 처음부터 다시 덮어씌우지 않고 유지
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.Play();
    }

    /// <summary>
    /// 재생 중인 배경음악을 멈춥니다.
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    /// <summary>
    /// 효과음을 재생합니다. (오브젝트 풀링 작동)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource availableSource = GetAvailableSFXSource();
        availableSource.clip = clip;
        availableSource.volume = volume;
        availableSource.pitch = pitch;

        availableSource.Play();
    }

    // 현재 소리가 나지 않는(놀고 있는) 오디오 채널을 찾아주는 함수
    private AudioSource GetAvailableSFXSource()
    {
        // 1. 리스트를 돌면서 재생 중이 아닌 소스를 찾습니다.
        foreach (AudioSource source in sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        // 사운드 누락 방지
        Transform sfxContainer = transform.Find("SFX_Pool");
        Debug.LogWarning("SFX 풀이 가득 차서 채널을 추가로 생성했습니다.");
        return CreateNewSFXSource(sfxContainer != null ? sfxContainer : this.transform);
    }
}