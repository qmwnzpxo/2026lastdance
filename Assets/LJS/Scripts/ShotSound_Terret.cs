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
        if (shootSound != null)
        {
            float randomPitch = Random.Range(minPitch, maxPitch);
            // 기존에 잘 만들어둔 AudioManager를 활용해 소리 재생!
            AudioManager.Instance.PlaySFX(shootSound, 1.0f, randomPitch);
        }
    }
}
