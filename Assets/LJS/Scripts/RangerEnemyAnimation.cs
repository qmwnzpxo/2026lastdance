using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RangerEnemyAnimation : MonoBehaviour
{
    private Animator _animator;
    private Rigidbody2D _rb;

    // Animator 파라미터 해싱 (문자열보다 성능이 훨씬 좋습니다)
    private readonly int hashIsWalk = Animator.StringToHash("isWalk");
    private readonly int hashIsRun = Animator.StringToHash("isRun");
    private readonly int hashIsHit = Animator.StringToHash("isHit");
    private readonly int hashIsDead = Animator.StringToHash("isDead");

    [Header("이동 애니메이션 설정")]
    [Tooltip("이 속도 이상이면 걷기로 판정")]
    [SerializeField] private float walkSpeedThreshold = 0.1f;
    [Tooltip("이 속도 이상이면 뛰기로 판정 (Controller의 moveSpeed와 맞춰주세요)")]
    [SerializeField] private float runSpeedThreshold = 3.0f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        // 애니메이터와 같은 오브젝트나 부모에 있는 Rigidbody2D를 가져옵니다.
        _rb = GetComponentInParent<Rigidbody2D>();
    }

    public void Tick() // called by EnemyBTController via EnemyManager
    {
        UpdateMovementAnimations();
    }

    /// <summary>
    /// 실제 이동 속도에 맞춰 걷기/뛰기 애니메이션을 자동 전환합니다.
    /// </summary>
    private void UpdateMovementAnimations()
    {
        if (_rb == null || _animator == null) return;

        // 2D 횡스크롤이므로 X축 이동 속도의 절댓값을 기준으로 삼습니다.
        // (유니티 6 이전 버전이라면 _rb.linearVelocity 대신 _rb.velocity를 사용하세요)
        // 적 이동 속도에는 적 배율(슬로우·히트스톱)이 곱해져 있으므로 나눠서 원래 속도로 판정.
        // 안 나누면 슬로우 중 Run→Walk→Idle로 바뀜 (재생 속도는 TimeScaleReceiver가 Animator.speed로 따로 맞춤)
        var tmg = CustomTimeManager.Instance;
        float enemyScale = tmg != null ? tmg.EnemyTimeScale : 1f;
        if (enemyScale <= 0.0001f) return; // 완전 정지 중엔 이전 상태 유지
        float currentSpeed = Mathf.Abs(_rb.linearVelocity.x) / enemyScale;

        // 속도에 따른 상태 판별
        bool isWalking = currentSpeed > walkSpeedThreshold && currentSpeed <= runSpeedThreshold;
        bool isRunning = currentSpeed > runSpeedThreshold;

        // 애니메이터 파라미터 적용
        _animator.SetBool(hashIsWalk, isWalking);
        _animator.SetBool(hashIsRun, isRunning);
    }

    /// <summary>
    /// 피격 시 호출할 메서드 (Trigger 방식)
    /// </summary>
    public void TriggerHit()
    {
        if (_animator == null) return;
        _animator.SetTrigger(hashIsHit);
    }

    /// <summary>
    /// 사망 시 호출할 메서드 (Bool 방식)
    /// </summary>
    public void SetDead(bool isDead = true)
    {
        if (_animator == null) return;
        _animator.SetBool(hashIsDead, isDead);
    }
}