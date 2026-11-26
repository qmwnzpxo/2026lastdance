using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    // 싱글톤
    public static AudioManager Instance { get; private set; }

    [Header("전체 볼륨 조절")]
    [Range(0f, 1f)] public float masterBGMVolume = 0.5f;
    [Range(0f, 1f)] public float masterSFXVolume = 0.5f;

    // 기본 BGM 볼륨
    private float _currentBGMBaseVolume = 1f;

    [Header("소리 뭉개짐 방지 세팅")]
    public float sfxCooldown = 0.05f;
    private Dictionary<AudioClip, float> clipLastPlayTime = new Dictionary<AudioClip, float>();

    [Header("오디오 채널 (자동 생성됨)")]
    [SerializeField] private AudioMixerGroup BGMMixer;
    [SerializeField] private AudioMixerGroup SFXMixer;
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
        bgmSource.outputAudioMixerGroup = BGMMixer; // BGM 믹서 그룹 연결

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
        source.outputAudioMixerGroup = SFXMixer; // SFX 믹서 그룹 연결

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
        if (clip == null || bgmSource == null) return;

        // 이미 같은 곡이 재생 중이라면 처음부터 다시 덮어씌우지 않고 유지
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        _currentBGMBaseVolume = volume;
        bgmSource.clip = clip;
        bgmSource.volume = _currentBGMBaseVolume * masterBGMVolume;
        bgmSource.Play();
    }

    /// <summary>
    /// BGM 마스터 볼륨을 실시간으로 변경합니다. (UI 환경설정 옵션 창과 연결)
    /// </summary>
    public void SetMasterBGMVolume(float volume)
    {
        masterBGMVolume = Mathf.Clamp01(volume);

        // 현재 BGM이 재생 중이라면 변경된 마스터 볼륨을 즉시 소리에 반영
        if (bgmSource != null)
        {
            bgmSource.volume = _currentBGMBaseVolume * masterBGMVolume;
        }
    }

    /// <summary>
    /// 재생 중인 배경음악을 멈춥니다.
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    /// <summary>
    /// 효과음을 재생합니다. (오브젝트 풀링 작동)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        // [소리 뭉개짐 방지] 동일한 클립이 너무 짧은 시간 내에 다시 재생되는지 확인
        if (clipLastPlayTime.TryGetValue(clip, out float lastTime))
        {
            if (Time.time - lastTime < sfxCooldown)
            {
                // 0.05초(sfxCooldown)가 지나지 않았다면 재생을 취소하여 뭉개짐을 막습니다.
                return;
            }
        }

        // 현재 재생 시간을 기록 (다음 번 검사를 위해)
        clipLastPlayTime[clip] = Time.time;

        AudioSource availableSource = GetAvailableSFXSource(); // (기존 채널 찾는 함수)
        if (availableSource == null) return;

        availableSource.clip = clip;

        // [볼륨 일괄 조절] 개별 볼륨에 마스터 볼륨을 곱해서 최종 소리를 냅니다.
        availableSource.volume = volume * masterSFXVolume;

        availableSource.pitch = pitch;
        availableSource.Play();
    }

    // 현재 소리가 나지 않는(놀고 있는) 오디오 채널을 찾아주는 함수
    private AudioSource GetAvailableSFXSource()
    {
        // 1. 리스트를 돌면서 재생 중이 아닌 소스를 찾습니다.
        for (int i = sfxPool.Count - 1; i >= 0; i--)
        {
            AudioSource source = sfxPool[i];
            if (source == null)
            {
                sfxPool.RemoveAt(i);
                continue;
            }

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