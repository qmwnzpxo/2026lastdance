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
    // 남은 쿨다운 (적 배율 시간 기준으로 감소). 0 이하면 공격 가능.
    // Time.time 기준 "마지막 공격 시각"을 쓰면 슬로우(빨강 칩·파랑 QTE·히트스톱)를 무시하고,
    // 빨강 칩 되감기 때 기록 중 흐른 실제 시간 때문에 쿨다운을 되돌릴 수 없어서 바꿈
    public float attackCooldownRemaining = 0f;

    // ── 스턴 ────────────────────────────────────
    public bool isStunned = false;
    public float stunDuration = 1f;
    // 이번 스턴에서 지난 시간 (적 배율 시간). ActionStun이 지역 변수 대신 이 값을 써서
    // 빨강 칩 되감기 때 스턴을 처음부터가 아니라 남은 시간만큼 이어서 진행할 수 있게 함
    public float stunElapsed = 0f;
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

    public bool AttackCooldownReady => attackCooldownRemaining <= 0f;

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