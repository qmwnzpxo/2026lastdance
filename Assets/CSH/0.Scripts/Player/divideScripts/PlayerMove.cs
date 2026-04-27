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
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.44f, 0.1f);
    [SerializeField] private Vector2 ceilingCheckSize = new Vector2(0.44f, 0.1f);

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
    [SerializeField] private SpriteRenderer spriteRenderer;
    private bool justStoppedDashing; // PlayerDash에서 StopDash 직후 세팅

    // ── 내부 필드 ────────────────────────────
    private Rigidbody2D rb;
    private float moveInput;
    private float verticalInput;
    private bool isTouchingCeiling;   // PlayerMove 전용 (외부에 불필요)
    private bool wasTouchingCeiling;
    private bool isWallSildeDown; // 벽 슬라이딩 중 아래 입력을 하는지 여부 (애니메이션 전환용)
    private float wallJumpTime = 0.1f;
    private float wallJumpCounter = 0f;
    private int wallJumpDirection;
    private CancellationTokenSource wallJumpCts;

    private PlayerState state;
    private PlayerAnimation ani;

    // ── PlayerDash에서 필요한 공개 접근자 ───
    public Rigidbody2D Rb => rb;
    public LayerMask GroundLayer => groundLayer;
    public Transform GroundCheck => groundCheck;
    public Vector2 GroundCheckSize => groundCheckSize;
    public bool JustStoppedDashing { get => justStoppedDashing; set => justStoppedDashing = value; }

    // ────────────────────────────────────────
    #region Unity 생명주기
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        state = GetComponent<PlayerState>();
        ani = GetComponent<PlayerAnimation>();
        rb.gravityScale = state.defaultGravityScale;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        moveInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        CheckGround();
        CheckCeiling();
        CheckWall();

        HandlePowerRun();
        HandleCeilingAttach();
        HandleWallSlide();
        HandleJump();
        HandleGetDamage();
    }

    void FixedUpdate()
    {
        if (Mathf.Abs(rb.linearVelocityX) >= 0.01)
        {
            FlipXSprite(rb.linearVelocityX >= 0.01 ? true : false);
        }
        // Dash / Tackle 중에는 PlayerDash가 물리를 전담
        if (state.isDashing || state.isTackling || state.isTackleGroundRush)
            return;
        RotateSprite();
        ApplyMovement();
        ApplyWallSlide();
        ApplyAnimation();
    }

    #endregion
    // ────────────────────────────────────────
    #region 물리 체크
    void CheckGround()
    {
        state.isGrounded = Physics2D.OverlapBox(
            groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    void CheckCeiling()
    {
        isTouchingCeiling = Physics2D.OverlapBox(
            ceilingCheck.position, ceilingCheckSize, 0f, groundLayer);
        ani.IsGrounded();
    }

    void CheckWall()
    {
        state.isTouchingLWall = Physics2D.Raycast(
            wallCheck.position, Vector2.left, wallCheckDistance, groundLayer);
        state.isTouchingRWall = Physics2D.Raycast(
            wallCheck.position, Vector2.right, wallCheckDistance, groundLayer);
    }

    #endregion
    // ────────────────────────────────────────
    #region 이동

    void ApplyMovement()
    {
        if (state.isWallJumping) return;
        if ((state.isWallSliding && !state.isGrounded) || state.isOnCeiling || state.isPowerRun)
        {
            float gaugeChange = Time.fixedDeltaTime * (state.isPowerRun ? 1.5f : 1f);
            state.ModifyGauge(-gaugeChange);
        }
        else
        {
            state.ModifyGauge(Time.fixedDeltaTime * 5);
        }
        FlipYSprite(state.isOnCeiling);

        bool blockedByWall = (state.isTouchingLWall || state.isTouchingRWall)
                             && !state.isGrounded && !state.isOnCeiling;
        float xMove = blockedByWall ? 0f : moveInput * moveSpeed;
        float yMove = rb.linearVelocity.y < 0 ? rb.linearVelocity.y - Time.fixedDeltaTime * 10 : rb.linearVelocity.y;
        rb.linearVelocity = new Vector2(xMove * (state.isPowerRun ? 1.5f : 1f), yMove);
    }

    void HandlePowerRun()
    {
        state.isPowerRun = Input.GetKey(KeyCode.LeftShift);
    }

    void ApplyWallSlide()
    {
        if (!state.isWallSliding) return;

        bool pressingTowardWall   = (state.isTouchingLWall && moveInput < -0.1f)
                                  ||(state.isTouchingRWall && moveInput >  0.1f);
        bool pressingAwayFromWall = (state.isTouchingLWall && moveInput >  0.1f)
                                  ||(state.isTouchingRWall && moveInput < -0.1f);


        float targetSlideSpeed;

        if (verticalInput > 0 || pressingTowardWall)
        {
            targetSlideSpeed = -wallSlideSpeedUp * (state.isPowerRun ? 1.5f : 1f);
            isWallSildeDown = state.isTouchingRWall;
        }
        else if (verticalInput < 0 || pressingAwayFromWall)
        {
            targetSlideSpeed = -wallSlideSpeedDown * (state.isPowerRun ? 1.5f : 1f);
            isWallSildeDown = !state.isTouchingRWall;
        }
        else
        {
            targetSlideSpeed = -wallSlideSpeed * (state.isPowerRun ? 1.5f : 1f);
            isWallSildeDown = state.isTouchingRWall;
        }

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
            && !state.isDashing
            && state.stamina > 0;
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
        wallJumpCounter = 0f;
        state.isWallJumping = false;
    }

    #endregion
    // ────────────────────────────────────────
    #region 천장 붙기

    void HandleCeilingAttach()
    {
        bool justTouchedCeiling = isTouchingCeiling && !wasTouchingCeiling;

        if (justTouchedCeiling && !state.isOnCeiling && (state.isDashing || state.isTackling || justStoppedDashing))
        {
            state.isOnCeiling = true;
            RestoreGravity();
            FlipYSprite(state.isOnCeiling);
        }

        if (state.isOnCeiling && (!isTouchingCeiling || state.stamina <= 0))
            DetachFromCeiling();

        wasTouchingCeiling = isTouchingCeiling;
        justStoppedDashing = false; // ← 매 프레임 리셋
    }

    void DetachFromCeiling()
    {
        state.isOnCeiling = false;
        RestoreGravity();
        //spriteTransform.localScale = spriteScale;
        FlipYSprite(state.isOnCeiling);
    }

    #endregion
    // ────────────────────────────────────────
    #region 스프라이트 플립 및 회전

    /// <summary>스프라이트 플립</summary>
    void FlipXSprite(bool flipX)
    {
        if (spriteRenderer == null) return;

        // 벽 슬라이딩 중에는 이동 방향과 상관없이 스프라이트를 고정 (벽 방향으로 회전하기 때문)
        if(state.isWallSliding) return;

        spriteRenderer.flipX = flipX;
    }

    void FlipYSprite(bool flipY)
    {
        if (spriteRenderer == null) return;

        //spriteRenderer.transform.localScale = flipY ? ceilingSpriteScale : spriteScale;
        spriteRenderer.flipY = flipY;
    }

    /// <summary>스프라이트 벽 방향에 따른 회전</summary>
    void RotateSprite()
    {
        if (spriteRenderer == null) return;

        if (state.isTouchingLWall && !state.isGrounded)
        {
            spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, -90);
            spriteRenderer.flipX = isWallSildeDown; // 벽 슬라이딩 중 아래 입력 시 스프라이트를 아래로 뒤집음
        }
        else if (state.isTouchingRWall && !state.isGrounded)
        {
            spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, 90);
            spriteRenderer.flipX = isWallSildeDown; // 벽 슬라이딩 중 아래 입력 시 스프라이트를 아래로 뒤집음
        }
        else
        {
            spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, 0);
            if(!state.isOnCeiling)
                spriteRenderer.flipY = false;
        }
    }

    #endregion
    // ────────────────────────────────────────
    #region 애니메이션 이벤트

    void ApplyAnimation()
    {
        Ani_Run();
        // 일섬 애니메이션은 PlayerCombat에서 트리거하므로 여기서는 처리하지 않음
    }

    void Ani_Run()
    {
        if (!state.isWallSliding)
        {
            if (Mathf.Abs(moveInput) > 0f)
            {
                ani.Run_on();
                if (Input.GetKey(KeyCode.LeftShift))
                    ani.Power_Run_on();
                else ani.Power_Run_off();
            }
            if (Mathf.Abs(rb.linearVelocityX) <= 0.01) ani.Run_off();
        }
        else if (verticalInput != 0f)
        {
            ani.Run_on();
        }


        if (state.isAttacking)
        {
            ani.Run_off();
            ani.Power_Run_off();
        }
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
        Debug.Log($"Restoring gravity. OnCeiling: {state.isOnCeiling}");
        rb.gravityScale = state.isOnCeiling
            ? state.ceilingGravityScale
            : state.defaultGravityScale;
    }

    public void HandleGetDamage()
    {
        if (Input.GetKeyDown(KeyCode.P))
            state.TakeDamage(0f);
    }
    #endregion
    // ────────────────────────────────────────
    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(ceilingCheck.position, ceilingCheckSize);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(wallCheck.position, Vector2.right * wallCheckDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(wallCheck.position, Vector2.left * wallCheckDistance);
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
