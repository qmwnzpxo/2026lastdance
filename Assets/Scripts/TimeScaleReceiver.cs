using System;
using TMPro.Examples;
using UnityEngine;

/// <summary>
/// Player / Enemy / Clone 오브젝트에 부착.
/// Animator.speed와 Rigidbody2D.simulated를 자동 관리.
/// deltaTime은 각 스크립트에서 CustomTimeManager 프로퍼티를 직접 사용.
/// </summary>
public class TimeScaleReceiver : MonoBehaviour
{
    [SerializeField] private TimeCategory _category;

    public Animator _animator;
    public Rigidbody2D _rb;

    void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        Subscribe(true);

        if (CustomTimeManager.Instance != null)
            OnScaleChanged(CustomTimeManager.Instance.GetTimeScale(_category));
    }
    void OnDisable() => Subscribe(false);

    private void Subscribe(bool on)
    {
        Debug.Log($"[TimeScaleReciver] Subscribed");
        var mgr = CustomTimeManager.Instance;
        if (mgr == null) return;

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
            _animator.speed = scale;

        // 물리: 완전 정지면 velocity도 0, 재개 시 복구는 각 이동 스크립트가 담당
        if (_rb != null && scale <= 0f)
            _rb.linearVelocity = Vector2.zero;

        Debug.Log(_animator.speed);
    }
}