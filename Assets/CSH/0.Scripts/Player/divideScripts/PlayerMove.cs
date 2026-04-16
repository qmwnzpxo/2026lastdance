using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// ※ Script Execution Order 설정 필요
//   PlayerDash: -1  /  PlayerMove: 0
//   (Project Settings > Script Execution Order)
public class PlayerMove : MonoBehaviour
{
    // ── 이동 ─────────────────────────────────
    [Header("이동")]
    [SerializeField] private float moveSpeed = 10f;

    // ── 점프 ─────────────────────────────────
    [Header("점프")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private float groundCheckRadius = 0.22f;
    [SerializeField] private float ceilingCheckRadius = 0.22f;

    // ── 벽타기 ───────────────────────────────
    [Header("벽타기")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.52f;
    [SerializeField] private float wallSlideSpeed = 2.5f;
    [SerializeField] private float wallSlideSpeedUp = -15f;
    [SerializeField] private float wallSlideSpeedDown = 10f;
    [SerializeField] private float wallJumpForceX = 6f;
    [SerializeField] private float wallJumpForceY = 12f;

    // ── 천장 붙기 ────────────────────────────
    [Header("천장 붙기")]
    [SerializeField] private Transform spriteTransform;
    private bool justStoppedDashing; // PlayerDash에서 StopDash 직후 세팅
    private readonly Vector3 spriteScale = new Vector3(1, 1, 1);
    private readonly Vector3 ceilingSpriteScale = new Vector3(1, -1, 1);

    // ── 내부 필드 ────────────────────────────
    private Rigidbody2D rb;
    private float moveInput;
    private float verticalInput;
    private bool isTouchingCeiling;   // PlayerMove 전용 (외부에 불필요)
    private bool wasTouchingCeiling;
    private float wallJumpTime = 0.1f;
    private float wallJumpCounter = 0f;
    private int wallJumpDirection;
    private CancellationTokenSource wallJumpCts;

    private PlayerState state;

    // ── PlayerDash에서 필요한 공개 접근자 ───
    public Rigidbody2D Rb => rb;
    public LayerMask GroundLayer => groundLayer;
    public Transform GroundCheck => groundCheck;
    public float GroundCheckRadius => groundCheckRadius;
    public bool JustStoppedDashing { get => justStoppedDashing; set => justStoppedDashing = value; }

    // ────────────────────────────────────────
    #region Unity 생명주기
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        state = GetComponent<PlayerState>();
        rb.gravityScale = state.defaultGravityScale;
    }

    void Update()
    {
        moveInput    = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        CheckGround();
        CheckCeiling();
        CheckWall();

        HandleCeilingAttach();
        HandleWallSlide();
        HandleJump();
    }

    void FixedUpdate()
    {
        // Dash / Tackle 중에는 PlayerDash가 물리를 전담
        if (state.isDashing || state.isTackling || state.isTackleGroundRush)
            return;
        

        ApplyMovement();
        ApplyWallSlide();
    }

    #endregion
    // ────────────────────────────────────────
    #region 물리 체크
    void CheckGround()
    {
        state.isGrounded = Physics2D.OverlapCircle(
            groundCheck.position, groundCheckRadius, groundLayer);
    }

    void CheckCeiling()
    {
        isTouchingCeiling = Physics2D.OverlapCircle(
            ceilingCheck.position, ceilingCheckRadius, groundLayer);
    }

    void CheckWall()
    {
        state.isTouchingLWall = Physics2D.Raycast(
            wallCheck.position, Vector2.left,  wallCheckDistance, groundLayer);
        state.isTouchingRWall = Physics2D.Raycast(
            wallCheck.position, Vector2.right, wallCheckDistance, groundLayer);
    }

    #endregion
    // ────────────────────────────────────────
    #region 이동

    void ApplyMovement()
    {
        if (state.isWallJumping) return;

        bool blockedByWall = (state.isTouchingLWall || state.isTouchingRWall)
                             && !state.isGrounded && !state.isOnCeiling;
        float xMove = blockedByWall ? 0f : moveInput * moveSpeed;
        rb.linearVelocity = new Vector2(xMove, rb.linearVelocity.y);
    }

    void ApplyWallSlide()
    {
        if (!state.isWallSliding) return;

        float targetSlideSpeed;
        if      (verticalInput > 0) targetSlideSpeed = -wallSlideSpeedUp;
        else if (verticalInput < 0) targetSlideSpeed = -wallSlideSpeedDown;
        else                        targetSlideSpeed = -wallSlideSpeed;

        float smoothedY = Mathf.Lerp(
            rb.linearVelocity.y, targetSlideSpeed, Time.fixedDeltaTime * 10f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, smoothedY);
    }

    #endregion
    // ────────────────────────────────────────
    #region 벽타기

    void HandleWallSlide()
    {
        state.isWallSliding =
            (state.isTouchingLWall || state.isTouchingRWall)
            //&& !state.isGrounded
            && !state.isOnCeiling
            && !state.isDashing ;
    }

    #endregion
    // ────────────────────────────────────────
    #region 점프

    void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space) || state.isDashing) return;

        // 천장 붙기 상태에서 Space → 분리 후 아래로
        if (state.isOnCeiling)
        {
            DetachFromCeiling();
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce);
            return;
        }

        if (state.isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        else if (state.isWallSliding && wallJumpCounter <= 0f)
        {
            wallJumpDirection = state.isTouchingLWall ? 1 : -1;
            rb.linearVelocity = new Vector2(
                wallJumpDirection * wallJumpForceX, wallJumpForceY);
            state.isWallJumping = true;

            wallJumpCts?.Cancel();
            wallJumpCts = new CancellationTokenSource();
            StartWallJumpCooldown(wallJumpCts.Token).Forget();
        }
    }

    private async UniTaskVoid StartWallJumpCooldown(CancellationToken ct)
    {
        wallJumpCounter = wallJumpTime;
        await UniTask.WaitForSeconds(wallJumpTime, cancellationToken: ct);
        wallJumpCounter    = 0f;
        state.isWallJumping = false;
    }

    #endregion
    // ────────────────────────────────────────
    #region 천장 붙기

    void HandleCeilingAttach()
    {
        bool justTouchedCeiling = isTouchingCeiling && !wasTouchingCeiling;

        if (justTouchedCeiling && !state.isOnCeiling && (state.isDashing||state.isTackling|| justStoppedDashing))
        {
            state.isOnCeiling     = true;
            rb.gravityScale       = state.ceilingGravityScale;
            spriteTransform.localScale = ceilingSpriteScale;
        }

        if (state.isOnCeiling && !isTouchingCeiling)
            DetachFromCeiling();

        wasTouchingCeiling = isTouchingCeiling;
        justStoppedDashing = false; // ← 매 프레임 리셋
    }

    void DetachFromCeiling()
    {
        state.isOnCeiling      = false;
        rb.gravityScale        = state.defaultGravityScale;
        spriteTransform.localScale = spriteScale;
    }

    #endregion
    // ────────────────────────────────────────
    #region 공개 메서드 (PlayerDash에서 호출)

    /// <summary>벽타기 관련 상태를 초기화한다. Dash / Tackle 시작 시 호출.</summary>
    public void ResetWallState()
    {
        state.isWallJumping = false;
        state.isWallSliding = false;
        wallJumpCounter = 0f;
        wallJumpCts?.Cancel();
    }

    /// <summary>중력 스케일을 현재 상태(천장/일반)에 맞게 복원한다.</summary>
    public void RestoreGravity()
    {
        rb.gravityScale = state.isOnCeiling
            ? state.ceilingGravityScale
            : state.defaultGravityScale;
    }

    #endregion
    // ────────────────────────────────────────
    #region Gizmos

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
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(wallCheck.position, Vector2.left  * wallCheckDistance);
        }
    }

    #endregion
    // ────────────────────────────────────────
    #region Lifecycle

    public void CancelAllTask()
    {
        wallJumpCts?.Cancel();
        wallJumpCts?.Dispose();
    }

    void OnDestroy()
    {
        wallJumpCts?.Cancel();
        wallJumpCts?.Dispose();
    }

    #endregion
}
