using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class EnemyCombat : MonoBehaviour
{
    [Header("딜레이")]
    [SerializeField] private float _preDelay = 0.1f;
    [SerializeField] private float _postDelay = 0.3f;
    public bool _isAttack = false;

    [Header("투사체")]
    [SerializeField] private EnemyBullet _bulletPrefab;
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private float _damage = 1f;

    private string _poolKey;
    private Transform _playerTransform;

    private void Awake()
    {
        _poolKey = _bulletPrefab != null ? _bulletPrefab.poolKey : string.Empty;
    }
    public virtual void Initialize(Transform playerTransform)
    {
        _playerTransform = playerTransform;
    }

    /// <summary>ActionAttack에서 호출. 선딜 → 발사 → 후딜 순서로 실행.</summary>
    /// <remarks>딜레이는 적 배율 시간 기준 (빨강 칩·파랑 QTE 슬로우, 히트스톱 중엔 그만큼 느려짐)</remarks>
    public async UniTask ExecuteAttack(CancellationToken ct)
    {
        _isAttack = true;
        try
        {
            // CustomTime.WaitForScaledSeconds는 취소돼도 예외 없이 끝나므로, 취소 확인은 직접 한다
            await CustomTime.WaitForScaledSeconds(_preDelay, TimeCategory.Enemy, ct);

            ct.ThrowIfCancellationRequested();
            Shoot();
            await CustomTime.WaitForScaledSeconds(_postDelay, TimeCategory.Enemy, ct);
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            _isAttack = false;
        }
    }

    protected virtual void Shoot()
    {
        if (_playerTransform == null)
        {
            Debug.LogWarning($"[EnemyCombat] {name}: playerTransform이 null입니다.");
            return;
        }
        if (string.IsNullOrEmpty(_poolKey))
        {
            Debug.LogWarning($"[EnemyCombat] {name}: poolKey가 비어있습니다.");
            return;
        }

        GameObject obj = EnemyBulletPool.Instance.Get(_poolKey);
        if (obj == null) return; // 풀 최대치 초과 → 공격 취소

        Vector2 firePos = _fireTransform != null
            ? (Vector2)_fireTransform.position
            : (Vector2)transform.position;
        Vector2 direction = ((Vector2)_playerTransform.position - firePos).normalized;

        Debug.Log($"[EnemyCombat] {name}: 공격 시작.");
        obj.transform.SetPositionAndRotation(firePos, Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        obj.GetComponent<EnemyBullet>().Init(direction, _damage);
    }

}
