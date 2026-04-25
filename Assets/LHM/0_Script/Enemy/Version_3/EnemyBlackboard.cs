using EnemyVersion3.Interfaces;
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
    public float attackRange = 5f;
    public float attackCooldown = 2f;
    public float attackWindupTime = 0.1f;   // 공격 시작 전
    public float attackRecoveryTime = 0.08f;// 후딜
    public float lastAttackTime = float.MinValue;

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