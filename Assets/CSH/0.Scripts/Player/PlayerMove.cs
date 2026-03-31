using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float runSpeed = 15f;
    [SerializeField] private bool run = false;
    [SerializeField] private bool notbattle = true;

    [Header("점프")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform  ceilingCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float ceilingCheckRadius = 0.2f;

    [Header("벽타기")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.3f;
    [SerializeField] private float wallSlideSpeed = 1.5f;
    [SerializeField] private float wallSlideSpeedUp = 0f;
    [SerializeField] private float wallSlideSpeedDown = 5f;
    [SerializeField] private float wallJumpForceX = 8f;
    [SerializeField] private float wallJumpForceY = 12f; 
    

    [Header("천장 붙기")]
    [SerializeField] private float defaultGravityScale = 1.2f;
    [SerializeField] private float ceilingGravityScale = -1.2f;
    [SerializeField] private float ceilingTimer;
    [SerializeField] private float ceilingTimeMultiplier;
    [SerializeField]private Transform spriteTransform;
    private Vector3 spriteScale = new Vector3(1, 1, 1);
    private Vector3 ceilingSpriteScale = new Vector3(1, -1, 1);

    [Header("대쉬")]
    [SerializeField] private float dashGauge = 0f;
    [SerializeField] private float maxDashGauge = 5f;
    [SerializeField] private List<Collider2D> hitEnemiesInDash = new List<Collider2D>();
    private float dashSpeed = 8f;           
    private float enhancedDashSpeed = 40f;  
    private float dashDuration = 0.1f;
    private float normalDashCooldown = 0.8f;
    private float dashCooldownCounter = 0f;
    private int pendingDashes = 0;          
    private Vector2 dashBoxSize = new Vector2(1f, 0.4f);
    private float dashCounter;
    private Vector2 dashDirection;
    private float dashGaugePerAttack = 0.1f;
    private bool dashHitEnemy = false;

    [Header("공격")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);
    [SerializeField] private float attackBoxDistance = 1f;
    [SerializeField] private float attackSecondHitDelay = 0.15f;
    [SerializeField] private LayerMask enemyLayer;

    private Rigidbody2D rb;
    private float moveInput;

    [Header("불값 모음")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isTouchingCeiling;
    [SerializeField] private bool isOnCeiling;
    private bool wasTouchingCeiling;
    [SerializeField] private bool isTouchingRWall;
    [SerializeField] private bool isTouchingLWall;
    [SerializeField] private bool isWallSliding;
    [SerializeField] private bool isWallJumping;
    [SerializeField] private bool isDashing;
    [SerializeField] private bool isAttack;

    private float wallJumpTime = 0.2f;
    private float wallJumpCounter;
    private int wallJumpDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 함수 연산 및 입력 처리용 update
    void Update()
    {
        moveInput = Input.GetAxis("Horizontal");
        //moveInput *= isGrounded || isOnCeiling ? 1f : 0.5f; // 공중에서 이동 속도 감소

        CheckGround();
        CheckCeiling();
        CheckWall();

        RotateAttackPivot();
        HandleAttack();
        HandleCeilingAttach();
        HandleWallSlide();
        HandleDash();
        HandleJump();

        callingHitstop();

    }

    // 물리 연산용 fixedUpdate
    void FixedUpdate()
    {
        if (!isDashing)
        {
            float xMove = ((isTouchingLWall||isTouchingRWall) && !isGrounded && !isOnCeiling) ? 0f : moveInput * moveSpeed;
            rb.linearVelocity = new Vector2(xMove, rb.linearVelocity.y);
        }

        if (isWallSliding)
        {
            float wallDir = moveInput != 0 ? moveInput : transform.localScale.x;
            if (moveInput != 0 || moveInput != wallDir)
                isWallSliding = false;

            float verticalInput = Input.GetAxisRaw("Vertical");
            float targetSlideSpeed;

            if (verticalInput > 0)
                targetSlideSpeed = -wallSlideSpeedUp;
            else if (verticalInput < 0)
                targetSlideSpeed = -wallSlideSpeedDown;
            else
                targetSlideSpeed = -wallSlideSpeed;

            float smoothedY = Mathf.Lerp(rb.linearVelocity.y, targetSlideSpeed, Time.fixedDeltaTime * 10f);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, smoothedY);
            
        }
    }

    void callingHitstop()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            HitstopManager.Instance.TriggerHitstop(); // 기본: 0.1초 동안 0.05배속 히트스탑
        }
    }

    void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }
    void CheckCeiling()
    {
        isTouchingCeiling = Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
    }

    void CheckWall()
    {
        isTouchingLWall = Physics2D.Raycast(wallCheck.position,
            Vector2.left, wallCheckDistance, groundLayer);
        isTouchingRWall = Physics2D.Raycast(wallCheck.position,
            Vector2.right, wallCheckDistance, groundLayer);

    }
    void HandleWallSlide()
    {
        isWallSliding = (isTouchingLWall || isTouchingRWall) && !isGrounded && !isOnCeiling && !isDashing && rb.linearVelocity.y < 0;
    }

    void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space) || isDashing) return;

        if (isOnCeiling)
        {
            DetachFromCeiling();
            return;
        }
        
        
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        else if (isWallSliding&&wallJumpCounter<=0)
        {
            wallJumpDirection = -(int)Mathf.Sign(rb.linearVelocity.x);
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpForceX, wallJumpForceY);
            Debug.Log("wallJump");
            isWallJumping = true;
            wallJumpCounter = wallJumpTime;
        }
    }

    void DetachFromCeiling()
    {
        isOnCeiling = false;
        rb.gravityScale = defaultGravityScale;
        spriteTransform.localScale=spriteScale;
    }

    void HandleCeilingAttach()
    {
        bool justTouchedCeiling = isTouchingCeiling && !wasTouchingCeiling;

        if (justTouchedCeiling && !isOnCeiling)
        {
            if (isDashing
                /* && rb.linearVelocity.magnitude >= moveSpeed*/)
            {
                isOnCeiling = true;
                rb.gravityScale = ceilingGravityScale;
                spriteTransform.localScale = ceilingSpriteScale;
            }
        }

        if (isOnCeiling && !isTouchingCeiling)
            DetachFromCeiling();

        if (isOnCeiling && !isDashing && Input.GetKeyDown(KeyCode.Space))
        {
            DetachFromCeiling();
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce);
        }

        wasTouchingCeiling = isTouchingCeiling;
    }

    void HandleDash()
    {
        if (dashCooldownCounter > 0)
            dashCooldownCounter -= Time.deltaTime;

        if (Input.GetMouseButtonDown(1))
        {


            if (isGrounded)
                rb.position += new Vector2(0f, 0.05f);
            if (isOnCeiling)
                rb.position += new Vector2(0f, -0.05f);
            if (isTouchingLWall)
                rb.position += new Vector2(-0.05f, 0f);
            if (isTouchingRWall)
                rb.position += new Vector2(0.05f, 0f);

            isWallJumping = false;
            isWallSliding = false;
            wallJumpCounter = 0f;

            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -Camera.main.transform.position.z;
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            dashDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

            isDashing = true;
            dashCounter = dashDuration;

            rb.gravityScale = 0f;
            rb.linearVelocity = dashDirection * dashSpeed;

            if (dashGauge >= 1f)
            {
                TriggerDash(true);
            }
            else if (dashCooldownCounter <= 0)
            {
                TriggerDash(false);
            }
            else
            {
                if (dashGauge >= 1f + pendingDashes)
                    pendingDashes++;
            }

            Debug.DrawRay(transform.position, dashDirection * 5f, Color.red, 1f);
        }

        if (isDashing)
        {
            dashCounter -= Time.deltaTime;

            Collider2D[] enemyHits = Physics2D.OverlapBoxAll(transform.position,
                dashBoxSize+(Vector2.one*0.1f), 0f, enemyLayer);
            foreach (Collider2D enemy in enemyHits)
            {
                if (!hitEnemiesInDash.Contains(enemy))
                {
                    hitEnemiesInDash.Add(enemy);
                    // hit.GetComponent<Enemy>()?.TakeDamage(10);
                    Debug.Log($"대쉬 공격 적중: {enemy.name}");

                    if (!dashHitEnemy)
                    {
                        dashHitEnemy = true;
                        dashGauge = Mathf.Min(dashGauge + 1f, maxDashGauge);
                    }
                }
            }

            Collider2D wallHit = Physics2D.OverlapBox(transform.position, dashBoxSize, 0f, groundLayer);
            if (wallHit != null)
            {
                StopDash();
                return;
            }

            if (dashCounter <= 0)
                StopDash();
        }
    }

    void StopDash()
    {
        isDashing = false;
        rb.gravityScale = isOnCeiling ? ceilingGravityScale : defaultGravityScale;

        if (!isOnCeiling)
            rb.linearVelocity = Vector2.up * 0.05f;

        if (pendingDashes > 0 && dashGauge >= 1f)
        {
            pendingDashes--;
            TriggerDash(true);
        }
        else
        {
            pendingDashes = 0;
        }
    }
    void TriggerDash(bool enhanced)
    {
        if (isGrounded)
            rb.position += new Vector2(0f, 0.05f);
        if (isOnCeiling)
            rb.position += new Vector2(0f, -0.05f);
        if (isTouchingLWall)
            rb.position += new Vector2(-0.05f, 0f);
        if (isTouchingRWall)
            rb.position += new Vector2(0.05f, 0f);

        isWallJumping = false;
        isWallSliding = false;
        wallJumpCounter = 0f;

        if (enhanced)
            dashGauge -= 1f;
        else
            dashCooldownCounter = normalDashCooldown; 


        dashHitEnemy = false;
        hitEnemiesInDash.Clear();

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -Camera.main.transform.position.z;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        dashDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

        isDashing = true;
        dashCounter = dashDuration;

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDirection * (enhanced ? enhancedDashSpeed : dashSpeed);

        Debug.DrawRay(transform.position, dashDirection * 5f, Color.red, 1f);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!isDashing) return;
        if (((1 << collision.gameObject.layer) & groundLayer) == 0) return;

        Vector2 normal = collision.contacts[0].normal;
        Vector2 slideVelocity = dashDirection - (Vector2.Dot(dashDirection, normal) * normal);
        rb.linearVelocity = slideVelocity * dashSpeed;
    }

    void RotateAttackPivot()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -Camera.main.transform.position.z;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2 dir = (mouseWorldPos - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        attackTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
    void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0) || isAttack) return;

        PerformAttack().Forget();
    }
    private async UniTaskVoid PerformAttack()
    {
        isAttack = true;

        Vector2 attackPos = (Vector2)attackTransform.position
            + (Vector2)attackTransform.right * attackBoxDistance;

        ExecuteAttackBox(attackPos);
        await UniTask.WaitForSeconds(attackSecondHitDelay);

        ExecuteAttackBox(attackPos);
        await UniTask.WaitForSeconds(attackSecondHitDelay);

        //ExecuteAttackBox();
        isAttack = false;
    }
    void ExecuteAttackBox(Vector2 attackPos)
    {

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPos, attackBoxSize, attackTransform.eulerAngles.z, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            Debug.Log($"공격 적중: {hit.name}");
            // hit.GetComponent<Enemy>()?.TakeDamage(10);
            dashGauge = Mathf.Min(dashGauge + dashGaugePerAttack, maxDashGauge);
        }

        Debug.DrawRay(attackTransform.position, attackTransform.right * attackBoxDistance, Color.magenta, 0.15f);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(wallCheck.position, Vector2.right * wallCheckDistance);
            Gizmos.DrawRay(wallCheck.position, Vector2.left * wallCheckDistance);
        }
        if(wallCheckDistance > 0) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, dashBoxSize);
        }
        if (isAttack)
        {
            Gizmos.color = Color.magenta;
            Vector2 attackPos = (Vector2)attackTransform.position + (Vector2)attackTransform.right * attackBoxDistance;
            Gizmos.DrawWireCube(attackPos, attackBoxSize);
        }
    }
}