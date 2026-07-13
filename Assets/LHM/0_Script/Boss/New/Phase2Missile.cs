using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// Phase 2 미사일 컴포넌트 — Phase2BossMissilePattern에 의해 초기화되는 발사체
/// ─────────────────────────────────────────────────────────────────────────────
/// - 수평 직선 이동 (중력 없음)
/// - 플레이어 태그 충돌 시 PlayerHealth.TakeDamage() 호출
/// - Wall 태그 충돌 시 자동 회수
/// - AUTO_RETURN_DELAY 초 후 화면 밖에 있어도 자동 회수 (메모리 누수 방지)
/// - OnDisable에서 Rigidbody2D 속도 초기화 (풀 재사용 안전)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Phase2Missile : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("미사일에 붙어있는 트레일 / VFX 파티클 시스템 (선택사항).")]
    [SerializeField] private ParticleSystem trailVFX;

    // ─── 런타임 상태 ───────────────────────────────────────────
    private Vector2           _direction;
    private float             _speed;
    private float             _damage;
    private LayerMask         _playerLayer;
    private Queue<GameObject> _returnPool;

    private Rigidbody2D _rb;
    private bool        _consumed = false;

    private const float AUTO_RETURN_DELAY = 6f;

    // ─── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        // isTrigger 확인 보조 메시지
        var col = GetComponent<BoxCollider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning("[Phase2Missile] BoxCollider2D의 Is Trigger가 꺼져 있습니다. 켜주세요.");
    }

    private void OnEnable()
    {
        _consumed = false;
        if (trailVFX != null)
            trailVFX.Play(withChildren: true);

        // 화면 밖으로 나가도 자동 회수 (CancellationToken: OnDestroy 때 자동 취소)
        AutoReturnAsync(AUTO_RETURN_DELAY, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnDisable()
    {
        _consumed          = true;
        _rb.linearVelocity = Vector2.zero;

        if (trailVFX != null)
            trailVFX.Stop(withChildren: true,
                          stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // ─── 공개 초기화 ───────────────────────────────────────────

    /// <summary>
    /// Phase2BossMissilePattern.LaunchMissile()에서 호출.
    /// 방향·속도·대미지·풀 참조를 주입하고 즉시 이동 시작.
    /// </summary>
    public void Initialize(
        Vector2           direction,
        float             speed,
        float             damage,
        Queue<GameObject> returnPool,
        LayerMask         playerLayer)
    {
        _direction   = direction.normalized;
        _speed       = speed;
        _damage      = damage;
        _returnPool  = returnPool;
        _playerLayer = playerLayer;
        _consumed    = false;

        _rb.linearVelocity = _direction * _speed;
    }

    // ─── 충돌 판정 ─────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed) return;

        if (other.CompareTag("Player"))
        {
            // PlayerHealth만 호출 (IDamageable 병행 호출 시 2배 데미지 버그 발생)
            // 기존 프로젝트 컨벤션: Charge/Stomp 패턴과 동일하게 PlayerHealth 사용
            other.GetComponent<PlayerHealth>()?.TakeDamage(_damage);
            ConsumeAndReturn();

        }
        else if (other.CompareTag("Wall") || other.CompareTag("Ground"))
        {
            ConsumeAndReturn();
        }
    }

    // ─── 풀 반환 ───────────────────────────────────────────────

    private void ConsumeAndReturn()
    {
        if (_consumed) return;
        _consumed = true;

        if (_returnPool != null)
        {
            gameObject.SetActive(false);
            _returnPool.Enqueue(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private async UniTaskVoid AutoReturnAsync(float delay, CancellationToken ct)
    {
        try
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                cancellationToken: ct
            );

            if (!_consumed)
            {
                Debug.Log("[Phase2Missile] 자동 회수 (화면 밖으로 이탈)");
                ConsumeAndReturn();
            }
        }
        catch (System.OperationCanceledException)
        {
            // 오브젝트 파괴 시 정상 취소 — 무시
        }
    }
}
