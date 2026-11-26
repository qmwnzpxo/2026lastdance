using UnityEngine;

public class ShotSound_Terret : MonoBehaviour
{
    [Header("터렛 사운드 설정")]
    [SerializeField] private AudioClip shootSound;

    // 터렛은 기계니까 음높이(피치) 변화를 적게 주거나 안 줘도 좋습니다.
    [SerializeField] private float minPitch = 0.7f;
    [SerializeField] private float maxPitch = 0.75f;

    private TurretEnemyCombat _combat;

    private void Awake()
    {
        // 같은 오브젝트에 있는 터렛 전투 스크립트 찾기
        _combat = GetComponent<TurretEnemyCombat>();
        if (_combat == null)
        {
            Debug.LogWarning($"[ShotSound_Terret] {name}에 TurretEnemyCombat 컴포넌트가 없습니다.");
        }
    }

    private void OnEnable()
    {
        // 활성화될 때 사격 이벤트 연결
        if (_combat != null)
        {
            _combat.OnShootEvent += PlayShootSound;
        }
    }

    private void OnDisable()
    {
        // 비활성화될 때 사격 이벤트 연결 해제 (에러 방지)
        if (_combat != null)
        {
            _combat.OnShootEvent -= PlayShootSound;
        }
    }

    private void PlayShootSound()
    {
        if (shootSound == null) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"[ShotSound_Terret] AudioManager.Instance가 없어 사격 사운드를 재생할 수 없습니다.");
            return;
        }

        float randomPitch = Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
        // 기존에 잘 만들어둔 AudioManager를 활용해 소리 재생!
        AudioManager.Instance.PlaySFX(shootSound, 1.0f, randomPitch);
    }
}
