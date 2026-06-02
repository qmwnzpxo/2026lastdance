using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    // ── 초록 칩 설정 ─────────────────────────
    [Header("초록 칩 - 3연 돌진")]
    [SerializeField] private int greenDashCount = 3;
    [SerializeField] private float greenDashRange = 5f;
    [SerializeField] private float greenDashSpeed = 55f;
    [SerializeField] private float _greenDashInterval = 0.09f; // 돌진 사이 입력 대기 시간
    
    // ── 파랑 칩 설정 ─────────────────────────
    [Header("파랑 칩 - 대각 대시 + 낙하 강타")]
    [SerializeField] private float blueFallSpeed = 80f;   // 낙하 속도
    [SerializeField] private float blueSlamRadius = 3f;    // 착지 범위 공격 반지름
    [SerializeField] private float blueSlamDamage = 8f;    // 착지 범위 공격 데미지

    // ── 공용 설정 ────────────────────────────
    [Header("공용")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);  // Sweep 판정 크기
    [RuntimeOnly] public float DashAttackBoxSize { set { attackBoxSize.x = value; attackBoxSize.y = value - 0.6f; } }
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
        HandleDash();
        //HandleTackle();
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
        _pinkDashCts?.Cancel();
        _pinkDashCts?.Dispose();
        _redDashCts?.Cancel();
        _redDashCts?.Dispose();
        _greenDashCts?.Cancel();
        _greenDashCts?.Dispose();
        _blueQTECts?.Cancel();
        _blueQTECts?.Dispose();
        _blueFallCts?.Cancel();
        _blueFallCts?.Dispose();
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
        if (state.isTackling || state.isTackleGroundRush || _blueDashSecondInput) return;

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
            ? groundHit.point - dashDirection * 0.05f
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
    #region 초록 칩 – 입력식 최대 3연속 대쉬
    void StartGreenDash()
    {
        if (_greenDashing || _greenComboStep > 0) return; // 이미 돌진 중이면 무시
        if (!state.TryUseDashGauge(state.DashCost)) return;

        _greenDashCts?.Cancel();
        _greenDashCts?.Dispose();
        _greenDashCts = new CancellationTokenSource();
        GreenDashComboRoutine(_greenDashCts.Token).Forget();
    }

    private async UniTaskVoid GreenDashComboRoutine(CancellationToken ct)
    {
        _greenComboStep = 0;
        _greenDashBuffered = false;

        try
        {
            while (_greenComboStep < greenDashCount && !ct.IsCancellationRequested)
            {
                // 1회 대쉬 수행
                await GreenDashSegment(ct);
                _greenComboStep++;

                if (_greenComboStep >= greenDashCount) break; // 마지막 대쉬 완료 → 종료

                // 다음 대쉬 입력 대기 (0.75초). 공중에 잠시 고정.
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;

                bool advanced = await WaitGreenWindow(ct);
                if (!advanced) break; // 입력 없음 → 콤보 종료(초기화)
            }
        }
        finally
        {
            EndGreenCombo();
        }
    }

    private async UniTask<bool> WaitGreenWindow(CancellationToken ct)
    {
        float t = 0f;
        while (t < _greenDashInterval)
        {
            if (ct.IsCancellationRequested) return false;
            if (_greenDashBuffered) return true; // 입력(또는 이동 중 버퍼된 입력) 감지

            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return false;

            t += CustomTime.ScaledFixedDeltaTime(state.category);
        }
        return _greenDashBuffered;
    }

    private async UniTask GreenDashSegment(CancellationToken ct)
    {
        _greenDashing = true;
        _greenDashBuffered = false; // 이번 대쉬를 트리거한 입력 소모
        ani.Attack_flash();

        Vector2 dir = GetMouseDirection();
        cameraFollow.SetCameraDumping(cameraDumpingScale);
       
        Vector2 start = rb.position;
        Vector2 target = start + dir * greenDashRange;

        RaycastHit2D groundHit = Physics2D.Raycast(
            start + dir * 0.05f, dir, greenDashRange, playerMove.GroundLayer);
        if (groundHit.collider != null)
            target = groundHit.point;

        SetLineRenderer(start, target, true);

        rb.gravityScale = 0f;
        state.isDashing = true;
        rb.linearVelocity = Vector2.zero;

        HashSet<Collider2D> hitThisDash = new HashSet<Collider2D>();
        var timeManager = CustomTimeManager.Instance;
        float traveled = 0f;
        float dist = Vector2.Distance(start, target);

        while (!ct.IsCancellationRequested && traveled < dist)
        {
            Vector2 cur = rb.position;
            Vector2 next = Vector2.MoveTowards(cur, target, greenDashSpeed * timeManager.PlayerFixedDeltaTime);

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
            traveled += greenDashSpeed * timeManager.PlayerFixedDeltaTime;

            // 벽 충돌 시 돌진 중단
            Collider2D wallHit = Physics2D.OverlapBox(
                rb.position, dashBoxSize, 0f, playerMove.GroundLayer);
            if (wallHit != null) break;

            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) break;
        }

        state.isDashing = false;
        _greenDashing = false;
    }

    void EndGreenCombo()
    {
        _greenComboStep = 0;
        _greenDashBuffered = false;
        _greenDashing = false;

        state.isDashing = false;
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.up * 0.05f;
        if (line != null) line.enabled = false;
    }
    #endregion
    // ────────────────────────────────────────
    #region 파랑 칩 – 대각 대시 + 낙하 공격
    void ExecuteBlueDash()
    {
        // 마우스 방향을 대각선 2방향 중 하나로 스냅
        Vector2 rawDir = GetMouseDirection();
        Vector2 snappedDir = SnapToDiagonal(rawDir);

        dashDirection = snappedDir;

        Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point
            : (Vector2)transform.position + dashDirection * dashRange;

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
    }

    /// <summary>입력 벡터를 가장 가까운 45° 대각선(상단 2방향)으로 스냅</summary>
    private Vector2 SnapToDiagonal(Vector2 dir)
    {
        // 대각선 4방향: (±√0.5, ±√0.5)
        float x = dir.x >= 0f ? 1f : -1f;
        float y = 1f;
        return new Vector2(x, y).normalized;
    }

    void StartBlueQTE()
    {
        _blueQTECts?.Cancel();
        _blueQTECts?.Dispose();
        _blueQTECts = new CancellationTokenSource();
        BlueQTERoutine(_blueQTECts.Token).Forget();
    }
    private async UniTaskVoid BlueQTERoutine(CancellationToken ct)
    {
        var tmg = CustomTimeManager.Instance;

        // 플레이어 + 적 슬로우
        tmg.SetPlayerTimeScale(0.02f);
        tmg.SetEnemyTimeScale(0.02f);

        float elapsed = 0f;
        bool activated = false;

        while (elapsed < 1f && !ct.IsCancellationRequested)
        {
            tmg.SetPlayerTimeScale(elapsed / 2);
            tmg.SetEnemyTimeScale(elapsed / 2);
            elapsed += Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                activated = true;
                break;
            }
            // Yield으로 update 프레임마다 대기 (슬로우 중에도 입력 체크 가능)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);
        }

        // 슬로우 즉시 해제
        tmg.SetPlayerTimeScale(1f);
        tmg.SetEnemyTimeScale(1f);

        if (activated && !ct.IsCancellationRequested)
            StartBlueFall();
    }

    void StartBlueFall()
    {
        _blueFallCts?.Cancel();
        _blueFallCts?.Dispose();
        _blueFallCts = new CancellationTokenSource();

        _blueFalling = true;
        Debug.Log($"blueFall start");
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        BlueFallRoutine(_blueFallCts.Token).Forget();
    }

    private async UniTaskVoid BlueFallRoutine(CancellationToken ct)
    {
        var tmg = CustomTimeManager.Instance;

        while (!ct.IsCancellationRequested && _blueFalling)
        {
            float step = blueFallSpeed * tmg.PlayerFixedDeltaTime;
            Vector2 cur = rb.position;
            Vector2 next = cur + Vector2.down * step;

            // 고속 낙하 시 지형 관통 방지: BoxCast로 착지 감지
            RaycastHit2D hit = Physics2D.BoxCast(
                cur,
                playerMove.GroundCheckSize,
                0f,
                Vector2.down,
                step + 0.1f,
                playerMove.GroundLayer);

            if (hit.collider != null)
            {
                // 착지 지점에 딱 맞춰 이동 후 강타
                rb.MovePosition(new Vector2(cur.x, hit.point.y + playerMove.GroundCheckSize.y * 0.5f));
                await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                BlueSlamLand();
                return;
            }

            rb.MovePosition(next);
            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
        }
    }


    void BlueSlamLand()
    {
        _blueFalling = false;
        Debug.Log($"blueFall end");

        // 범위 내 적 전체 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, blueSlamRadius, enemyLayer);
        foreach (var hit in hits)
            ApplyDamage(hit, state.damage * blueSlamDamage);

        // 상태 복구
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.zero;
        _blueDashSecondInput = false;
        if (line != null) line.enabled = false;

        HitstopManager.Instance.TriggerHeavy();
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


#region 태클(래거시)
/*

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

        StopTackle();
    }

    #endregion
    // ────────────────────────────────────────

*/
#endregion