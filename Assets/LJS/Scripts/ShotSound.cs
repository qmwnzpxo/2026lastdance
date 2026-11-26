using UnityEngine;

public class ShotSound : MonoBehaviour
{
    [Header("사운드 설정")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    private RangerEnemyCombat _combat;

    private void Awake()
    {
        // 같은 오브젝트에 있는 전투 스크립트를 찾아 연결합니다.
        _combat = GetComponent<RangerEnemyCombat>();
        if (_combat == null)
        {
            Debug.LogWarning($"[ShotSound] {name}에 RangerEnemyCombat 컴포넌트가 없습니다.");
        }
    }

    private void OnEnable()
    {
        // 오브젝트가 활성화될 때 이벤트를 구독(연결)합니다.
        if (_combat != null)
        {
            _combat.OnShootEvent += PlayShootSound;
        }
    }

    private void OnDisable()
    {
        // 에러 및 메모리 누수 방지를 위해 비활성화될 때는 반드시 구독을 해제합니다.
        if (_combat != null)
        {
            _combat.OnShootEvent -= PlayShootSound;
        }
    }

    // 전투 스크립트에서 사격 신호를 보내면 자동으로 실행되는 함수입니다.
    private void PlayShootSound()
    {
        if (shootSound == null) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"[ShotSound] AudioManager.Instance가 없어 사격 사운드를 재생할 수 없습니다.");
            return;
        }

        // 총소리도 살짝 피치를 섞어주면 연사할 때 기계음처럼 들리지 않아 좋습니다.
        float randomPitch = Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
        AudioManager.Instance.PlaySFX(shootSound, 1.0f, randomPitch);
    }
}
