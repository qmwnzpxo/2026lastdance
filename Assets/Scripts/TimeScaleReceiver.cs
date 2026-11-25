using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CSHPlayer;

/// <summary>
/// Player / Enemy / Clone 오브젝트에 부착.
/// Animator.speed와 Rigidbody2D.simulated를 자동 관리.
/// deltaTime은 각 스크립트에서 CustomTimeManager 프로퍼티를 직접 사용.
/// </summary>
public class TimeScaleReceiver : MonoBehaviour
{
    [SerializeField] private TimeCategory _category;
    private bool _pendingSubscribe = false;

    public Animator _animator;
    public Rigidbody2D _rb;

    void Awake()
    {
        if (_animator == null) { 
            TryGetComponent<Animator>(out var ani);
            _animator = ani;
        }

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (CustomTimeManager.Instance != null)
            Subscribe(true);
        else
            _pendingSubscribe = true;
    }

    void Start()
    {
        if (_pendingSubscribe)
        {
            Subscribe(true);
            _pendingSubscribe = false;
        }

        if (CustomTimeManager.Instance != null)
            OnScaleChanged(CustomTimeManager.Instance.GetTimeScale(_category));
    }

    void OnDisable()
    {
        _pendingSubscribe = false;
        Subscribe(false);
        Unfreeze();
    }

    private void Subscribe(bool on)
    {
        Debug.Log($"[TimeScaleReciver] Subscribed");
        var mgr = CustomTimeManager.Instance;
        if (mgr == null) return;

        // 히트스탑 정지/복원은 카테고리와 무관하게 공통
        if (on) mgr.OnHitstopStateChanged += OnHitstopStateChanged;
        else mgr.OnHitstopStateChanged -= OnHitstopStateChanged;

        Action<float> handler = _category switch
        {
            TimeCategory.Player => OnScaleChanged,
            TimeCategory.Enemy => OnScaleChanged,
            TimeCategory.Ghost => OnScaleChanged,
            _ => null
        };
        if (handler == null) return;

        switch (_category)
        {
            case TimeCategory.Player:
                if (on) mgr.OnPlayerTimeScaleChanged += handler;
                else mgr.OnPlayerTimeScaleChanged -= handler;
                break;
            case TimeCategory.Enemy:
                if (on) mgr.OnEnemyTimeScaleChanged += handler;
                else mgr.OnEnemyTimeScaleChanged -= handler;
                break;
            case TimeCategory.Ghost:
                if (on) mgr.OnCloneTimeScaleChanged += handler;
                else mgr.OnCloneTimeScaleChanged -= handler;
                break;
        }
    }

    private void OnScaleChanged(float scale)
    {
        // 애니메이션 속도
        if (_animator != null)
        {
            _animator.speed = scale;
            Debug.Log(_animator.speed);
        }

        // 물리: 완전 정지면 velocity도 0, 재개 시 복구는 각 이동 스크립트가 담당
        if (_rb != null && scale <= 0f)
            _rb.linearVelocity = Vector2.zero;

    }

    // ─────────────────────────────────────────────────────────
    #region 히트스탑 물리 정지
    // 유니티 물리(중력·속도 적분)는 CustomTimeManager 배율을 모르므로
    // 히트스탑 동안 Rigidbody를 FreezeAll로 묶었다가 끝나면 속도째 복원한다.
    // gravityScale은 건드리지 않는다 (대시·벽타기 등이 직접 바꾸므로 복원 시 꼬임).

    private bool _frozen;
    private int _freezeRequestId;
    private Vector2 _savedVelocity;
    private float _savedAngularVelocity;
    private RigidbodyConstraints2D _savedConstraints;

    private void OnHitstopStateChanged(bool active)
    {
        if (active) FreezeAtEndOfFixedStep(++_freezeRequestId).Forget();
        else Unfreeze();
    }

    /// <summary>
    /// 이번 물리 스텝이 끝난 뒤에 묶는다.
    /// 대시처럼 MovePosition 직후 같은 FixedUpdate에서 히트스탑이 걸리는 경우
    /// 바로 묶으면 그 이동이 씹히기 때문.
    /// </summary>
    private async UniTaskVoid FreezeAtEndOfFixedStep(int requestId)
    {
        bool canceled = await UniTask
            .Yield(PlayerLoopTiming.LastFixedUpdate, destroyCancellationToken)
            .SuppressCancellationThrow();
        if (canceled || requestId != _freezeRequestId) return;
        if (CustomTimeManager.Instance == null || !CustomTimeManager.Instance.IsHitstopActive) return;
        Freeze();
    }

    private void Freeze()
    {
        if (_frozen || _rb == null || !isActiveAndEnabled) return;
        // Kinematic(빨강 칩 재생 등)·비활성 물리는 대상 아님
        if (!_rb.simulated || _rb.bodyType != RigidbodyType2D.Dynamic) return;

        _frozen = true;
        _savedVelocity = _rb.linearVelocity;
        _savedAngularVelocity = _rb.angularVelocity;
        _savedConstraints = _rb.constraints;

        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void Unfreeze()
    {
        _freezeRequestId++; // 대기 중인 Freeze 요청 무효화
        if (!_frozen) return;
        _frozen = false;
        if (_rb == null) return;

        _rb.constraints = _savedConstraints;
        _rb.linearVelocity = _savedVelocity;
        _rb.angularVelocity = _savedAngularVelocity;
    }

    #endregion
}