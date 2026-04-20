using System.Collections.Generic;
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

    // ── 공용 설정 ────────────────────────────
    [Header("공용")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);  // Sweep 판정 크기
    [SerializeField] private LayerMask enemyLayer;

    // ── 대쉬 내부 상태 ───────────────────────
    private Vector2 dashDirection;
    private Vector2 dashTargetPosition;
    private float dashRemainingDistance;
    private bool dashHitEnemy;
    private readonly HashSet<Collider2D> hitEnemiesInDash = new HashSet<Collider2D>();

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

    // ────────────────────────────────────────
    #region Unity 생명주기

    void Awake()
    {
        mainCamera = Camera.main;
        state = GetComponent<PlayerState>();
        playerMove = GetComponent<PlayerMove>();
        line = GetComponent<LineRenderer>();
        ani = GetComponent<PlayerAnimation>();

        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("enemy");
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

    #endregion
    // ────────────────────────────────────────
    #region 대쉬 – Update 입력

    void HandleDash()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // 벽타기 상태 리셋
            playerMove.ResetWallState();

            ani.Attack_flash_on();

            // 대시 중 입력 → 버퍼에 저장
            if (state.isDashing)
            {
                state.dashBuffered = true;
                return; // 즉시 실행하지 않음
            }
            ExecuteDash(GetMouseDirection());
        }

        // 대쉬 진행 중 종료 조건 체크 (Update)
        if (state.isDashing)
        {
            dashRemainingDistance -= dashSpeed * Time.deltaTime;

            Collider2D wallHit = Physics2D.OverlapBox(
                transform.position, dashBoxSize, 0f, playerMove.GroundLayer);
            if (wallHit != null)
            {
                StopDash();
                return;
            }
            if (dashRemainingDistance <= 0f)
                StopDash();
        }
    }

    void TriggerDash()
    {
        // 벽/지형에 붙어있을 때 살짝 띄워서 시작
        //if (state.isGrounded) rb.position += new Vector2(0f, 0.05f);
        //if (state.isOnCeiling) rb.position += new Vector2(0f, -0.05f);
        //if (state.isTouchingLWall) rb.position += new Vector2(-0.05f, 0f);
        //if (state.isTouchingRWall) rb.position += new Vector2(0.05f, 0f);
        state.isGrounded = false;
        state.isOnCeiling = false;
        state.isTouchingLWall = false;
        state.isTouchingRWall = false;

        state.TryUseDashGauge(state.DashCost);

        dashHitEnemy = false;
        hitEnemiesInDash.Clear();

        dashDirection = GetMouseDirection();

        state.isDashing = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashDirection * dashSpeed;

        Debug.DrawRay(transform.position, dashDirection * 5f, Color.red, 1f);
    }

    void StopDash()
    {
        state.isDashing = false;
        ani.Attack_flash_off();
        playerMove.JustStoppedDashing = true; // ← 추가
        playerMove.RestoreGravity();

        if (line != null)
            line.enabled = false;

        if (!state.isOnCeiling)
            rb.linearVelocity = Vector2.up * 0.05f;

        if (state.dashBuffered)
        {
            state.dashBuffered = false;
            ExecuteDash(GetMouseDirection());
        }
    }

    void ExecuteDash(Vector2 dir)
    {
        // 마우스 방향 계산
        dashDirection = dir;

        // 목표 지점 계산 (지형 충돌 고려)
        Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point
            : (Vector2)transform.position + dashDirection * dashRange;

        dashRemainingDistance = Vector2.Distance(transform.position, dashTargetPosition);

        if (line != null)
        {
            line.enabled = true;
            line.SetPosition(0, transform.position);
            line.SetPosition(1, dashTargetPosition);
        }

        rb.gravityScale = 0f;

        // 게이지 충분 → 강화 대쉬 / 부족 → 태클
        if (state.DashGauge >= 1f)
            TriggerDash();
        else
            TriggerTackleFromDash(dashDirection);

        Debug.DrawRay(transform.position, dashDirection * dashRange, Color.red, 1f);
    }

    #endregion
    // ────────────────────────────────────────
    #region 대쉬 – FixedUpdate 물리

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
            from, attackBoxSize + Vector2.one * 0.1f, 0f, dir, distance, enemyLayer);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && !hitEnemiesInDash.Contains(hit.collider))
                OnDashHitEnemy(hit.collider);
        }
    }

    void OnDashHitEnemy(Collider2D hit)
    {
        hitEnemiesInDash.Add(hit);
        float damage = state.damage * dashDamage;
        ApplyDamage(hit, damage);
        Debug.Log($"대쉬 공격 적중: {hit.name}");

        if (!dashHitEnemy)
        {
            dashHitEnemy = true;
            state.AddDashGauge(1f);
            HitstopManager.Instance.TriggerHitstop(duration: 0.02f);
        }
    }

    #endregion
    // ────────────────────────────────────────
    #region 태클 – Update 입력

    void HandleTackle()
    {
        tackleCooldownCounter = Mathf.Max(0f, tackleCooldownCounter - Time.deltaTime);

        bool inputPressed = Input.GetKeyDown(KeyCode.E);
        if (!inputPressed || state.isDashing || state.isTackling
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

        if (SweepTacklePath(currentPos, nextPos)) return;

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

        if (SweepTacklePath(currentPos, nextPos)) return;

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
        Debug.Log($"몸통박치기 적중: {enemy.name}");
        ApplyDamage(enemy, state.damage * tackleDamage);
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
        target.GetComponent<BossBase>()?.TakeDamage(damage);
        target.GetComponent<ObjectHp>()?.OnDamage(damage);
        target.GetComponent<EnemyBase>()?.OnDamage(damage);
        target.GetComponent<NewEnemyBase>()?.OnDamage(damage);
    }

    #endregion
    // ────────────────────────────────────────
    #region Gizmos

    void OnDrawGizmosSelected()
    {
        // 대쉬 박스 (벽 충돌 판정용)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, dashBoxSize);
    }

    #endregion
}
