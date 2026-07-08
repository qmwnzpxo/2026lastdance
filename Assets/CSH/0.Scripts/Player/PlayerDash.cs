using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using CSHPlayer;

// ※ Script Execution Order: PlayerDash = -1 로 설정할 것
//   (Project Settings > Script Execution Order)
//   → PlayerDash.FixedUpdate 가 PlayerMove.FixedUpdate 보다 먼저 실행되어야 return 분기가 정상 동작
/// <summary>
/// 플레이어 대쉬 
/// - 핑크 칩: 기본 대쉬
/// </summary>
[DefaultExecutionOrder(-1)]
public partial class PlayerDash : MonoBehaviour
{
    // ── 대쉬 설정 ────────────────────────────
    [Header("대쉬")]
    [SerializeField] private float dashRange = 8f;
    [SerializeField] private float dashDamage = 5f;
    [SerializeField] private float dashSpeed = 50f;
    [SerializeField] private Vector2 dashBoxSize = new Vector2(1f, 0.4f);
    // ── 빨강 칩 설정 ─────────────────────────
    [Header("빨강 칩 - 프로-모션")]
    [SerializeField] private float redRecordDuration = 5f;
    [SerializeField] private float redRecordFPS = 30f;
    private List<GhostMoveData> moveRecords = new List<GhostMoveData>();
    private List<GhostEventData> eventRecords = new List<GhostEventData>();
    private List<EnemySnapshotData> enemySnapshots = new List<EnemySnapshotData>();
    [SerializeField] private GameObject Ghost;
    // ── 초록 칩 설정 ─────────────────────────
    [Header("초록 칩 - 3연 돌진")]
    [SerializeField] private int greenDashCount = 3;
    [SerializeField] private float greenDashRange = 5f;
    [SerializeField] private float greenDashSpeed = 50f;
    [SerializeField] private float _greenDashInterval = 0.75f; // 돌진 사이 입력 대기 시간
    
    // ── 파랑 칩 설정 ─────────────────────────
    [Header("파랑 칩 - 대각 대시 + 낙하 강타")]
    [SerializeField] private float blueFallSpeed = 80f;   // 낙하 속도
    [SerializeField] private float blueSlamRadius = 3f;    // 착지 범위 공격 반지름
    [SerializeField] private float blueSlamDamage = 8f;    // 착지 범위 공격 데미지

    // ── 공용 설정 ────────────────────────────
    [Header("공용")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);  // Sweep 판정 크기
    [RuntimeOnly] public float DashAttackBoxSize { set { attackBoxSize.x = value; attackBoxSize.y = value; } }
    [SerializeField] private LayerMask enemyLayer;
    private LayerMask bulletLayer;
    private LayerMask combined;
    private float cameraDumpingScale = 0.2f;
    private float originalCameraDumping = 1f;

    // ── 대쉬 내부 상태 ───────────────────────
    private Vector2 dashDirection;
    private Vector2 dashTargetPosition;
    private bool dashHitEnemy;
    private Vector2 dashOrigin;
    private readonly HashSet<Collider2D> hitEnemiesInDash = new HashSet<Collider2D>();

    // ── 파랑 칩 내부 상태 ────────────────────
    [SerializeField]private bool _blueFalling = false; // 낙하 강타 진행 중
    [SerializeField]private bool _blueDashSecondInput = false; // 대시 도중 재입력 받았는지

    // ── 초록 칩 내부 상태 ────────────────────
    private bool _greenDashing = false; // 초록 칩 대시 모션중
    private int  _greenComboStep = 0; // 초록 칩 대시 몇 번째 단계인지
    private bool _greenDashBuffered = false; // 초록 칩 대시 중 입력(버퍼 포함)

    // ── CTS ──────────────────────────────────
    private CancellationTokenSource _pinkDashCts;
    private CancellationTokenSource _redDashCts;
    private CancellationTokenSource _greenDashCts;
    private CancellationTokenSource _blueQTECts;
    private CancellationTokenSource _blueFallCts;

    // ── 참조 ─────────────────────────────────
    private PlayerState state;
    private PlayerMove playerMove;
    private Rigidbody2D rb;
    private PolygonCollider2D col;
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
        col = GetComponent<PolygonCollider2D>();

        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("enemy");
        if (bulletLayer == 0)
            bulletLayer = LayerMask.GetMask("enemyBullet");
        combined = enemyLayer | bulletLayer;
        if (Ghost == null && ghostSprite == null)
        {
            Ghost = new GameObject("PlayerGhost");
            ghostSprite = Ghost.AddComponent<SpriteRenderer>();
        }
        if(ghostSprite == null)
            ghostSprite = Ghost.GetComponent<SpriteRenderer>();
        playerSprite = playerMove.SpriteRenderer;
    }

    void Start()
    {
        // Awake 이후 PlayerMove.Rb 접근이 안전
        rb = playerMove.Rb;
        Ghost.SetActive(false);
    }

    void Update()
    {
        HandleDash();
    }

     void FixedUpdate()
    {
        if (_blueFalling)
        {
            return;
        }

        if (state.isDashing)
        {
            if (_greenDashing) return;
            FixedUpdateDash();
            return;
        }
    }
    
    void CtsFree()
    {
        try
        {
            _pinkDashCts?.Cancel();
            _redDashCts?.Cancel();
            _greenDashCts?.Cancel();
            _blueQTECts?.Cancel();
            _blueFallCts?.Cancel();

            _pinkDashCts?.Dispose();
            _redDashCts?.Dispose();
            _greenDashCts?.Dispose();
            _blueQTECts?.Dispose();
            _blueFallCts?.Dispose();

            if (CustomTimeManager.Instance != null)
            {
                CustomTimeManager.Instance.ResetAll();
                CustomTimeManager.Instance.ResetHitstopMultiplier();
            }
        }
        finally
        {
            _pinkDashCts = null;
            _redDashCts = null;
            _greenDashCts = null;
            _blueQTECts = null;
            _blueFallCts = null;
        }
    }
    void OnDestroy()
    {
        CtsFree();
    }

    void OnDisable()
    {
        CtsFree();
    }

    #endregion
    // ────────────────────────────────────────
    #region 대쉬 – Update 입력

    void HandleDash()
    {
        if (_blueDashSecondInput) return;

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

            // ─── 초록 칩: 콤보 진행 중이면 다음 대쉬 입력으로 처리 ───
            if (state.currentChip == PlayerChip.green && (_greenDashing || _greenComboStep > 0))
            {
                _greenDashBuffered = true;
                return;
            }

            if (state.isRedDashRecording || state.isRedDashPlaying) return;

            playerMove.ResetWallState();
            ani.Dash();

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
                    if (!state.isRedDashRecording && !state.isRedDashPlaying)
                        StartRedDash();
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
                    ExecutePinkDash(GetMouseDirection());
                    return;
            }
        }
    }

    #endregion
    // ────────────────────────────────────────
    #region 핑크(기본) 대쉬
    private void ExecutePinkDash(Vector2 dir)
    {
        if (!state.TryUseDashGauge(state.DashCost)) return;
        if (state.isDashing) return;

        _pinkDashCts?.Cancel();
        _pinkDashCts?.Dispose();
        _pinkDashCts = new CancellationTokenSource();

        ExecuteDash(dir, _pinkDashCts).Forget();
    }


    private async UniTaskVoid ExecuteDash(Vector2 dir,CancellationTokenSource cts)
    {
        dashDirection = dir;

        Vector2 origin = (Vector2)transform.position;
        Vector2 rayOrigin = origin + dashDirection * 0.05f;

        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);

        await CustomTime.WaitForScaledSeconds(0.06f, state.category, cts);
        
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point - dashDirection
            : origin + dashDirection * dashRange;


        SetLineRenderer(origin, dashTargetPosition, true);

        //if (state.TryUseDashGauge(state.DashCost))
        TriggerDash(origin);
    }

    void TriggerDash(Vector2 origin)
    {
        dashOrigin = origin;

        state.isGrounded = false;
        state.isOnCeiling = false;
        state.isTouchingLWall = false;
        state.isTouchingRWall = false;

        rb.gravityScale = 0f;
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        dashHitEnemy = false;
        hitEnemiesInDash.Clear();

        rb.linearVelocity = Vector2.zero;
        state.isDashing = true;
    }

    void StopDash()
    {
        state.isDashing = false;
        state.StartImmunity(0.06f);
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);

        if (line != null) line.enabled = false;
        if (!state.isOnCeiling)
            rb.linearVelocity = Vector2.up * 0.05f;
        if (state.currentChip == PlayerChip.blue)
        {
            StartBlueQTE();
            return;

        }
        if (!_blueDashSecondInput && state.dashBuffered)
        {
            state.dashBuffered = false;
            _pinkDashCts?.Cancel();
            _pinkDashCts?.Dispose();
            _pinkDashCts = new CancellationTokenSource();
            ExecuteDash(GetMouseDirection(),_pinkDashCts).Forget();
        }
    }

    void FixedUpdateDash()
    {

        rb.MovePosition(dashTargetPosition);

        // 원래 위치 → 목표 위치 구간 전체를 한 번에 공격 판정
        SweepDashEnemies(dashOrigin, dashTargetPosition);
        
        // 이동·판정 완료, 즉시 종료
        StopDash();
    }

    void SweepDashEnemies(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0f) return;

        dir /= distance;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            from, attackBoxSize, 0f, dir, distance, combined);

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
            //target.gameObject.GetComponent<IDamageable>()?.TakeDamage(damage);
            var damaged = target.GetComponents<IDamageable>();
            if (damaged == null) return;
            else
            {
                foreach (IDamageable damageable in damaged)
                {
                    damageable.TakeDamage(damage);
                }
            }
        }
        catch (System.NullReferenceException ex)
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
