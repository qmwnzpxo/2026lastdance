using EnemyVersion3;
using System.Threading;
using UnityEngine;

public class EnemyBlackboard
{
    // ── 참조 ────────────────────────────────────
    public Transform selfTransform;
    public Rigidbody2D rigid2D;
    public Transform playerTransform;
    public EnemyState enemyState;
    public EnemyCombat enemyCombat;

    // ── 탐지 ────────────────────────────────────
    public float detectionRange = 8f;
    public float chaseMaintenanceRange = 10f;
    public LayerMask groundLayer;
    public bool hasLOS = false; // Line of Sight (시야 확보 여부)

    // ── 탐지 게이지 ─────────────────────────────
    public float detectGauge = 0f;
    public float maxDetectGauge = 100f;
    public float gaugeIncreaseRate = 50f;   // 초당
    public float gaugeDecreaseRate = 100f;  // 초당
    public bool isDetected = false;

    // ── 탐지 해제 타이머 ────────────────────────
    public float chaseEndTimer = 0f;
    public float chaseEndDuration = 1f;

    // ── 공격 ────────────────────────────────────
    public float attackRange;
    public float attackCooldown;
    public float lastAttackTime = float.MinValue;

    // ── 스턴 ────────────────────────────────────
    public bool isStunned = false;
    public float stunDuration = 1f;
    public float stunDecelerationRate = 1f; // 스턴 지속시간 감소 가속도(기계들의 경우 일종의 롤 강인함 개념)

    // ── 비동기 액션 관리 ────────────────────────
    public string runningActionId = null;
    public CancellationTokenSource actionCts;
    public bool actionCompleted = false;
    public NodeState lastActionResult = NodeState.Failure;

    // ── 캐시 프로퍼티 ───────────────────────────
    public float DistanceToPlayer =>
        playerTransform != null
            ? Vector2.Distance(selfTransform.position, playerTransform.position)
            : float.MaxValue;

    public bool AttackCooldownReady =>
        Time.time - lastAttackTime >= attackCooldown;

    // ── 인터페이스 ───────────────────────────────
    public IMoveable moveable;

    // ── 유틸 ────────────────────────────────────
    public virtual void CancelAction()
    {
        actionCts?.Cancel();
        actionCts?.Dispose();
        actionCts = null;
        runningActionId = null;
        actionCompleted = false;
    }
}