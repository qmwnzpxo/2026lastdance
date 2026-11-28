using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 트리거 콜라이더로 부활 위치를 기록하고, 사망 시 Death 모션 후 해당 위치로 재시작한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DeathRestart : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private string playerTag = "Player";

    [Header("재시작 연출 설정")]
    [Tooltip("Death 모션 출력 후 부활 위치로 이동하기까지 대기 시간 (초)")]
    [SerializeField] private float deathMotionDuration = 0.9f;

    // 씬 내 마지막으로 닿은 체크포인트 공유 상태
    private static DeathRestart _activeCheckpoint;
    private static Vector3? _savedPosition;
    private static Transform _playerTransform;
    private static PlayerState _playerState;           // PlayerState: HP·사망 상태 감지 및 회복
    private static PlayerAnimation _playerAnimation;   // PlayerAnimation: Death()로 사망 모션 출력
    private static Rigidbody2D _playerRb;              // Rigidbody2D: 순간이동 직후 관성 속도 제거
    private static bool _isHandlingDeath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _activeCheckpoint = null;
        _savedPosition = null;
        _playerTransform = null;
        _playerState = null;
        _playerAnimation = null;
        _playerRb = null;
        _isHandlingDeath = false;
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[DeathRestart] {name}: Collider2D의 Is Trigger가 꺼져 있습니다.", this);
        }
    }

    private void OnDestroy()
    {
        if (_activeCheckpoint == this)
        {
            _activeCheckpoint = null;
            _isHandlingDeath = false;
        }
    }

    private void Update()
    {
        if (_activeCheckpoint != this || _isHandlingDeath || _playerState == null) return;

        // PlayerState: 체력 0 이하 또는 사망 상태 감지
        if (_playerState.currentHP <= 0f || _playerState.isDead)
        {
            RestartAfterDeathAsync().Forget();
        }
    }

    // Collider2D(Trigger): 플레이어 진입 시 위치 기록
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        CachePlayerComponents(other);
        _savedPosition = transform.position;
        _activeCheckpoint = this;
    }

    private static void CachePlayerComponents(Collider2D playerCollider)
    {
        PlayerState state = playerCollider.GetComponent<PlayerState>()
                            ?? playerCollider.GetComponentInParent<PlayerState>();

        Transform rootTransform = state != null ? state.transform : playerCollider.transform;
        _playerTransform = rootTransform;
        _playerState = state;
        _playerAnimation = rootTransform.GetComponent<PlayerAnimation>()
                           ?? rootTransform.GetComponentInChildren<PlayerAnimation>();
        _playerRb = rootTransform.GetComponent<Rigidbody2D>();
    }

    private async UniTaskVoid RestartAfterDeathAsync()
    {
        if (_isHandlingDeath || !_savedPosition.HasValue || _playerTransform == null) return;

        _isHandlingDeath = true;
        CancellationToken ct = this.GetCancellationTokenOnDestroy();

        try
        {
            // PlayerState.Die() 미호출 시 직접 사망 모션 실행
            if (_playerState != null && !_playerState.isDead)
            {
                _playerAnimation?.Death();
            }

            if (deathMotionDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(deathMotionDuration),
                    ignoreTimeScale: true,
                    cancellationToken: ct);
            }

            if (_playerTransform == null || !_savedPosition.HasValue) return;

            _playerTransform.position = _savedPosition.Value;

            if (_playerRb != null)
            {
                _playerRb.linearVelocity = Vector2.zero;
            }

            // CameraFollow: 부활 직후 카메라 즉시 이동
            CameraFollow.SnapMainToTarget();

            if (_playerState != null)
            {
                _playerState.currentHP = _playerState.maxHP;
                _playerState.stamina = _playerState.maxStamina;
                //_playerState.dashGauge = _playerState.MaxDashGauge;
                _playerState.isDead = false;
                _playerState.isHit = false;
            }
        }
        catch (OperationCanceledException)
        {
            // 오브젝트 파괴 시 정상 취소
        }
        finally
        {
            _isHandlingDeath = false;
        }
    }
}

