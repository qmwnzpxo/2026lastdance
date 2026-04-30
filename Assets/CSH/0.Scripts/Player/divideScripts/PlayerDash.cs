using Cysharp.Threading.Tasks;
using EnemyVersion3.Interfaces;
using System.Collections.Generic;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;

// ※ Script Execution Order: PlayerDash = -1 로 설정할 것
//   (Project Settings > Script Execution Order)
//   → PlayerDash.FixedUpdate 가 PlayerMove.FixedUpdate 보다 먼저 실행되어야 return 분기가 정상 동작
[DefaultExecutionOrder(-1)]
public class PlayerDash : MonoBehaviour
{
    // ── 대쉬 설정 ────────────────────────────
    [Header("대쉬")]
    [SerializeField] private float dashRange = 8f;
    [SerializeField] private float dashDamage = 5f;
    [SerializeField] private float dashSpeed = 50f;
    [SerializeField] private Vector2 dashBoxSize = new Vector2(1f, 0.4f);

    // ── 태클 설정 ────────────────────────────
    [Header("태클 (몸통박치기)")]
    [SerializeField] private float tackleRange = 8f;
    [SerializeField] private float tackleSpeed = 50f;
    [SerializeField] private float tackleDamage = 0f;   // 0 = 무데미지
    [SerializeField] private float tackleCooldown = 1f;
    [SerializeField] private float tackleKnockbackForce = 8f;        // 적 넉백 강도
    [SerializeField] private float tackleSelfKnockbackRatio = 0.33f; // 플레이어 넉백 비율
    
    // ── 초록 칩 설정 ─────────────────────────
    [Header("초록 칩 - 3연 돌진")]
    [SerializeField] private int greenDashCount = 3;
    [SerializeField] private float greenDashRange = 5f;
    [SerializeField] private float greenDashSpeed = 55f;
    [SerializeField] private float greenDashInterval = 0.09f; // 돌진 사이 대기 시간
    
    // ── 파랑 칩 설정 ─────────────────────────
    [Header("파랑 칩 - 대각 대시 + 낙하 강타")]
    [SerializeField] private float blueFallSpeed = 80f;   // 낙하 속도
    [SerializeField] private float blueSlamRadius = 3f;    // 착지 범위 공격 반지름
    [SerializeField] private float blueSlamDamage = 8f;    // 착지 범위 공격 데미지

    // ── 공용 설정 ────────────────────────────
    [Header("공용")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);  // Sweep 판정 크기
    [SerializeField] private LayerMask enemyLayer;
    private LayerMask bulletLayer;
    private LayerMask combined;
    private float cameraDumpingScale = 0.2f;
    private float originalCameraDumping = 1f;

    // ── 대쉬 내부 상태 ───────────────────────
    private Vector2 dashDirection;
    private Vector2 dashTargetPosition;
    private float dashRemainingDistance;
    private bool dashHitEnemy;
    private readonly HashSet<Collider2D> hitEnemiesInDash = new HashSet<Collider2D>();

    // ── 파랑 칩 내부 상태 ────────────────────
    private bool _blueFalling = false; // 낙하 강타 진행 중
    private bool _blueDashSecondInput = false; // 대시 도중 재입력 받았는지

    // ── 초록 칩 내부 상태 ────────────────────
    private bool _greenDashing = false; // 초록 칩 연속 돌진 중

    // ── CTS ──────────────────────────────────
    private CancellationTokenSource _greenDashCts;
    private CancellationTokenSource _blueFallCts;

    // ── 태클 내부 상태 ───────────────────────
    private Vector2 tackleDirection;
    private Vector2 tackleGroundDirection;
    private float tackleGroundDistanceRemaining;
    private float tackleCounter;
    private float tackleCooldownCounter;
    private bool hasUsedAirTackle;

    // ── 참조 ─────────────────────────────────
    private PlayerState state;
    private PlayerMove playerMove;
    private Rigidbody2D rb;
    private LineRenderer line;
    private PlayerAnimation ani;
    private Camera mainCamera;
    private CameraFollow cameraFollow;

    // ────────────────────────────────────────
    #region Unity 생명주기

    void Awake()
    {
        mainCamera = Camera.main;
        cameraFollow = mainCamera.transform.parent.GetComponent<CameraFollow>();
        state = GetComponent<PlayerState>();
        playerMove = GetComponent<PlayerMove>();
        line = GetComponent<LineRenderer>();
        ani = GetComponent<PlayerAnimation>();

        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("enemy");
        if (bulletLayer == 0)
            bulletLayer = LayerMask.GetMask("enemyBullet");
        combined = enemyLayer | bulletLayer;
    }

    void Start()
    {
        // Awake 이후 PlayerMove.Rb 접근이 안전
        rb = playerMove.Rb;
    }

    void Update()
    {
        // 착지 시 에어태클 플래그 초기화
        if (state.isGrounded && !state.isTackling && !state.isTackleGroundRush)
            hasUsedAirTackle = false;

        HandleDash();
        HandleTackle();
    }

     void FixedUpdate()
    {
        if (_blueFalling)
        {
            FixedUpdateBlueFall();
            return;
        }
        if (state.isDashing)
        {
            FixedUpdateDash();
            return;
        }
        if (state.isTackling)
        {
            FixedUpdateTackle();
            return;
        }
        if (state.isTackleGroundRush)
        {
            FixedUpdateGroundRush();
            return;
        }
    }

    void OnDestroy()
    {
        _greenDashCts?.Cancel();
        _greenDashCts?.Dispose();
        _blueFallCts?.Cancel();
        _blueFallCts?.Dispose();
    }


    #endregion
    // ────────────────────────────────────────
    #region 대쉬 – Update 입력

    void HandleDash()
    {
        if (state.isTackling || state.isTackleGroundRush) return;

        if (Input.GetMouseButtonDown(1))
        {
            // ─── 파랑 칩: 대시 도중 재입력 ───
            if (state.currentChip == PlayerChip.blue && state.isDashing)
            {
                _blueDashSecondInput = true;
                StopDash();
                StartBlueFall();
                return;
            }

            // ─── 초록 칩: 연속 돌진 중 무시 ───
            if (state.currentChip == PlayerChip.green && _greenDashing)
                return;

            playerMove.ResetWallState();
            ani.Attack_flash();

            // 대시 중 입력 → 버퍼
            if (state.isDashing)
            {
                state.dashBuffered = true;
                return;
            }

            // ─── 칩별 분기 ───────────────────
            switch (state.currentChip)
            {
                case PlayerChip.red:
                    // 빨강: 대시 무시, 아무것도 안 함
                    return;

                case PlayerChip.green:
                    // 초록: 3연 돌진 비동기 시작
                    StartGreenDash();
                    return;

                case PlayerChip.blue:
                    // 파랑: 대각선 방향으로만 대시
                    ExecuteBlueDash();
                    return;

                default:
                    // 핑크(기본): 기존 대시
                    ExecuteDash(GetMouseDirection());
                    return;
            }
        }

        // 대쉬 진행 중 종료 조건 체크
        if (state.isDashing)
        {
            dashRemainingDistance -= dashSpeed * Time.deltaTime;

            Collider2D wallHit = Physics2D.OverlapBox(
                transform.position, dashBoxSize, 0f, playerMove.GroundLayer);
            if (wallHit != null) { StopDash(); return; }
            if (dashRemainingDistance <= 0f) StopDash();
        }
    }

    #endregion
    // ────────────────────────────────────────
    #region 핑크(기본) 대쉬

    void ExecuteDash(Vector2 dir)
    {
        dashDirection = dir;

        Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point
            : (Vector2)transform.position + dashDirection * dashRange;

        dashRemainingDistance = Vector2.Distance(transform.position, dashTargetPosition);

        SetLineRenderer(transform.position, dashTargetPosition, true);

        if (state.TryUseDashGauge(state.DashCost))
            TriggerDash();
        else
            TriggerTackleFromDash(dashDirection);
    }

    void TriggerDash()
    {
        state.isGrounded = false;
        state.isOnCeiling = false;
        state.isTouchingLWall = false;
        state.isTouchingRWall = false;

        rb.gravityScale = 0f;
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        dashHitEnemy = false;
        hitEnemiesInDash.Clear();

        dashDirection = GetMouseDirection();
        state.isDashing = true;
        rb.linearVelocity = dashDirection * dashSpeed;
    }

    void StopDash()
    {
        state.isDashing = false;
        playerMove.JustStoppedDashing = true;
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);

        if (line != null) line.enabled = false;
        if (!state.isOnCeiling)
            rb.linearVelocity = Vector2.up * 0.05f;

        if (!_blueDashSecondInput && state.dashBuffered)
        {
            state.dashBuffered = false;
            ExecuteDash(GetMouseDirection());
        }
    }

    void FixedUpdateDash()
    {
        Vector2 currentPos = rb.position;
        Vector2 nextPos = Vector2.MoveTowards(
            currentPos, dashTargetPosition, dashSpeed * Time.fixedDeltaTime);
        SweepDashEnemies(currentPos, nextPos);
        rb.MovePosition(nextPos);
    }
    void SweepDashEnemies(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0f) return;

        dir /= distance;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            from, attackBoxSize + Vector2.one * 0.1f, 0f, dir, distance, combined);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && !hitEnemiesInDash.Contains(hit.collider))
                OnDashHitEnemy(hit.collider);
        }
    }

    void OnDashHitEnemy(Collider2D hit)
    {
        if (hit == null) return;
        hitEnemiesInDash.Add(hit);
        float damage = state.damage * dashDamage;
        ApplyDamage(hit, damage);
        Debug.Log($"대쉬 공격 적중: {hit.name}");

        if (!dashHitEnemy)
        {
            dashHitEnemy = true;
            state.AddDashGauge(state.dashGaugePerAttack);
            HitstopManager.Instance.TriggerHitstop();
        }
    }
    #endregion
    // ────────────────────────────────────────
    #region 초록 칩 – 3연 돌진
    void StartGreenDash()
    {
        if (!state.TryUseDashGauge(state.DashCost)) return;

        _greenDashCts?.Cancel();
        _greenDashCts?.Dispose();
        _greenDashCts = new CancellationTokenSource();
        GreenDashRoutine(_greenDashCts.Token).Forget();
    }

    private async UniTaskVoid GreenDashRoutine(CancellationToken ct)
    {
        _greenDashing = true;
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        for (int i = 0; i < greenDashCount; i++)
        {
            if (ct.IsCancellationRequested) break;

            // 매 돌진마다 현재 마우스 방향 계산
            Vector2 dir = GetMouseDirection();
            Vector2 start = rb.position;
            Vector2 target = start + dir * greenDashRange;

            // 지형 충돌 보정
            RaycastHit2D groundHit = Physics2D.Raycast(
                start + dir * 0.05f, dir, greenDashRange, playerMove.GroundLayer);
            if (groundHit.collider != null)
                target = groundHit.point;

            SetLineRenderer(start, target, true);

            rb.gravityScale = 0f;
            state.isDashing = true;
            rb.linearVelocity = dir * greenDashSpeed;

            HashSet<Collider2D> hitThisDash = new HashSet<Collider2D>();
            float traveled = 0f;
            float dist = Vector2.Distance(start, target);

            while (!ct.IsCancellationRequested && traveled < dist)
            {
                Vector2 cur = rb.position;
                Vector2 next = Vector2.MoveTowards(cur, target, greenDashSpeed * Time.fixedDeltaTime);

                // 스윕 판정
                Vector2 sweepDir = next - cur;
                float sweepDist = sweepDir.magnitude;
                if (sweepDist > 0f)
                {
                    var hits = Physics2D.BoxCastAll(
                        cur, attackBoxSize + Vector2.one * 0.1f, 0f,
                        sweepDir.normalized, sweepDist, enemyLayer);
                    foreach (var h in hits)
                    {
                        if (h.collider != null && !hitThisDash.Contains(h.collider))
                        {
                            hitThisDash.Add(h.collider);
                            ApplyDamage(h.collider, state.damage * dashDamage);
                            HitstopManager.Instance.TriggerHitstop(0.04f);
                        }
                    }
                }

                rb.MovePosition(next);
                traveled += greenDashSpeed * Time.fixedDeltaTime;

                // 벽 충돌 시 돌진 중단
                Collider2D wallHit = Physics2D.OverlapBox(
                    rb.position, dashBoxSize, 0f, playerMove.GroundLayer);
                if (wallHit != null) break;

                await UniTask.WaitForFixedUpdate(cancellationToken: ct);
            }

            state.isDashing = false;

            // 마지막 돌진이 아니면 잠깐 대기 (방향 변경 기회)
            if (i < greenDashCount - 1 && !ct.IsCancellationRequested)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f; // 대기 중 공중에 고정
                await UniTask.WaitForSeconds(greenDashInterval, cancellationToken: ct)
                    .SuppressCancellationThrow();
            }
        }

        // 3연 돌진 완료
        state.isDashing = false;
        playerMove.JustStoppedDashing = true;
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.up * 0.05f;
        if (line != null) line.enabled = false;

        _greenDashing = false;
    }

    #endregion
    // ────────────────────────────────────────
    #region 파랑 칩 – 대각 대시 + 낙하 공격
    void ExecuteBlueDash()
    {
        // 마우스 방향을 대각선 4방향 중 하나로 스냅
        Vector2 rawDir = GetMouseDirection();
        Vector2 snappedDir = SnapToDiagonal(rawDir);

        dashDirection = snappedDir;

        Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point
            : (Vector2)transform.position + dashDirection * dashRange;

        dashRemainingDistance = Vector2.Distance(transform.position, dashTargetPosition);

        SetLineRenderer(transform.position, dashTargetPosition, true);

        _blueDashSecondInput = false;

        if (state.TryUseDashGauge(state.DashCost))
        {
            state.isGrounded = false;
            state.isOnCeiling = false;
            state.isTouchingLWall = false;
            state.isTouchingRWall = false;

            rb.gravityScale = 0f;
            cameraFollow.SetCameraDumping(cameraDumpingScale);
            dashHitEnemy = false;
            hitEnemiesInDash.Clear();

            state.isDashing = true;
            rb.linearVelocity = dashDirection * dashSpeed;
        }
        else
        {
            TriggerTackleFromDash(dashDirection);
        }
    }

    /// <summary>입력 벡터를 가장 가까운 45° 대각선(4방향)으로 스냅</summary>
    private Vector2 SnapToDiagonal(Vector2 dir)
    {
        // 대각선 4방향: (±√0.5, ±√0.5)
        float x = dir.x >= 0f ? 1f : -1f;
        float y = dir.y >= 0f ? 1f : -1f;
        return new Vector2(x, y).normalized;
    }

    void StartBlueFall()
    {
        _blueFallCts?.Cancel();
        _blueFallCts?.Dispose();
        _blueFallCts = new CancellationTokenSource();

        _blueFalling = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.down * blueFallSpeed;
        cameraFollow.SetCameraDumping(cameraDumpingScale);
    }

    void FixedUpdateBlueFall()
    {
        rb.linearVelocity = Vector2.down * blueFallSpeed;

        // 착지 감지
        bool landed = Physics2D.OverlapBox(
            playerMove.GroundCheck.position, playerMove.GroundCheckSize, 0f, playerMove.GroundLayer);
        if (landed)
            BlueSlamLand();
    }

    void BlueSlamLand()
    {
        _blueFalling = false;

        // 범위 내 적 전체 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, blueSlamRadius, enemyLayer);
        foreach (var hit in hits)
            ApplyDamage(hit, state.damage * blueSlamDamage);

        // 상태 복구
        playerMove.JustStoppedDashing = true;
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.zero;
        _blueDashSecondInput = false;
        if (line != null) line.enabled = false;

        HitstopManager.Instance.TriggerHitstop(0.12f, 0.05f);
    }

    #endregion
    // ────────────────────────────────────────
    #region 태클 – Update 입력

    void HandleTackle()
    {
        tackleCooldownCounter = Mathf.Max(0f, tackleCooldownCounter - Time.deltaTime);

        bool inputPressed = Input.GetKeyDown(KeyCode.E);
        if (!inputPressed || (state.isDashing || state.DashGauge <= state.DashCost) || state.isTackling
            || state.isTackleGroundRush || tackleCooldownCounter > 0f)
            return;

        if (!state.isGrounded && hasUsedAirTackle)
            return;

        StartTackle(GetMouseDirection());
    }

    private void TriggerTackleFromDash(Vector2 aimDirection)
    {
        if (state.isTackling || state.isTackleGroundRush || tackleCooldownCounter > 0f)
            return;
        if (!state.isGrounded && hasUsedAirTackle)
            return;

        StartTackle(aimDirection);
    }

    private void StartTackle(Vector2 aimDirection)
    {
        rb.gravityScale = 0f;
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        if (state.isGrounded)
        {
            tackleDirection = new Vector2(Mathf.Sign(aimDirection.x), 0f);
            if (tackleDirection == Vector2.zero) tackleDirection = Vector2.right;
            state.isTackleGroundRush = true;
            state.isTackling = false;
        }
        else
        {
            tackleDirection = aimDirection;
            state.isTackling = true;
            state.isTackleGroundRush = false;
            hasUsedAirTackle = true;
        }

        tackleGroundDirection = new Vector2(tackleDirection.x, 0f).normalized;
        if (tackleGroundDirection == Vector2.zero)
            tackleGroundDirection = transform.right;

        tackleCounter = tackleRange / tackleSpeed;
        tackleGroundDistanceRemaining = tackleRange;
        tackleCooldownCounter = tackleCooldown;
        rb.gravityScale = 0f;

        if (line != null)
        {
            line.SetPosition(0, transform.position);
            line.SetPosition(1, transform.position + (Vector3)tackleDirection * tackleRange);
        }

        Debug.DrawRay(transform.position, tackleDirection * tackleRange, Color.yellow, 1f);
    }

    void StopTackle()
    {
        state.isTackling = false;
        state.isTackleGroundRush = false;
        tackleGroundDistanceRemaining = 0f;

        if (state.isGrounded)
            hasUsedAirTackle = false;

        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);

        line.enabled = false;
    }

    private void StartTackleGroundRush()
    {
        state.isTackling = false;
        state.isTackleGroundRush = true;
        tackleGroundDirection = new Vector2(tackleDirection.x, 0f).normalized;
        if (tackleGroundDirection == Vector2.zero)
            tackleGroundDirection = transform.right;

        // 남은 시간 기준으로 거리 산출 (초기화 안 함)
        tackleGroundDistanceRemaining = tackleCounter * tackleSpeed;
    }

    #endregion
    // ────────────────────────────────────────
    #region 태클 – FixedUpdate 물리

    void FixedUpdateTackle()
    {
        tackleCounter -= Time.fixedDeltaTime;

        Vector2 currentPos = rb.position;
        Vector2 nextPos = currentPos + tackleDirection * tackleSpeed * Time.fixedDeltaTime;

        if (SweepTacklePath(currentPos, nextPos))
        {
            StopTackle();
            return;
        }

        rb.MovePosition(nextPos);

        bool landed = Physics2D.OverlapBox(
            playerMove.GroundCheck.position, playerMove.GroundCheckSize, 0f, playerMove.GroundLayer);
        if (landed)
        {
            StartTackleGroundRush();
            return;
        }

        if (tackleCounter <= 0f)
            StopTackle();
    }

    void FixedUpdateGroundRush()
    {
        float moveDistance = tackleSpeed * Time.fixedDeltaTime;
        Vector2 currentPos = rb.position;
        Vector2 nextPos = currentPos + tackleGroundDirection * moveDistance;

        if (SweepTacklePath(currentPos, nextPos))
        {
            StopTackle();
            return;
        }

        rb.MovePosition(nextPos);
        tackleGroundDistanceRemaining -= moveDistance;

        bool stillGrounded = Physics2D.OverlapBox(
            playerMove.GroundCheck.position, playerMove.GroundCheckSize, 0f, playerMove.GroundLayer);
        if (tackleGroundDistanceRemaining <= 0f || !stillGrounded)
            StopTackle();
    }

    /// <summary>태클 경로에서 첫 번째 적 감지 시 데미지 후 정지. 충돌 시 true 반환.</summary>
    private bool SweepTacklePath(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0f) return false;

        dir /= distance;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            from, attackBoxSize + Vector2.one * 0.1f, 0f, dir, distance, enemyLayer);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            OnTackleHitEnemy(hit.collider);
            StopTackle();
            return true;
        }
        return false;
    }

    void OnTackleHitEnemy(Collider2D enemy)
    {
        if(enemy == null) return;
        Debug.Log($"몸통박치기 적중: {enemy.name}");
        //ApplyDamage(enemy,0);

        // 적에게 스턴 적용
        enemy.GetComponent<EnemyBTController>()?.ApplyStun();

        // IMoveable이 없으면 넉백 처리 안 함
        var moveable = enemy.GetComponent<EnemyVersion3.Interfaces.IMoveable>();
        if (moveable == null) return;

        var enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
            enemyRb.linearVelocity = tackleDirection * tackleKnockbackForce;

        StopTackle();
        rb.linearVelocity = -tackleDirection * tackleKnockbackForce * tackleSelfKnockbackRatio;
    }

    #endregion
    // ────────────────────────────────────────
    #region 공통 유틸

    /// <summary>현재 마우스 커서 방향의 normalized 벡터를 반환한다.</summary>
    private Vector2 GetMouseDirection()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2 dir = (mouseWorldPos - (Vector2)transform.position).normalized;
        return dir == Vector2.zero ? Vector2.right : dir;
    }

    /// <summary>대상 컴포넌트에 맞게 데미지를 전달한다.</summary>
    private void ApplyDamage(Collider2D target, float damage)
    {
        if (target == null) return;
        try
        {
            target.GetComponent<IDamageable>()?.TakeDamage(damage);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"데미지 적용 중 오류: {ex.Message}");
        }
    }

    private void SetLineRenderer(Vector3 from, Vector3 to, bool enabled)
    {
        if (line == null) return;
        line.enabled = enabled;
        if (enabled) { line.SetPosition(0, from); line.SetPosition(1, to); }
    }

    #endregion
    // ────────────────────────────────────────
    #region Gizmos

    void OnDrawGizmosSelected()
    {
        // 대쉬 박스 (벽 충돌 판정용)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, dashBoxSize);

        if (_blueFalling)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, blueSlamRadius);
        }
    }

    #endregion
}
