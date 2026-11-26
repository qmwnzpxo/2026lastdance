using UnityEngine;

/// <summary>
/// 원거리 적(RangerEnemyCombat)과 터렛(TurretEnemyCombat)의 사격 사운드를 통합 처리하는 컴포넌트.
/// </summary>
public class EnemyShotSound : MonoBehaviour
{
    [Header("사운드 설정")]
    [SerializeField] private AudioClip shootSound;
    [Range(0f, 1f)] [SerializeField] private float volume = 1.0f;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    [Header("전투 컴포넌트 참조 (비워두면 자동 탐색)")]
    [SerializeField] private RangerEnemyCombat rangerCombat;
    [SerializeField] private TurretEnemyCombat turretCombat;

    private void Awake()
    {
        if (rangerCombat == null) rangerCombat = GetComponent<RangerEnemyCombat>();
        if (turretCombat == null) turretCombat = GetComponent<TurretEnemyCombat>();

        if (rangerCombat == null && turretCombat == null)
        {
            Debug.LogWarning($"[EnemyShotSound] {name}에 RangerEnemyCombat 또는 TurretEnemyCombat 컴포넌트가 없습니다.", this);
        }
    }

    private void OnEnable() => SetEventSubscription(true);

    private void OnDisable() => SetEventSubscription(false);

    private void SetEventSubscription(bool subscribe)
    {
        if (rangerCombat != null)
        {
            if (subscribe) rangerCombat.OnShootEvent += PlayShootSound;
            else rangerCombat.OnShootEvent -= PlayShootSound;
        }

        if (turretCombat != null)
        {
            if (subscribe) turretCombat.OnShootEvent += PlayShootSound;
            else turretCombat.OnShootEvent -= PlayShootSound;
        }
    }

    private void PlayShootSound()
    {
        if (shootSound == null) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"[EnemyShotSound] AudioManager.Instance가 없어 '{shootSound.name}'을 재생할 수 없습니다.", this);
            return;
        }

        float min = Mathf.Min(minPitch, maxPitch);
        float max = Mathf.Max(minPitch, maxPitch);
        float randomPitch = Random.Range(min, max);
        AudioManager.Instance.PlaySFX(shootSound, volume, randomPitch);
    }
}
