using BTActions;
using BTConditions;
using EnemyVersion3.Interfaces;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyBTController : MonoBehaviour
{
    [Header("탐지")]
    [SerializeField] protected float _detectionRange;
    [SerializeField] protected float _chaseMaintenanceRange;

    [Header("추격 게이지")]
    [SerializeField] protected float _gaugeIncreaseRate;
    [SerializeField] protected float _gaugeDecreaseRate;
    [SerializeField] protected float _chaseEndDuration;

    [Header("공격")]
    [SerializeField] protected float _attackRange;
    [SerializeField] protected float _attackCooldown;

    [Header("후퇴")]
    [SerializeField] protected float _retreatTriggerRange;
    [SerializeField] protected float _retreatDistance;

    protected BTNode _root;
    protected EnemyBlackboard _bb;

    // ────────────────────────────────────────────
    #region Unity 생명주기

    protected virtual void Awake()
    {
        InitBlackboad();
        _root = BuildTree();
    }

    protected virtual void FixedUpdate()
    {
        UpdateBlackboard();
        _root.Tick(_bb);
    }

    protected virtual void OnDestroy()
    {
        _bb.CancelAction();
    }

    #endregion
    // ────────────────────────────────────────────
    #region 트리 구성

    protected virtual BTNode BuildTree()
    {
        return new BTSelector(
            // 우선순위 1: 후퇴
            new BTSequence(
                new ConditionNeedsRetreat(),
                new ConditionHasMoveable(),
                new ActionRetreat()
            ),
            // 우선순위 2: 공격
            new BTSequence(
                new ConditionIsChasing(),
                new ConditionInAttackRange(),
                new ConditionAttackCooldownReady(),
                new ActionAttack()
            ),
            // 우선순위 3: 추격
            new BTSequence(
                new ConditionIsChasing(),
                new ActionChase()
            ),
            // 우선순위 4: 순찰 (fallback)
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
            detectionRange = _detectionRange,
            chaseMaintenanceRange = _chaseMaintenanceRange,
            gaugeIncreaseRate = _gaugeIncreaseRate,
            gaugeDecreaseRate = _gaugeDecreaseRate,
            chaseEndDuration = _chaseEndDuration,
            attackRange = _attackRange,
            attackCooldown = _attackCooldown,
            moveable = GetComponent<IMoveable>(),
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

        // 추격 게이지 증감
        if (playerInDetection)
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

    #endregion

    // ────────────────────────────────────────
    #region Gizmos
    Color retreatdisColor = new Color(0f, 1f, 0.7f, 1f);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _chaseMaintenanceRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _retreatTriggerRange);

        Gizmos.color = retreatdisColor;
        Gizmos.DrawWireSphere(transform.position, _retreatDistance);
    }
    #endregion

}