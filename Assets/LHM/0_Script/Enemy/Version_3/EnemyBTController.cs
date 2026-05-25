using BTActions;
using BTConditions;
using EnemyVersion3.Interfaces;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(TimeScaleReceiver))]
public class EnemyBTController : MonoBehaviour
{
    [Header("탐지")]
    [SerializeField] protected float _detectionRange;
    [SerializeField] protected float _chaseMaintenanceRange;
    [SerializeField] protected LayerMask _groundLayer; 

    [Header("추격 게이지")]
    [SerializeField] protected float _gaugeIncreaseRate;
    [SerializeField] protected float _gaugeDecreaseRate;
    [SerializeField] protected float _chaseEndDuration;
    [RuntimeOnly,SerializeField] protected float _currentgauge; // 디버그용

    [Header("공격")]
    [SerializeField] protected float _attackRange;
    [SerializeField] protected float _attackCooldown;
    [SerializeField] protected float _attackWindupTime;   // 공격 선 딜레이
    [SerializeField] protected float _attackRecoveryTime; // 공격 후 딜레이

    [Header("스턴")]
    [SerializeField] protected float _stunDuration;

    protected BTNode _root;
    protected EnemyBlackboard _bb;
    protected EnemyState _state;

    [HideInInspector] public bool requireInit = true;
    // ─── 외부 API ───────────────────────────────
    public EnemyBlackboard _BB { get => _bb; }


    // ────────────────────────────────────────────
    #region Unity 생명주기

    protected virtual void Awake()
    {
        if (_groundLayer == 0)
            _groundLayer = LayerMask.GetMask("ground");
        if (requireInit)
            ReInit();
        if(_state == null)
            _state=GetComponent<EnemyState>();
    }

    protected virtual void FixedUpdate()
    {
        if (_bb != null&&_bb.enemyState.enabled)
        {
            UpdateBlackboard();
            _root.Tick(_bb);
            _currentgauge = _bb.detectGauge; // 디버그용
        }
    }

    public virtual void ReInit()
    {
        InitBlackboad();
        _root = BuildTree();
        requireInit = false;
        _state.Init();
    }

    protected virtual void OnDestroy()
    {
        _bb?.CancelAction();
    }

    protected virtual void OnEnable()
    {
        if (requireInit)
            ReInit();
    }

    public virtual void OnDisable()
    {
        _bb.CancelAction();
        _root = null;
        _bb = null;
        requireInit = true;
    }

    #endregion

    // ────────────────────────────────────────────
    #region 트리 구성

    protected virtual BTNode BuildTree()
    {
        return new BTSelector(
            // 우선순위 1: 스턴
            new BTSequence(
                new ConditionIsStunned(),
                new ActionStun()
            ),
            // 우선순위 2: 후퇴
            new BTSequence(
                new ConditionNeedsRetreat(),
                new ConditionHasMoveable(),
                new ActionRetreat()
            ),
            // 우선순위 3: 공격
            new BTSequence(
                new ConditionIsChasing(),
                new ConditionInAttackRange(),
                new ConditionHasLOS(),
                new ConditionAttackCooldownReady(),
                new ActionAttack()
            ),
            // 우선순위 4: 추격
            new BTSequence(
                new ConditionIsChasing(),
                new ActionChase()
            ),
            // 우선순위 5: 순찰 (fallback) or 대기(현재는 순찰이 기본형태)
            new ActionPatrol()
        );
    }

    #endregion

    // ────────────────────────────────────────────
    #region Blackboard 초기화 (BT 밖에서 처리)

    protected virtual void InitBlackboad()
    {
        _bb = new EnemyBlackboard
        {
            selfTransform = transform,
            rigid2D = GetComponent<Rigidbody2D>(),
            playerTransform = GameObject.FindWithTag("Player")?.transform,
            enemyState = GetComponent<EnemyState>(),
            detectionRange = _detectionRange,
            groundLayer = _groundLayer,
            chaseMaintenanceRange = _chaseMaintenanceRange,
            gaugeIncreaseRate = _gaugeIncreaseRate,
            gaugeDecreaseRate = _gaugeDecreaseRate,
            chaseEndDuration = _chaseEndDuration,
            attackRange = _attackRange,
            attackCooldown = _attackCooldown,
            stunDuration = _stunDuration,
            moveable = GetComponent<IMoveable>(),
            enemyCombat = GetComponent<EnemyCombat>(),
        };

    }
    #endregion

    // ────────────────────────────────────────────
    #region Blackboard 갱신 (BT 밖에서 처리)

    protected virtual void UpdateBlackboard()
    {
        float dist = _bb.DistanceToPlayer;
        bool playerInDetection = dist <= _bb.detectionRange;
        bool playerInMaintenance = dist <= _bb.chaseMaintenanceRange;

        _bb.hasLOS = CheckLOS();

        // 추격 게이지 증감
        if (playerInDetection && _bb.hasLOS)
        {
            _bb.detectGauge = Mathf.Min(
                _bb.detectGauge + _bb.gaugeIncreaseRate * Time.fixedDeltaTime,
                _bb.maxDetectGauge);
        }
        else if (_bb.isDetected && !playerInMaintenance)
        {
            _bb.detectGauge = Mathf.Max(
                _bb.detectGauge - _bb.gaugeDecreaseRate * Time.fixedDeltaTime,
                0f);
        }

        // 게이지 100 도달 → 추격 시작
        if (!_bb.isDetected && _bb.detectGauge >= _bb.maxDetectGauge)
            _bb.isDetected = true;

        // 추격 유지 범위 이탈 → 해제 타이머
        if (_bb.isDetected && !playerInMaintenance)
        {
            _bb.chaseEndTimer += Time.fixedDeltaTime;
            if (_bb.chaseEndTimer >= _bb.chaseEndDuration)
            {
                _bb.isDetected = false;
                _bb.detectGauge = 0f;
                _bb.chaseEndTimer = 0f;
            }
        }
        else
        {
            _bb.chaseEndTimer = 0f;
        }
    }

    private bool CheckLOS()
    {
        if (_bb.playerTransform == null) return false;

        Vector2 origin = _bb.selfTransform.position;
        Vector2 target = _bb.playerTransform.position;
        Vector2 dir = target - origin;
        float dist = dir.magnitude;

        if (dist < 0.01f) return true;

        RaycastHit2D hit = Physics2D.Raycast(
            origin, dir.normalized, dist, _bb.groundLayer);

        return hit.collider == null;
    }

    public void CancelCurrentAction() =>_bb.CancelAction();

    public void ApplyStun()
    {
        _bb.stunDuration = _stunDuration;
        _bb.isStunned = true;
        _bb.CancelAction(); // 현재 진행 중인 액션 즉시 중단
    }

    #endregion

    // ────────────────────────────────────────
    #region Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _chaseMaintenanceRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

    }

    #endregion

}