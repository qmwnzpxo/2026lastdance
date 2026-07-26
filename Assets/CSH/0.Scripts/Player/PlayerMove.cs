using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using DialogSystem.CinematicDialogue;

// Script Execution Order 설정 필요
// PlayerDash: -1  /  PlayerMove: 0
public class PlayerMove : MonoBehaviour
{
    // ── 이동 ─────────────────────────────────
    [Header("이동")]
    [SerializeField] private float moveSpeed = 10f;
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
    private bool _lastFacingRight = true;

    private float runSpeed = 2f;
    public float RunSpeed { get { return runSpeed; } set { runSpeed = value; } }

    // ── 점프 ─────────────────────────────────
    [Header("점프")]
    [SerializeField] private float jumpForce = 8f;
    public float JumpForce { get => jumpForce; set => jumpForce = value; }
    [SerializeField] private LayerMask groundLayer;
    private LayerMask groundOnlyLayer;
    private LayerMask walkableLayer;

    // ── 고정 오프셋 감지 ────────────────────
    [Header("고정 오프셋 감지")]
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

    // ── 벽 끝 자동 등반 ──────────────────────
    [Header("벽 끝 자동 등반")]
    [SerializeField] private float _climbEdgeCheckDist = 0.6f; // 벽 상단 모서리 감지 레이 길이
    [SerializeField] private float _climbSpeed = 8f; // 등반 시 위로 올라가는 속도
    [SerializeField] private float _climbHorizSpeed = 8f; // 등반 시 옆으로 이동하는 속도
    [SerializeField] private float _climbHeight = 1.0f; // 등반 완료 목표 높이 (이 값만큼 올라가면 종료)

    // ── 천장 붙기 ────────────────────────────
    [Header("천장 붙기")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [EditorOnly, SerializeField] private Transform spriteTransform;

    // ── 내부 필드 ────────────────────────────
    private Rigidbody2D rb;
    private float moveInput;
    private float verticalInput;

    private bool isTouchingCeiling;   // PlayerMove 전용 (외부에 불필요)
    private bool wasTouchingCeiling;
    
    private bool isWallSlideDown; // 벽 슬라이딩 중 아래 입력을 하는지 여부 (애니메이션 전환용)

    private bool isClimbing;
    private float climbTargetY;
    private float climbDirX;

    private float wallJumpTime = 0.1f;
    private float wallJumpCounter = 0f;
    private int wallJumpDirection;
    private CancellationTokenSource wallJumpCts;

    private Vector3 normalSpriteScale;
    private Vector3 flipSpriteScale;

    private PlayerState state;
    private PlayerAnimation ani;
    private TimeScaleReceiver timeScaleReceiver;

    // ── PlayerDash에서 필요한 공개 접근자 ───
    public Rigidbody2D Rb => rb;
    public LayerMask GroundLayer => walkableLayer;
    public Transform GroundCheck => groundCheck;
    //public Vector2 GroundCheckSize => new Vector2(colMax.x - colMin.x, 0.1f);
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public Transform SpriteTransform => spriteTransform;
    private bool _dashJustStopped;

    void OnDashStopped() => _dashJustStopped = true;

    // ────────────────────────────────────────
    #region Unity 생명주기
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        state = GetComponent<PlayerState>();
        ani = GetComponent<PlayerAnimation>();
        if (state == null)
        {
            state = GetComponent<PlayerState>();
        }

        timeScaleReceiver = GetComponent<TimeScaleReceiver>();
        timeScaleReceiver._animator = ani.ani;
        timeScaleReceiver._rb = rb;

        rb.gravityScale = state.defaultGravityScale;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        normalSpriteScale = spriteTransform.localScale;
        flipSpriteScale = new Vector3(normalSpriteScale.x,-normalSpriteScale.y,normalSpriteScale.z);
        if(groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("ground");
        }
        groundOnlyLayer = LayerMask.GetMask("groundOnly");
        walkableLayer = groundLayer | groundOnlyLayer;
        state.OnDashStopped += OnDashStopped;
    }

    private void Start()
    {
        //ApplyColliderBlend();
    }

    private void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.PlaybackCts != null)
        {
            Debug.LogWarning($"[PlayerMove] DialogueManager is active but PlaybackCts is null. Skipping Update.");
            return;
        }
        moveInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        CheckGround();
        CheckCeiling();
        CheckWall();
        //UpdateBlend();

        HandlePowerRun();
        HandleCeilingAttach();
        HandleWallSlide();
        HandleJump();
        HandleClimbEdge();
        HandleGetDamage();
    }

    void FixedUpdate()
    {
        bool skipAutoFlip = state.isAttacking || state.isDashing;

        if (!skipAutoFlip)
        {
            float velX = rb.linearVelocityX;
            if (Mathf.Abs(velX) >= 0.01f)
            {
                bool facingRight = velX > 0f;
                _lastFacingRight = facingRight;
                FlipXSprite(facingRight);
            }
        }

        // Dash / Tackle 중에는 PlayerDash가 물리를 전담
        if (state.isDashing || state.isRedDashPlaying)
            return;

        // 등반 중에는 등반 물리만 처리
        if (isClimbing)
        {
            ApplyClimbMovement();
            return;
        }

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
        state.isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, walkableLayer);
    }

    void CheckCeiling()
    {
        isTouchingCeiling = Physics2D.OverlapBox(ceilingCheck.position, ceilingCheckSize, 0f, groundLayer);
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
        if ((state.isWallSliding && !state.isGrounded) || state.isOnCeiling || (state.isPowerRun && moveInput != 0))
        {
            float gaugeChange = CustomTime.ScaledFixedDeltaTime(state.category) * (state.isPowerRun ? RunSpeed : 1f);
            state.ModifyGauge(-gaugeChange);
        }
        else
        {
            state.ModifyGauge(CustomTime.ScaledFixedDeltaTime(state.category) * 5);
        }
        FlipYSprite(state.isOnCeiling);

        bool blockedByWall = (state.isTouchingLWall || state.isTouchingRWall)
                             && !state.isGrounded && !state.isOnCeiling;
        float scale = CustomTimeManager.Instance.PlayerTimeScale;
        float xMove = blockedByWall ? 0f : moveInput * moveSpeed * scale;
        float yMove = rb.linearVelocity.y < 0 ?
              rb.linearVelocity.y - CustomTime.ScaledFixedDeltaTime(state.category) * 10 
            : rb.linearVelocity.y;
        rb.linearVelocity = new Vector2(xMove * (state.isPowerRun ? RunSpeed : 1f), yMove);
    }
    void HandlePowerRun()
    {
        state.isPowerRun = Input.GetKey(KeyCode.LeftShift) && (moveInput != 0 || verticalInput != 0) && !state.isExhausted;
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
            targetSlideSpeed = -wallSlideSpeedUp * (state.isPowerRun ? RunSpeed : 1f);
            isWallSlideDown = state.isTouchingRWall;
        }
        else if (verticalInput < 0 || pressingAwayFromWall)
        {
            targetSlideSpeed = -wallSlideSpeedDown * (state.isPowerRun ? RunSpeed : 1f);
            isWallSlideDown = !state.isTouchingRWall;
        }
        else
        {
            targetSlideSpeed = 0f;
            isWallSlideDown = state.isTouchingRWall;
        }
        /*
        float limitVelocity = rb.linearVelocityY;
        limitVelocity = Mathf.Clamp(limitVelocity,wallSlideSpeedUp,wallSlideSpeedDown);
        float smoothedY = Mathf.Lerp(limitVelocity
            , targetSlideSpeed, CustomTime.ScaledFixedDeltaTime(state.category) * 10f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, smoothedY);*/

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, targetSlideSpeed * (state.isPowerRun?1f:runSpeed));
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
            && !state.isExhausted;
        if(state.isWallSliding)
        {
            rb.gravityScale = 0f;
            if (verticalInput == 0 && moveInput == 0)
            {
                rb.linearVelocityY = 0f;
            }
        }
        else
        {
            RestoreGravity();
        }
    }

    #endregion

    // ────────────────────────────────────────
    #region 벽 끝 자동 등반
    /// <summary>
    /// 벽 슬라이딩 중 벽 상단 모서리를 감지하면 자동으로 등반 시작.
    /// 감지 원리:
    ///   - 플레이어 허리 높이에서 벽 방향으로 Raycast → 벽이 있으면 아직 벽 중간
    ///   - 플레이어 머리 높이에서 같은 방향으로 Raycast → 벽이 없으면 "모서리 발견"
    /// </summary>
    void HandleClimbEdge()
    {
        // 등반 중이면 완료 체크만
        if (isClimbing)
        {
            if (rb.position.y >= climbTargetY || state.isGrounded)
                FinishClimb();
            return;
        }

        // 대쉬 중 무시
        if (state.isDashing) return;

        bool touchingRight = state.isTouchingRWall;
        bool touchingLeft = state.isTouchingLWall;
        if (!touchingRight && !touchingLeft) return;

        float dirX = touchingRight ? 1f : -1f;
        Vector2 origin = rb.position;

        // 허리 높이(현재 위치)에서 벽 방향으로 쏴서 벽이 있는지 확인
        Vector2 midOrigin = origin + Vector2.up * (-_climbEdgeCheckDist/2f);
        RaycastHit2D midHit = Physics2D.Raycast(
            midOrigin, new Vector2(dirX, 0f), wallCheckDistance + 0.1f, groundLayer);

        // 머리 위(_climbEdgeCheckDist만큼) 높이에서 같은 방향으로 쏴서 벽이 없으면 모서리
        Vector2 topOrigin = origin + Vector2.up * _climbEdgeCheckDist;
        RaycastHit2D topHit = Physics2D.Raycast(
            topOrigin, new Vector2(dirX, 0f), wallCheckDistance + 0.1f, groundLayer);

        Vector2 botOrigin = origin + Vector2.up * (-_climbEdgeCheckDist / 0.9f);
        RaycastHit2D botHit = Physics2D.Raycast(
            botOrigin, new Vector2(dirX, 0f), wallCheckDistance + 0.1f, groundLayer);

        // 허리는 막혀있고, 머리 위는 뚫려있으면 → 모서리 감지
        if (midHit.collider != null && topHit.collider == null&&(moveInput!=0||verticalInput > 0))
        {
            // 벽 슬라이딩 중이 아니면 무시
            if (!state.isWallSliding) return;
            StartClimb(dirX, origin.y+0.5f);
        }
        else if(botHit.collider != null && midHit.collider != null && topHit.collider == null
            && (moveInput != 0 || verticalInput != 0))
        {
            StartClimb(dirX, origin.y);
        }
    }

    void StartClimb(float dirX, float currentY)
    {
        isClimbing = true;
        climbDirX = dirX;
        climbTargetY = currentY + _climbHeight;

        // 등반 중 벽 슬라이딩 상태 해제
        state.isWallSliding = false;
        state.isWallJumping = false;

        rb.gravityScale = 0f; // 등반 중 중력 제거
    }

    void ApplyClimbMovement()
    {
        // 위로 + 벽 방향(벽 위로 올라가면서 살짝 앞으로)
        rb.linearVelocity = new Vector2(
            climbDirX * _climbHorizSpeed,
            _climbSpeed
        );
    }

    void FinishClimb()
    {
        isClimbing = false;

        rb.linearVelocity = new Vector2(climbDirX * _climbHorizSpeed, 0f);
        RestoreGravity();
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
        await CustomTime.WaitForScaledSeconds(wallJumpTime, state.category, ct);
        wallJumpCounter = 0f;
        state.isWallJumping = false;
    }

    #endregion

    // ────────────────────────────────────────
    #region 천장 붙기

    void HandleCeilingAttach()
    {
        bool justTouchedCeiling = isTouchingCeiling && !wasTouchingCeiling;

        if (justTouchedCeiling && !state.isOnCeiling && (state.isDashing || _dashJustStopped))
        {
            state.isOnCeiling = true;
            RestoreGravity();
            FlipYSprite(state.isOnCeiling);
        }

        if (state.isOnCeiling && (!isTouchingCeiling || state.isExhausted))
            DetachFromCeiling();

        wasTouchingCeiling = isTouchingCeiling;
        _dashJustStopped = false;
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

    // 스프라이트 플립
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
        //spriteRenderer.flipY = flipY;
        spriteTransform.localScale = flipY ? flipSpriteScale : normalSpriteScale;
    }

    // 스프라이트 벽 방향에 따른 회전
    void RotateSprite()
    {
        if (spriteRenderer == null) return;
        //if (state.isOnCeiling) return;

        if (state.isTouchingLWall && (!state.isGrounded || !state.isOnCeiling))
        {
            spriteTransform.rotation = Quaternion.Euler(0, 0, -90);
            spriteRenderer.flipX = isWallSlideDown;
        }
        else if (state.isTouchingRWall && (!state.isGrounded || !state.isOnCeiling))
        {
            spriteTransform.rotation = Quaternion.Euler(0, 0, 90);
            spriteRenderer.flipX = isWallSlideDown;
        }
        else
        {
            spriteTransform.rotation = Quaternion.Euler(0, 0, 0);
            if (!state.isOnCeiling)
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
        else if ((verticalInput != 0f||moveInput != 0f) && state.isWallSliding)
        {
            ani.Run_on();
        }
        else
        {
            ani.Run_off();
            ani.Power_Run_off();
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

    // 벽타기 관련 상태를 초기화한다. Dash / Tackle 시작 시 호출
    public void ResetWallState()
    {
        state.isWallJumping = false;
        state.isWallSliding = false;
        wallJumpCounter = 0f;
        wallJumpCts?.Cancel();
    }

    // 중력 스케일을 현재 상태(천장/일반)에 맞게 복원한다
    public void RestoreGravity()
    {
        rb.gravityScale = state.isOnCeiling
            ? state.ceilingGravityScale
            : state.defaultGravityScale;
    }

    public void HandleGetDamage()
    {
        if (Input.GetKeyDown(KeyCode.P))
            state.TakeDamage(0f);
    }
    public void SetFacingRight(bool facingRight)
    {
        _lastFacingRight = facingRight;
        FlipXSprite(facingRight);
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
            Gizmos.DrawRay(wallCheck.position, Vector2.left * wallCheckDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(wallCheck.position + Vector3.up * (-_climbEdgeCheckDist / 0.9f), Vector2.right * wallCheckDistance);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(wallCheck.position + Vector3.up * (-_climbEdgeCheckDist / 0.9f), Vector2.left * wallCheckDistance);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(
                wallCheck.position + Vector3.up * _climbEdgeCheckDist,
                Vector2.right * wallCheckDistance);
            Gizmos.DrawRay(
                wallCheck.position + Vector3.up * _climbEdgeCheckDist,
                Vector2.left * wallCheckDistance);
        }

        // 현재 몸체(팔각형) 바운드 — 런타임에만 colMin/colMax 갱신됨
        /*Gizmos.color = Color.yellow;
        Vector2 pos = transform.position;
        Vector2 center = pos + (colMin + colMax) * 0.5f;
        Vector2 size = colMax - colMin;
        Gizmos.DrawWireCube(center, size);*/
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
        state.OnDashStopped -= OnDashStopped;
        wallJumpCts?.Cancel();
        wallJumpCts?.Dispose();
    }

    #endregion
}
