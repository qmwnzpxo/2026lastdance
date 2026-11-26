using UnityEngine;

public class CharactersAudio : MonoBehaviour
{
    [Header("단일 사운드")]
    public AudioClip Run1;
    public AudioClip Run2;
    public AudioClip Run3;
    public AudioClip Run4;
    public AudioClip Flash;
    public AudioClip DownFlash;
    public AudioClip Promotion;
    public AudioClip EnemyShot;

    [Header("다중/랜덤 사운드 배열")]
    public AudioClip[] randomClips;

    [Header("랜덤 사운드 피치 세팅")]
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    public void Run1Event() => PlayClip(Run1, 0.8f);
    public void Run2Event() => PlayClip(Run2, 0.8f);
    public void Run3Event() => PlayClip(Run3, 0.8f);
    public void Run4Event() => PlayClip(Run4, 0.8f);
    public void FlashEvent() => PlayClip(Flash, 1.0f);
    public void DownFlashEvent() => PlayClip(DownFlash, 1.0f);
    public void PromotionEvent() => PlayClip(Promotion, 1.0f);
    public void EnemyShotEvent() => PlayClip(EnemyShot, 1.0f);

    public void PlayRandomSoundEvent()
    {
        // 1. 배열 자체를 안 쓰거나(Length == 0), 비워뒀다면 아무 일도 하지 않고 조용히 종료합니다.
        if (randomClips == null || randomClips.Length == 0) return;

        // 2. 등록된 사운드 중 하나를 무작위로 고릅니다.
        int randomIndex = Random.Range(0, randomClips.Length);
        AudioClip selectedClip = randomClips[randomIndex];

        // 3. 뽑힌 클립이 비어있지 않다면 피치를 섞어서 재생합니다.
        float min = Mathf.Min(minPitch, maxPitch);
        float max = Mathf.Max(minPitch, maxPitch);
        PlayClip(selectedClip, 1.0f, Random.Range(min, max));
    }

    private void PlayClip(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"[CharactersAudio] AudioManager.Instance가 없어 '{clip.name}'을 재생할 수 없습니다.");
            return;
        }

        AudioManager.Instance.PlaySFX(clip, volume, pitch);
    }
}
