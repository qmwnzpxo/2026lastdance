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
    [SerializeField] private Vector3 wallCheckOffset = new Vector3(0.5f, 0, 0f);
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
    private float dashSpeed = 60f;
    private float dashDuration = 0.1f;
    private Vector2 dashBoxSize = new Vector2(1f, 0.4f);
    private bool isDashing;
    private float dashCounter;
    private Vector2 dashDirection;

    private Rigidbody2D rb;
    private float moveInput;

    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isTouchingCeiling;
    [SerializeField] private bool isOnCeiling;
    private bool wasTouchingCeiling;
    [SerializeField] private bool isTouchingWall;
    [SerializeField] private bool isWallSliding;

    [SerializeField] private bool isWallJumping;
    private float wallJumpTime = 0.2f;
    private float wallJumpCounter;
    private int wallJumpDirection;
    Vector2 mouseWorldPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 함수 연산 및 입력 처리용 update
    void Update()
    {
        moveInput = Input.GetAxis("Horizontal");
        moveInput *= isGrounded || isOnCeiling ? 1f : 0.5f; // 공중에서 이동 속도 감소

        CheckGround();
        CheckCeiling();
        CheckWall();
        HandleCeilingAttach();
        HandleWallSlide();
        HandleJump();
        HandleWallJumpTimer();
        HandleDash();
        callingHitstop();
    }

    // 물리 연산용 fixedUpdate
    void FixedUpdate()
    {
        if (!isWallJumping && !isDashing)
        {
            float xMove = (isTouchingWall && !isGrounded && !isOnCeiling) ? 0f : moveInput * moveSpeed;
            rb.linearVelocity = new Vector2(xMove, rb.linearVelocity.y);
        }

        if (isWallSliding && !isDashing)
        {
            float wallDir = moveInput != 0 ? moveInput : transform.localScale.x;
            if (moveInput != 0 && moveInput != wallDir)
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
            HitstopManager.Instance.TriggerHitstop(); // 0.1초 동안 0.05배속 히트스탑
        }
    }

    void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }
    void CheckCeiling()
    {
        if (isDashing)
        {
            isTouchingCeiling = Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
        }
    }

    void CheckWall()
    {
        float dir = moveInput != 0 ? moveInput : transform.localScale.x;
        isTouchingWall = Physics2D.Raycast(wallCheck.position + wallCheckOffset,
            Vector2.right * dir, wallCheckDistance, groundLayer);

    }
    void HandleWallSlide()
    {
        isWallSliding = isTouchingWall && !isGrounded && !isOnCeiling && !isDashing && rb.linearVelocity.y < 0;
    }

    void HandleJump()
    {
        if (isOnCeiling) return;

        if (!Input.GetKeyDown(KeyCode.Space) || isDashing) return;

        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        else if (isWallSliding)
        {
            wallJumpDirection = -(int)Mathf.Sign(rb.linearVelocity.x);
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpForceX, wallJumpForceY);

            isWallJumping = true;
            wallJumpCounter = wallJumpTime;
        }
    }

    void HandleWallJumpTimer()
    {
        if (!isWallJumping) return;

        wallJumpCounter -= Time.deltaTime;
        if (wallJumpCounter <= 0)
            isWallJumping = false;
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
            if (isDashing && rb.linearVelocity.magnitude > moveSpeed)
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


    // 대쉬 시작할 때 기존 상태 초기화
    void HandleDash()
    {
        if (Input.GetMouseButtonDown(1) && !isDashing)
        {
            if(isGrounded)
                rb.position += new Vector2(0f, 0.05f);
            if(isOnCeiling)
                rb.position += new Vector2(0f, -0.05f);
            if(isTouchingWall)
                rb.position += new Vector2(-Mathf.Sign(rb.linearVelocity.x) * 0.05f, 0f);
            // 대쉬 시작 시 기존 상태 초기화
            isWallJumping = false;
            isWallSliding = false;
            wallJumpCounter = 0f;

            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -Camera.main.transform.position.z;
            mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            dashDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

            isDashing = true;
            dashCounter = dashDuration;

            rb.gravityScale = 0f;
            rb.linearVelocity = dashDirection * dashSpeed;

            Debug.DrawRay(transform.position, dashDirection * 5f, Color.red, 1f);
        }

        if (isDashing)
        {
            dashCounter -= Time.deltaTime;
            RaycastHit2D rayhit=Physics2D.Raycast(wallCheck.position + wallCheckOffset,
            mouseWorldPos, wallCheckDistance, groundLayer);
            Collider2D hit = Physics2D.OverlapBox(transform.position, dashBoxSize, 0f, groundLayer);
            if (hit != null)
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

        // 천장에 붙은 상태면 velocity 유지, 아니면 0
        if (!isOnCeiling)
            rb.linearVelocity = Vector2.up*0.05f;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!isDashing) return;
        if (((1 << collision.gameObject.layer) & groundLayer) == 0) return;

        Vector2 normal = collision.contacts[0].normal;
        Vector2 slideVelocity = dashDirection - (Vector2.Dot(dashDirection, normal) * normal);
        rb.linearVelocity = slideVelocity * dashSpeed;
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
            Gizmos.DrawRay(wallCheck.position + wallCheckOffset, Vector2.right * wallCheckDistance);
        }
        if(wallCheckDistance > 0) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, dashBoxSize);
        }
    }
}