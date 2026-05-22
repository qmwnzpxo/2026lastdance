using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 플레이어가 트리거에 진입하면 게이트를 닫고, 방 안의 적이 전멸하면 게이트를 연다.
/// 락다운 시작·종료 시 CameraFollow에 카메라 전환을 위임한다.
/// </summary>
public class LockdownRoom : MonoBehaviour
{
    [Header("게이트 (입출구 콜라이더)")]
    [SerializeField] private Collider2D[] _gates;

    [Header("카메라 기준점")]
    [SerializeField] private Transform _roomCenter;     // 락다운 중 카메라가 바라볼 위치
    [SerializeField] private float _lockdownOrthoSize = 10f;  // 락다운 시 줌아웃 크기
    [SerializeField] private float _zoomDuration = 0.4f;

    [Header("클리어 조건 - 적 목록")]
    [SerializeField] private EnemyState[] _enemies;

    // ── 내부 상태 ──────────────────────────
    private bool _isLocked = false;
    private CameraFollow _cameraFollow;
    private CancellationTokenSource _cts;

    // ────────────────────────────────────────
    private void Awake()
    {
        // 메인 카메라의 부모에 CameraFollow가 있는 구조 (기존 코드 기준)
        _cameraFollow = Camera.main.transform.parent.GetComponent<CameraFollow>();
        SetGatesActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isLocked || !other.CompareTag("Player")) return;
        StartLockdown();
    }

    // ── 락다운 시작 ──────────────────────────
    private void StartLockdown()
    {
        _isLocked = true;
        SetGatesActive(true);

        _cameraFollow.StartLockdown(_roomCenter, _lockdownOrthoSize, _zoomDuration);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        WaitForAllEnemiesDead(_cts.Token).Forget();
    }

    // ── 전멸 대기 ────────────────────────────
    private async UniTaskVoid WaitForAllEnemiesDead(CancellationToken ct)
    {
        await UniTask.WaitUntil(AllEnemiesDead, cancellationToken: ct)
                     .SuppressCancellationThrow();

        if (!ct.IsCancellationRequested)
            EndLockdown();
    }

    private bool AllEnemiesDead()
    {
        foreach (var enemy in _enemies)
        {
            if (enemy != null && enemy.gameObject.activeSelf)
                return false;
        }
        return true;
    }

    // ── 락다운 해제 ──────────────────────────
    private void EndLockdown()
    {
        SetGatesActive(false);
        _cameraFollow.EndLockdown(_zoomDuration);
    }

    // ── 게이트 토글 ──────────────────────────
    private void SetGatesActive(bool active)
    {
        foreach (var gate in _gates)
        {
            if (gate != null) gate.enabled = active;
        }
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Gizmos ───────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (_roomCenter == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(_roomCenter.position, _lockdownOrthoSize * 0.5f);
    }
}