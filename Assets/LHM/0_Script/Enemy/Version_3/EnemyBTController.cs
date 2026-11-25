using EnemyVersion3;
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

    [Header("스턴")]
    [SerializeField] protected float _stunDuration;

    protected BTNode _root;
    protected EnemyBlackboard _bb;
    protected EnemyState _state;

    [HideInInspector] public bool requireInit = true;
    // ─── 외부 API ───────────────────────────────
    public EnemyBlackboard _BB { get => _bb; }
    public BTNode _Root { get => _root; }
    public NodeState NodeState;


    // ────────────────────────────────────────────
    #region Unity 생명주기

    protected virtual void Awake()
    {
        if (_groundLayer == 0)
            _groundLayer = LayerMask.GetMask("ground");
        if(_state == null)
            _state=GetComponent<EnemyState>();
        if (requireInit)
            ReInit();
    }

    public virtual void ReInit()
    {
        // 기존 블랙보드에서 돌던 액션(UniTask)을 먼저 끊는다.
        // 안 끊으면 옛 토큰으로 계속 돌다가 오브젝트 파괴 후 MissingReferenceException 발생.
        _bb?.CancelAction();
        InitBlackboad();
        _root = BuildTree();
        requireInit = false;
        _state.Init();

        if (_armAim == null) _armAim = GetComponent<EnemyArmAim>();
        if (_armAim != null) _armAim.EnemyBlackBoard = _bb;
        if (_anim == null) _anim = GetComponentInChildren<RangerEnemyAnimation>();
    }

    protected virtual void OnDestroy()
    {
        _bb?.CancelAction();
    }

    protected virtual void OnEnable()
    {
        if (requireInit)
            ReInit();
        EnemyManager.Register(this);
        RefreshPause();
    }

    public virtual void OnDisable()
    {
        EnemyManager.Unregister(this);
        _bb?.CancelAction();
        _root = null;
        _bb = null;
        requireInit = true;
        _paused = false;
    }

    #endregion

    // ────────────────────────────────────────────
    #region EnemyManager Tick (Update 대체)

    protected EnemyArmAim _armAim;
    protected RangerEnemyAnimation _anim;

    [SerializeField] private bool _behaviourEnabled = true;
    [RuntimeOnly, SerializeField] private bool _paused; // 디버그용

    /// <summary>이 적 개별 행동 on/off. 끄면 진행 중인 액션이 취소된다.</summary>
    public bool BehaviourEnabled
    {
        get => _behaviourEnabled;
        set { _behaviourEnabled = value; RefreshPause(); }
    }

    /// <summary>개별 on && 전체 on 일 때만 행동한다.</summary>
    public bool IsBehaviourRunning => _behaviourEnabled && EnemyManager.IsGloballyActive;

    /// <summary>on/off 상태가 바뀌었으면 정지/재개 처리. EnemyManager가 호출.</summary>
    public void RefreshPause()
    {
        bool shouldPause = !IsBehaviourRunning;
        if (shouldPause == _paused) return;
        _paused = shouldPause;
        if (_paused) OnBehaviourPaused();
        else OnBehaviourResumed();
    }

    /// <summary>
    /// 정지: BT 액션(UniTask)은 Tick과 별개로 돌기 때문에 반드시 취소해야 멈춘다.
    /// 탐지 게이지 등 블랙보드 값은 유지 → 재개 시 BT가 현재 상태로 브랜치를 다시 고른다.
    /// (스턴 중이었다면 스턴은 해제된다)
    /// </summary>
    protected virtual void OnBehaviourPaused()
    {
        if (_bb == null) return;
        _bb.CancelAction();
        if (_bb.rigid2D != null)
            _bb.rigid2D.linearVelocity = new Vector2(0f, _bb.rigid2D.linearVelocity.y);
    }

    protected virtual void OnBehaviourResumed() { }

    public virtual void FixedTick()
    {
        if (_paused || _bb == null || !_bb.enemyState.enabled) return;

        UpdateBlackboard();
        NodeState = _root.Evaluate(_bb);
        _currentgauge = _bb.detectGauge; // 디버그용
    }

    public virtual void Tick()
    {
        if (!_paused && _armAim != null && _armAim.enabled) _armAim.Tick();
        // 애니메이션은 속도 기반 표시라 정지 중에도 갱신 (멈추면 idle로 전환됨)
        if (_anim != null && _anim.isActiveAndEnabled) _anim.Tick();
    }

    public virtual void LateTick()
    {
        if (!_paused && _armAim != null && _armAim.enabled) _armAim.LateTick();
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

    public void ApplyStun(float duration = 0f)
    {
        _bb.stunDuration = duration > 0f ? duration : _stunDuration;
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