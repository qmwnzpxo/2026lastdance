//using Cysharp.Threading.Tasks;
//using System.Collections.Generic;
//using System.Threading;
//using UnityEngine;

//public class PlayerMove : MonoBehaviour
//{
//    [Header("이동")]
//    [SerializeField] private float moveSpeed = 7f;

//    [Header("점프")]
//    [SerializeField] private float jumpForce = 12f;
//    [SerializeField] private LayerMask groundLayer;
//    [SerializeField] private Transform groundCheck;
//    [SerializeField] private Transform ceilingCheck;
//    [SerializeField] private float groundCheckRadius = 0.2f;
//    [SerializeField] private float ceilingCheckRadius = 0.2f;

//    [Header("벽타기")]
//    [SerializeField] private Transform wallCheck;
//    [EditorOnly, SerializeField] private float wallCheckDistance = 0.3f;
//    [EditorOnly, SerializeField] private float wallSlideSpeed = 1.5f;
//    [EditorOnly, SerializeField] private float wallSlideSpeedUp = 0f;
//    [EditorOnly, SerializeField] private float wallSlideSpeedDown = 5f;
//    [EditorOnly, SerializeField] private float wallJumpForceX = 8f;
//    [EditorOnly, SerializeField] private float wallJumpForceY = 12f;

//    [Header("천장 붙기")]
//    [EditorOnly, SerializeField] private float defaultGravityScale = 1.2f;
//    [EditorOnly, SerializeField] private float ceilingGravityScale = -1.2f;
//    [EditorOnly, SerializeField] private Transform spriteTransform;
//    private Vector3 spriteScale = new Vector3(1, 1, 1);
//    private Vector3 ceilingSpriteScale = new Vector3(1, -1, 1);

//    [Header("대쉬")]
//    [SerializeField] private float dashGauge = 0f;
//    [SerializeField] private float maxDashGauge = 5f;
//    [SerializeField] private float dashRange = 8f;
//    [SerializeField] private float dashDamage = 5f;
//    [SerializeField] private float dashSpeed = 50f;
//    [RuntimeOnly, SerializeField] private List<Collider2D> hitEnemiesInDash = new List<Collider2D>();
//    private Vector2 dashBoxSize = new Vector2(1f, 0.4f);
//    private Vector2 dashTargetPosition;
//    private float dashRemainingDistance;
//    private Vector2 dashDirection;
//    private float dashGaugePerAttack = 0.3f;
//    private bool dashHitEnemy = false;
//    private LineRenderer line;

//    [Header("몸통박치기")]
//    [SerializeField] private float tackleRange = 8f;
//    [SerializeField] private float tackleSpeed = 50f;
//    [SerializeField] private float tackleDamage = 0f;   // 0으로 두면 무데미지
//    [SerializeField] private float tackleCooldown = 1f;
//    private float tackleCooldownCounter = 0f;
//    private bool isTackling;
//    private bool isTackleGroundRush;
//    private bool hasUsedAirTackle;
//    private Vector2 tackleDirection;
//    private Vector2 tackleGroundDirection;
//    private float tackleGroundDistanceRemaining;
//    private float tackleCounter;

//    [Header("공격")]
//    [SerializeField] private Transform attackTransform;
//    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);
//    [SerializeField] private float attackBoxDistance = 1f;
//    [EditorOnly, SerializeField] private float attackSecondHitDelay = 0.1f;
//    [EditorOnly, SerializeField] private LayerMask enemyLayer;

//    private Rigidbody2D rb;
//    private float moveInput;
//    private float verticalInput;

//    [Header("불값 모음")]
//    [SerializeField] private bool isGrounded;
//    [SerializeField] private bool isTouchingCeiling;
//    [SerializeField] private bool isOnCeiling;
//    private bool wasTouchingCeiling;
//    [SerializeField] private bool isTouchingRWall;
//    [SerializeField] private bool isTouchingLWall;
//    [SerializeField] private bool isWallSliding;
//    [SerializeField] private bool isWallJumping;
//    [SerializeField] private bool isDashing;
//    [SerializeField] private bool isAttack;

//    private float wallJumpTime = 0.2f;
//    private float wallJumpCounter;
//    private int wallJumpDirection;
//    private CancellationTokenSource wallJumpCts;
//    private PlayerState playerState;

//    void Awake()
//    {
//        rb = GetComponent<Rigidbody2D>();
//        if (playerState == null)
//            playerState = GetComponent<PlayerState>();
//        if (enemyLayer == 0)
//            enemyLayer = LayerMask.GetMask("enemy");
//        line = GetComponent<LineRenderer>();
//    }

//    void Update()
//    {
//        moveInput = Input.GetAxis("Horizontal");
//        verticalInput = Input.GetAxisRaw("Vertical");

//        CheckGround();
//        CheckCeiling();
//        CheckWall();

//        if (isGrounded && !isTackling && !isTackleGroundRush)
//            hasUsedAirTackle = false;

//        RotateAttackPivot();
//        HandleAttack();
//        HandleCeilingAttach();
//        HandleWallSlide();
//        HandleDash();
//        HandleTackle();
//        HandleJump();

//        callingHitstop();
//    }

//    void FixedUpdate()
//    {
//        if (isDashing)
//        {
//            Vector2 currentPosition = rb.position;
//            Vector2 nextPosition = Vector2.MoveTowards(currentPosition, dashTargetPosition, dashSpeed * Time.fixedDeltaTime);
//            SweepDashEnemies(currentPosition, nextPosition);
//            rb.MovePosition(nextPosition);
//            return;
//        }

//        if (isTackling)
//        {
//            tackleCounter -= Time.fixedDeltaTime;
//            Vector2 currentPosition = rb.position;
//            Vector2 nextPosition = currentPosition + tackleDirection * tackleSpeed * Time.fixedDeltaTime;

//            if (SweepTacklePath(currentPosition, nextPosition))
//                return;

//            rb.MovePosition(nextPosition);

//            bool landed = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
//            if (landed)
//            {
//                StartTackleGroundRush();
//                return;
//            }

//            if (tackleCounter <= 0f)
//            {
//                StopTackle();
//                return;
//            }
//            return;
//        }

//        if (isTackleGroundRush)
//        {
//            float moveDistance = tackleSpeed * Time.fixedDeltaTime;
//            Vector2 currentPosition = rb.position;
//            Vector2 nextPosition = currentPosition + tackleGroundDirection * moveDistance;

//            if (SweepTacklePath(currentPosition, nextPosition))
//                return;

//            rb.MovePosition(nextPosition);
//            tackleGroundDistanceRemaining -= moveDistance;

//            bool stillGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
//            if (tackleGroundDistanceRemaining <= 0f || !stillGrounded)
//            {
//                StopTackle();
//                return;
//            }

//            return;
//        }

//        if (!isWallJumping)
//        {
//            float xMove = ((isTouchingLWall || isTouchingRWall)
//                && !isGrounded && !isOnCeiling) ? 0f : moveInput * moveSpeed;
//            rb.linearVelocity = new Vector2(xMove, rb.linearVelocity.y);
//        }

//        if (isWallSliding)
//        {
//            float wallDir = moveInput != 0 ? moveInput : transform.localScale.x;
//            if (moveInput != 0 || moveInput != wallDir)
//                isWallSliding = false;

//            float targetSlideSpeed;
//            if (verticalInput > 0)
//                targetSlideSpeed = -wallSlideSpeedUp;
//            else if (verticalInput < 0)
//                targetSlideSpeed = -wallSlideSpeedDown;
//            else
//                targetSlideSpeed = -wallSlideSpeed;

//            float smoothedY = Mathf.Lerp(rb.linearVelocity.y, targetSlideSpeed, Time.fixedDeltaTime * 10f);
//            rb.linearVelocity = new Vector2(rb.linearVelocity.x, smoothedY);
//        }
//    }

//    void callingHitstop()
//    {
//        if (Input.GetKeyDown(KeyCode.H))
//            HitstopManager.Instance.TriggerHitstop();
//    }

//    void CheckGround()
//    {
//        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
//    }
//    void CheckCeiling()
//    {
//        isTouchingCeiling = Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
//    }
//    void CheckWall()
//    {
//        isTouchingLWall = Physics2D.Raycast(wallCheck.position, Vector2.left, wallCheckDistance, groundLayer);
//        isTouchingRWall = Physics2D.Raycast(wallCheck.position, Vector2.right, wallCheckDistance, groundLayer);
//    }

//    void HandleWallSlide()
//    {
//        isWallSliding = (isTouchingLWall || isTouchingRWall)
//            && !isGrounded && !isOnCeiling && !isDashing
//            && (rb.linearVelocity.y < 0 || Mathf.Abs(verticalInput) > 0);
//    }

//    void HandleJump()
//    {
//        if (!Input.GetKeyDown(KeyCode.Space) || isDashing) return;

//        if (isOnCeiling)
//        {
//            DetachFromCeiling();
//            return;
//        }

//        if (isGrounded)
//        {
//            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
//        }
//        else if (isWallSliding && wallJumpCounter <= 0)
//        {
//            wallJumpDirection = isTouchingLWall ? 1 : -1;
//            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpForceX, wallJumpForceY);
//            isWallJumping = true;

//            wallJumpCts?.Cancel();
//            wallJumpCts = new CancellationTokenSource();
//            StartWallJumpCooldown(wallJumpCts.Token).Forget();
//        }
//    }

//    private async UniTaskVoid StartWallJumpCooldown(CancellationToken ct)
//    {
//        wallJumpCounter = wallJumpTime;
//        await UniTask.WaitForSeconds(wallJumpTime, cancellationToken: ct);
//        wallJumpCounter = 0f;
//        isWallJumping = false;
//    }

//    void DetachFromCeiling()
//    {
//        isOnCeiling = false;
//        rb.gravityScale = defaultGravityScale;
//        spriteTransform.localScale = spriteScale;
//    }

//    void HandleCeilingAttach()
//    {
//        bool justTouchedCeiling = isTouchingCeiling && !wasTouchingCeiling;

//        if (justTouchedCeiling && !isOnCeiling)
//        {
//            if (isDashing)
//            {
//                isOnCeiling = true;
//                rb.gravityScale = ceilingGravityScale;
//                spriteTransform.localScale = ceilingSpriteScale;
//            }
//        }

//        if (isOnCeiling && !isTouchingCeiling)
//            DetachFromCeiling();

//        if (isOnCeiling && !isDashing && Input.GetKeyDown(KeyCode.Space))
//        {
//            DetachFromCeiling();
//            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce);
//        }

//        wasTouchingCeiling = isTouchingCeiling;
//    }

//    void HandleDash()
//    {
//        if (Input.GetMouseButtonDown(1))
//        {

//            isWallJumping = false;
//            isWallSliding = false;
//            wallJumpCounter = 0f;

//            Vector3 mouseScreenPos = Input.mousePosition;
//            mouseScreenPos.z = -Camera.main.transform.position.z;
//            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
//            dashDirection = (mouseWorldPos - (Vector2)transform.position).normalized;
//            if (dashDirection == Vector2.zero)
//                dashDirection = Vector2.right;

//            Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
//            RaycastHit2D groundHit = Physics2D.Raycast(origin, dashDirection, dashRange, groundLayer);
//            dashTargetPosition = groundHit.collider != null ? groundHit.point :
//                (Vector2)transform.position + dashDirection * dashRange;
//            dashRemainingDistance = Vector2.Distance(transform.position, dashTargetPosition);

//            line.SetPosition(0, transform.position);
//            line.SetPosition(1, dashTargetPosition);
//            rb.gravityScale = 0f;

//            if (dashGauge >= 1f)
//            {
//                TriggerDash();  // 강화 대쉬
//            }
//            else
//            {
//                TriggerTackleFromDash(dashDirection);   // 게이지 없으면 태클
//            }

//            Debug.DrawRay(transform.position, dashDirection * dashRange, Color.red, 1f);
//        }

//        if (isDashing)
//        {
//            dashRemainingDistance -= dashSpeed * Time.deltaTime;

//            Collider2D wallHit = Physics2D.OverlapBox(transform.position, dashBoxSize, 0f, groundLayer);
//            if (wallHit != null)
//            {
//                StopDash();
//                return;
//            }

//            if (dashRemainingDistance <= 0f)
//            {
//                StopDash();
//                return;
//            }
//        }
//    }

//    void HandleTackle()
//    {
//        tackleCooldownCounter = Mathf.Max(0f, tackleCooldownCounter - Time.deltaTime);

//        if (!Input.GetKeyDown(KeyCode.E) || isDashing || isTackling || isTackleGroundRush || tackleCooldownCounter > 0f)
//            return;

//        if (!isGrounded && hasUsedAirTackle)
//            return;

//        Vector3 mouseScreenPos = Input.mousePosition;
//        mouseScreenPos.z = -Camera.main.transform.position.z;
//        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
//        Vector2 aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;
//        if (aimDirection == Vector2.zero)
//            aimDirection = Vector2.right;

//        StartTackle(aimDirection);
//    }

//    private void TriggerTackleFromDash(Vector2 aimDirection)
//    {
//        if (isTackling || isTackleGroundRush || tackleCooldownCounter > 0f)
//            return;
//        if (!isGrounded && hasUsedAirTackle)
//            return;
//        if (aimDirection == Vector2.zero)
//            aimDirection = Vector2.right;

//        StartTackle(aimDirection);
//    }

//    /// <summary>실제 태클을 시작하는 공통 로직</summary>
//    private void StartTackle(Vector2 aimDirection)
//    {
//        if (isGrounded)
//        {
//            tackleDirection = new Vector2(Mathf.Sign(aimDirection.x), 0f);
//            if (tackleDirection == Vector2.zero)
//                tackleDirection = Vector2.right;
//            isTackleGroundRush = true;
//            isTackling = false;
//        }
//        else
//        {
//            tackleDirection = aimDirection;
//            isTackling = true;
//            isTackleGroundRush = false;
//            hasUsedAirTackle = true;
//        }

//        tackleGroundDirection = new Vector2(tackleDirection.x, 0f).normalized;
//        if (tackleGroundDirection == Vector2.zero)
//            tackleGroundDirection = transform.right;

//        tackleCounter = tackleRange / tackleSpeed;
//        tackleGroundDistanceRemaining = tackleRange;
//        tackleCooldownCounter = tackleCooldown;
//        rb.gravityScale = 0f;

//        if (line != null)
//        {
//            line.SetPosition(0, transform.position);
//            line.SetPosition(1, transform.position + (Vector3)tackleDirection * tackleRange);
//        }

//        Debug.DrawRay(transform.position, tackleDirection * tackleRange, Color.yellow, 1f);
//    }

//    /// <summary>태클 경로에서 첫 번째 적을 감지하면 데미지 후 정지. 충돌 시 true 반환.</summary>
//    private bool SweepTacklePath(Vector2 from, Vector2 to)
//    {
//        Vector2 direction = to - from;
//        float distance = direction.magnitude;
//        if (distance <= 0f) return false;

//        direction /= distance;
//        RaycastHit2D[] hits = Physics2D.BoxCastAll(
//            from, attackBoxSize + Vector2.one * 0.1f, 0f, direction, distance, enemyLayer);

//        foreach (RaycastHit2D hit in hits)
//        {
//            if (hit.collider == null) continue;

//            OnTackleHitEnemy(hit.collider);
//            StopTackle();
//            return true;
//        }

//        return false;
//    }

//    private void OnTackleHitEnemy(Collider2D enemy)
//    {
//        Debug.Log($"몸통박치기 적중: {enemy.name}");
//        float damage = playerState.damage * tackleDamage;
//        enemy.GetComponent<BossBase>()?.TakeDamage(damage);
//        enemy.GetComponent<ObjectHp>()?.OnDamage(damage);
//        enemy.GetComponent<EnemyBase>()?.OnDamage(damage);
//        enemy.GetComponent<NewEnemyBase>()?.OnDamage(damage);
//    }

//    private void StartTackleGroundRush()
//    {
//        isTackling = false;
//        isTackleGroundRush = true;
//        tackleGroundDirection = new Vector2(tackleDirection.x, 0f).normalized;
//        if (tackleGroundDirection == Vector2.zero)
//            tackleGroundDirection = transform.right;
//        // 남은 시간 기준으로 남은 거리 유지 (초기화 안 함)
//        tackleGroundDistanceRemaining = tackleCounter * tackleSpeed;
//    }

//    private void StopTackle()
//    {
//        isTackling = false;
//        isTackleGroundRush = false;
//        tackleGroundDistanceRemaining = 0f;
//        if (isGrounded)
//            hasUsedAirTackle = false;
//        rb.gravityScale = isOnCeiling ? ceilingGravityScale : defaultGravityScale;
//        if (line != null)
//            line.SetPosition(1, transform.position);
//    }

//    private void SweepDashEnemies(Vector2 from, Vector2 to)
//    {
//        Vector2 direction = to - from;
//        float distance = direction.magnitude;
//        if (distance <= 0f) return;

//        direction /= distance;
//        RaycastHit2D[] hits = Physics2D.BoxCastAll(
//            from, attackBoxSize + Vector2.one * 0.1f, 0f, direction, distance, enemyLayer);

//        foreach (RaycastHit2D hit in hits)
//        {
//            if (hit.collider != null && !hitEnemiesInDash.Contains(hit.collider))
//                EnemyDashHit(hit.collider);
//        }
//    }

//    private void EnemyDashHit(Collider2D hit)
//    {
//        hitEnemiesInDash.Add(hit);
//        float damage = playerState.damage * dashDamage;
//        hit.GetComponent<BossBase>()?.TakeDamage(damage);
//        hit.GetComponent<ObjectHp>()?.OnDamage(damage);
//        hit.GetComponent<EnemyBase>()?.OnDamage(damage);
//        hit.GetComponent<NewEnemyBase>()?.OnDamage(damage);
//        Debug.Log($"대쉬 공격 적중: {hit.name}");

//        if (!dashHitEnemy)
//        {
//            dashHitEnemy = true;
//            dashGauge = Mathf.Min(dashGauge + 1f, maxDashGauge);
//            HitstopManager.Instance.TriggerHitstop(duration: 0.02f);
//        }
//    }

//    void StopDash()
//    {
//        isDashing = false;
//        rb.gravityScale = isOnCeiling ? ceilingGravityScale : defaultGravityScale;
//        line.SetPosition(1, transform.position);

//        if (!isOnCeiling)
//            rb.linearVelocity = Vector2.up * 0.05f;
//    }

//    void TriggerDash( )
//    {
//        if (isGrounded) rb.position += new Vector2(0f, 0.05f);
//        if (isOnCeiling) rb.position += new Vector2(0f, -0.05f);
//        if (isTouchingLWall) rb.position += new Vector2(-0.05f, 0f);
//        if (isTouchingRWall) rb.position += new Vector2(0.05f, 0f);

//        isWallJumping = false;
//        isWallSliding = false;
//        wallJumpCounter = 0f;
        
//        dashGauge -= 1f;

//        dashHitEnemy = false;
//        hitEnemiesInDash.Clear();

//        Vector3 mouseScreenPos = Input.mousePosition;
//        mouseScreenPos.z = -Camera.main.transform.position.z;
//        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
//        dashDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

//        isDashing = true;

//        rb.gravityScale = 0f;
//        rb.linearVelocity = dashDirection * dashSpeed;

//        Debug.DrawRay(transform.position, dashDirection * 5f, Color.red, 1f);
//    }

//    void RotateAttackPivot()
//    {
//        Vector3 mouseScreenPos = Input.mousePosition;
//        mouseScreenPos.z = -Camera.main.transform.position.z;
//        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

//        Vector2 dir = (mouseWorldPos - (Vector2)transform.position).normalized;
//        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
//        attackTransform.rotation = Quaternion.Euler(0f, 0f, angle);
//    }

//    void HandleAttack()
//    {
//        if (!Input.GetMouseButtonDown(0) || isAttack) return;
//        PerformAttack().Forget();
//    }

//    private async UniTaskVoid PerformAttack()
//    {
//        isAttack = true;

//        Vector2 attackPos = (Vector2)attackTransform.position
//            + (Vector2)attackTransform.right * attackBoxDistance;

//        ExecuteAttackBox(attackPos);
//        await UniTask.WaitForSeconds(attackSecondHitDelay);
//        ExecuteAttackBox(attackPos);
//        await UniTask.WaitForSeconds(attackSecondHitDelay);
//        ExecuteAttackBox(attackPos);
//        await UniTask.WaitForSeconds(attackSecondHitDelay);

//        isAttack = false;
//    }

//    void ExecuteAttackBox(Vector2 attackPos)
//    {
//        Collider2D[] hits = Physics2D.OverlapBoxAll(
//            attackPos, attackBoxSize, attackTransform.eulerAngles.z, enemyLayer);

//        foreach (Collider2D hit in hits)
//        {
//            Debug.Log($"공격 적중: {hit.name}");
//            hit.GetComponent<BossBase>()?.TakeDamage(playerState.damage);
//            hit.GetComponent<ObjectHp>()?.OnDamage(playerState.damage);
//            hit.GetComponent<EnemyBase>()?.OnDamage(playerState.damage);
//            dashGauge = Mathf.Min(dashGauge + dashGaugePerAttack, maxDashGauge);
//            HitstopManager.Instance.TriggerHitstop();
//        }

//        Debug.DrawRay(attackTransform.position, attackTransform.right * attackBoxDistance, Color.magenta, 0.15f);
//    }

//    void OnDrawGizmosSelected()
//    {
//        if (groundCheck != null)
//        {
//            Gizmos.color = Color.green;
//            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
//        }
//        if (ceilingCheck != null)
//        {
//            Gizmos.color = Color.blue;
//            Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
//        }
//        if (wallCheck != null)
//        {
//            Gizmos.color = Color.red;
//            Gizmos.DrawRay(wallCheck.position, Vector2.right * wallCheckDistance);
//            Gizmos.color = Color.blue;
//            Gizmos.DrawRay(wallCheck.position, Vector2.left * wallCheckDistance);
//        }
//        if (wallCheckDistance > 0)
//        {
//            Gizmos.color = Color.yellow;
//            Gizmos.DrawWireCube(transform.position, dashBoxSize);
//        }
//        if (isAttack)
//        {
//            Gizmos.color = Color.magenta;
//            Vector2 attackPos = (Vector2)attackTransform.position + (Vector2)attackTransform.right * attackBoxDistance;
//            Gizmos.DrawWireCube(attackPos, attackBoxSize);
//        }
//    }

//    public void CancelAllTask()
//    {
//        wallJumpCts?.Cancel();
//        wallJumpCts?.Dispose();
//    }

//    private void OnDestroy()
//    {
//        wallJumpCts?.Cancel();
//        wallJumpCts?.Dispose();
//    }
//}