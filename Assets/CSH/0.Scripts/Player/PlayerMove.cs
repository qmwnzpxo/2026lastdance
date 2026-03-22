using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 7f;

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

    private Rigidbody2D rb;
    private float moveInput;

    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isTouchingCeiling;
    [SerializeField] private bool isTouchingWall;
    [SerializeField] private bool isWallSliding;

    [SerializeField] private bool isWallJumping;
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
        moveInput = Input.GetAxisRaw("Horizontal");
        moveInput*= isGrounded? 1f : 0.5f; // 공중에서 이동 속도 감소

        CheckGround();
        CheckCeiling();
        CheckWall();
        HandleWallSlide();
        HandleJump();
        HandleWallJumpTimer();
        callingHitstop();
    }

    // 물리 연산용 fixedUpdate
    void FixedUpdate()
    {
        if (!isWallJumping)
        {
            // 벽 슬라이딩 중엔 X 이동 입력 무시
            float xMove = (isTouchingWall&&!isGrounded) ? 0f : moveInput * moveSpeed;
            rb.linearVelocity = new Vector2(xMove, rb.linearVelocity.y);
        }

        if (isWallSliding)
        {
            // 벽에서 떨어지는 키 (반대 방향) 입력 시 벽에서 떨어지게
            float wallDir = moveInput != 0 ? moveInput : transform.localScale.x;
            if (moveInput != 0 && moveInput != wallDir)
            {
                isWallSliding = false;
            }

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
        { // 예시: H 키를 눌렀을 때 히트스탑 발동
            HitstopManager.Instance.TriggerHitstop(); // 예시: 0.1초 동안 0.05배속 히트스탑
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
        float dir = moveInput != 0 ? moveInput : transform.localScale.x;
        isTouchingWall = Physics2D.Raycast(wallCheck.position,
            Vector2.right * dir, wallCheckDistance, groundLayer);
    }

    void HandleWallSlide()
    {
        isWallSliding = isTouchingWall && !isGrounded && rb.linearVelocity.y < 0;
    }

    void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;

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
        }
    }
}