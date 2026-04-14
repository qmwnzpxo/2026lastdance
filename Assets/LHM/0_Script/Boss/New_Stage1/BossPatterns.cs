using System.Threading;
using Boss.Phase1;
using Cysharp.Threading.Tasks;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
// BossPatterns.cs  —  1 · 2 페이즈 전체 패턴 통합 파일
//
// ── 중복 제거 내역 ────────────────────────────────────────────────────────
//   Before: 각 패턴마다 SpawnWarningLine / FireLaserAsync / MovePanel 중복
//   After:  LaserUtility 정적 메서드로 위임 → 패턴 클래스는 흐름만 담당
//
// ── 네임스페이스 통일 ────────────────────────────────────────────────────
//   Before: Boss.Phase1 / BossSystem.Patterns 혼재
//   After:  BossSystem.Patterns 단일 네임스페이스
// ═══════════════════════════════════════════════════════════════════════════

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // 1 페이즈
    // ───────────────────────────────────────────────────────────────────────

    #region Phase 1 ── 방어막 (Persistent)

    /// <summary>
    /// 레이저 코어 4개가 모두 파괴될 때까지 보스를 무적 상태로 유지.
    ///
    /// ── 흐름 ────────────────────────────────────────────────────────────
    ///   AcquireInvincibility → 코어 파괴 폴링 → 조건 충족 시 자동 종료
    ///   (Blackboard.OnBarrierBroken 이벤트는 마지막 코어 파괴 시 자동 발생)
    ///
    /// ── CancellationToken 처리 ───────────────────────────────────────────
    ///   InvincibilityHandle은 using 스코프로 묶여 있으므로
    ///   ct 취소(페이즈 전환/보스 파괴) 시에도 무적 누수 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase1_Shield",
                     menuName = "BossSystem/Patterns/Phase1_Shield")]
    public class Phase1ShieldPattern : BossPatternBase
    {
        [Header("Shield")]
        [SerializeField] private GameObject _shieldVfxPrefab;
        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {

            // InvincibilityHandle: 구조체 IDisposable → using 블록 종료 시 자동 Release
            using var invHandle = ctx.DamageHandler.CreateInvincibilityHandle();

            GameObject vfx = null;
            if (_shieldVfxPrefab != null)
                vfx = Object.Instantiate(_shieldVfxPrefab,
                          ctx.BossTransform.position, Quaternion.identity,
                          ctx.BossTransform);

            // 코어 파괴 완료 폴링
            while (!ct.IsCancellationRequested)
            {
                if (ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
                {
                    Log("All cores destroyed. Shield deactivated.");
                    break;
                }
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_pollInterval),
                    cancellationToken: ct);
            }

            if (vfx != null) Object.Destroy(vfx);
            // invHandle Dispose → ReleaseInvincibility 자동 호출
        }
    }

    #endregion


    #region Phase 1 ── 360도 레이저 (Persistent)

    /// <summary>
    /// 4개의 레이저 바디를 상하좌우로 배치하고 보스 중심으로 회전시키는 패턴.
    ///
    /// ── Circular Link 재연결 ────────────────────────────────────────────
    ///   UpdateLaserBeams()가 매 프레임 "살아있는 바디만" 순환 연결.
    ///   정적 버퍼 _aliveIndicesBuffer로 매 프레임 GC 발생 없음.
    ///
    /// ── 수학: 회전 위치 계산 ────────────────────────────────────────────
    ///   각 바디 i의 각도 = (globalAngle + i × 90°) × Deg2Rad
    ///   pos.x = center.x + cos(rad) × radius
    ///   pos.y = center.y + sin(rad) × radius
    ///   → Mathf.Cos/Sin은 라디안 입력이므로 반드시 Deg2Rad 변환.
    /// </summary>
  #region Phase 1 ── 360도 레이저 (Persistent)

    [CreateAssetMenu(fileName = "Pattern_Phase1_360Laser",
                     menuName = "BossSystem/Patterns/Phase1_360Laser")]
    public class Phase1LaserPattern : BossPatternBase
    {
        [Header("Laser Cores")]
        [SerializeField] private GameObject _laserCorePrefab;
        // ── [수정] 커스텀 스크립트 대신 유니티 내장 LineRenderer 사용 ──
        [SerializeField] private LineRenderer _laserBeamPrefab;
        [SerializeField] private float _radius = 5f;
        [SerializeField] private float _rotationSpeed = 30f;  // 도/초

        [Header("Damage Settings")]
        [SerializeField] private float _damagePerSecond = 20f;
        [SerializeField] private float _laserThickness = 0.5f;
        [SerializeField] private LayerMask _playerLayer;

        private static readonly Vector3[] _spawnDirs =
        {
            Vector3.up, Vector3.right, Vector3.down, Vector3.left
        };

        private static readonly int[] _aliveBuffer = new int[4];

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            var bossPos = ctx.BossTransform.position;

            // ── 코어 스폰 ─────────────────────────────────────────────────
            var cores = new LaserCore[4];
            for (int i = 0; i < 4; i++)
            {
                var go = Object.Instantiate(_laserCorePrefab,
                             bossPos + _spawnDirs[i] * _radius, Quaternion.identity);
                cores[i] = go.GetComponent<LaserCore>();

                int idx = i;
                cores[idx].OnCoreDestroyed += (destroyedCore) =>
                {
                    ctx.DestroyedCoreCount++;
                    Log($"Core {idx} destroyed. Total: {ctx.DestroyedCoreCount}");
                };
            }

            // ── 빔 스폰 (LineRenderer 직접 활용) ───────────────────────────
            var beams = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                beams[i] = Object.Instantiate(_laserBeamPrefab);
                beams[i].enabled = false; // 처음엔 꺼둠
            }

            // ── 메인 루프 ─────────────────────────────────────────────────
            float angle = 0f;
            while (!ct.IsCancellationRequested)
            {
                angle += _rotationSpeed * Time.deltaTime;
                UpdateCorePositions(ctx.BossTransform.position, cores, angle);

                // 빔 연결 및 데미지 판정을 한 번에 처리
                UpdateBeamsAndDamage(cores, beams, _laserThickness, _playerLayer, _damagePerSecond);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Cleanup(cores, beams);
        }

        private void UpdateCorePositions(Vector3 center, LaserCore[] cores, float angle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i] == null || !cores[i].gameObject.activeInHierarchy) continue;

                float rad = (angle + i * 90f) * Mathf.Deg2Rad;
                cores[i].transform.position = new Vector3(
                    center.x + Mathf.Cos(rad) * _radius,
                    center.y + Mathf.Sin(rad) * _radius,
                    center.z
                );
            }
        }

        private static void UpdateBeamsAndDamage(LaserCore[] cores, LineRenderer[] beams, float thickness, LayerMask playerLayer, float dps)
        {
            int aliveCount = 0;
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i] != null && cores[i].gameObject.activeInHierarchy)
                    _aliveBuffer[aliveCount++] = i;
            }

            // 일단 모든 빔 끄기
            for (int i = 0; i < beams.Length; i++)
                if (beams[i] != null) beams[i].enabled = false;

            // 코어가 2개 미만이면 빔 연결 불가
            if (aliveCount < 2) return;

            Vector2 castSize = new Vector2(0.1f, thickness);

            for (int i = 0; i < aliveCount; i++)
            {
                int next = (i + 1) % aliveCount;

                Vector3 from = cores[_aliveBuffer[i]].transform.position;
                Vector3 to = cores[_aliveBuffer[next]].transform.position;
                Vector3 dir = to - from;
                float dist = dir.magnitude;

                if (dist < 0.01f) continue;

                // 1. 시각적 선 연결
                if (beams[i] != null)
                {
                    beams[i].enabled = true;
                    beams[i].SetPosition(0, from);
                    beams[i].SetPosition(1, to);
                }

                // 2. 물리 판정 (LaserBeamManager의 방식을 그대로 가져옴)
                var hit = Physics2D.BoxCast(
                    origin: from,
                    size: castSize,
                    angle: Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                    direction: dir.normalized,
                    distance: dist,
                    layerMask: playerLayer
                );

                if (hit.collider != null)
                {
                    // TODO: 프로젝트의 플레이어 데미지 처리 인터페이스에 맞게 주석을 풀고 적용하세요.
                    // 예: hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(dps * Time.deltaTime);
                }
            }
        }

        private static void Cleanup(LaserCore[] cores, LineRenderer[] beams)
        {
            foreach (var c in cores) if (c != null) Object.Destroy(c.gameObject);
            foreach (var b in beams) if (b != null) Object.Destroy(b.gameObject);
        }
    }
    #endregion


    #region Phase 1 ── 판넬 조준 사격 (Sequential)

    /// <summary>
    /// 판넬이 보스 위치에서 소환 → waypoint 순환 이동 → 조준 경고 → 발사.
    ///
    /// ── 흐름 (1 사이클) ─────────────────────────────────────────────────
    ///   for each waypoint:
    ///     MoveTowards → AimAt2D → FadeInWarningLine → CircleCast 발사
    ///
    /// ── LaserUtility 위임 ────────────────────────────────────────────────
    ///   이동: LaserUtility.MoveTowardsAsync
    ///   경고: LaserUtility.FadeInWarningLineAsync
    ///   발사: LaserUtility.CircleCastAndDamage
    ///   → 이 클래스는 "흐름 제어"만 담당, 물리/렌더링 로직 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase1_Panel",
                     menuName = "BossSystem/Patterns/Phase1_Panel")]
    public class Phase1PanelPattern : BossPatternBase
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _moveSpeed       = 5f;
        [SerializeField] private float _warningDuration = 2f;
        [SerializeField] private float _fireDuration    = 0.3f;
        [SerializeField] private float _postFireDelay   = 0.5f;

        [Header("Laser")]
        [SerializeField] private float     _castRadius   = 0.2f;
        [SerializeField] private float     _castDistance = 30f;
        [SerializeField] private float     _laserDamage  = 25f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color        _warningColor = new Color(1f, 0.3f, 0f, 1f);
        [SerializeField] private Color        _fireColor    = Color.red;
        [SerializeField] private GameObject   _muzzleVfxPrefab;

        // GC-safe Color 버퍼 (LaserUtility.FadeInWarningLineAsync에 ref로 전달)
        private Color _colorBuffer;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                Debug.LogWarning("[PanelPattern] No waypoints assigned.");
                return;
            }

            var panelGo = Object.Instantiate(_panelPrefab,
                              ctx.BossTransform.position, Quaternion.identity);
            var panelTf = panelGo.transform;

            // 경고선 인스턴스 재사용 (waypoint 순환 중 매번 생성/파괴 방지)
            LineRenderer warnLine = null;
            if (_warningLinePrefab != null)
                warnLine = Object.Instantiate(_warningLinePrefab);

            try
            {
                for (int i = 0; i < _waypoints.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    // ── 1. 이동 ───────────────────────────────────────────
                    await LaserUtility.MoveTowardsAsync(
                        panelTf, _waypoints[i].position, _moveSpeed, ct);

                    // ── 2. 조준 ───────────────────────────────────────────
                    LaserUtility.AimAt2D(panelTf, ctx.PlayerTransform.position);

                    // ── 3. 경고선 페이드인 ────────────────────────────────
                    // trackTarget 델리게이트: 경고 중 플레이어 이동 추적
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panelTf.position,             // 시작점 람다화
                        () => ctx.PlayerTransform.position,
                        _warningColor,

                        _warningDuration,
                        ct);

                    ct.ThrowIfCancellationRequested();

                    // ── 4. 발사 (색상 전환 → 판정 → 지속) ──────────────
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(
                            warnLine,
                            panelTf.position,
                            ctx.PlayerTransform.position,
                            _fireColor);

                    if (_muzzleVfxPrefab != null)
                        LaserUtility.DestroyAfterDelayAsync(
                            Object.Instantiate(_muzzleVfxPrefab, panelTf.position, panelTf.rotation),
                            _fireDuration, ct).Forget();

                    LaserUtility.CircleCastAndDamage(
                        panelTf.position,
                        panelTf.right,          // 에임 방향
                        _castRadius,
                        _castDistance,
                        _playerLayer,
                        _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);

                    LaserUtility.HideLine(warnLine);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_postFireDelay), cancellationToken: ct);
                }
            }
            finally
            {
                if (warnLine  != null) Object.Destroy(warnLine.gameObject);
                if (panelGo   != null) Object.Destroy(panelGo);
            }
        }
    }

    #endregion


    // ───────────────────────────────────────────────────────────────────────
    // 2 페이즈
    // ───────────────────────────────────────────────────────────────────────

    #region Phase 2 ── 좌우 스윕 레이저 (Sequential)

    /// <summary>
    /// 맵 외곽에서 좌 → 위 → 우 순서로 스윕 레이저를 발사하는 패턴.
    ///
    /// ── 발사 시퀀스 ─────────────────────────────────────────────────────
    ///   for each direction (left, up, right):
    ///     경고 FadeIn (_warningDuration초) → BoxCast 판정 → 다음 방향
    ///
    /// ── BoxCast 선택 근거 ────────────────────────────────────────────────
    ///   맵 전체를 가로지르는 '판형' 레이저 → 두께가 있는 직사각형 단면.
    ///   BoxCast의 size.x = 두께, distance = 맵 폭 으로 1회 캐스트로 처리.
    ///
    /// ── LaserUtility 위임 ────────────────────────────────────────────────
    ///   경고: FadeInWarningLineAsync
    ///   발사: BoxCastAndDamage
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_SideLaser",
                     menuName = "BossSystem/Patterns/Phase2_SideLaser")]
    public class Phase2SideLaserPattern : BossPatternBase
    {
        [Header("Side Laser")]
        [SerializeField] private float _warningDuration = 3f;
        [SerializeField] private float _laserDuration   = 0.4f;
        [SerializeField] private float _mapEdgeOffset   = 20f;     // 맵 밖 소환 오프셋

        [Header("Damage")]
        [SerializeField] private float     _laserThickness = 0.5f;
        [SerializeField] private float     _castDistance   = 40f;
        [SerializeField] private float     _laserDamage    = 35f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color        _warningColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color        _fireColor    = Color.red;

        // 발사 순서: 왼쪽 벽 → 천장 → 오른쪽 벽 (기획: 좌→위→우)
        // (origin 오프셋, 발사 방향)
        private static readonly (Vector2 offset, Vector2 dir)[] _sweepData =
        {
            (new Vector2(-1f, 0f), Vector2.right),   // 왼쪽에서 오른쪽으로
            (new Vector2(0f,  1f), Vector2.down),    // 위에서 아래로
            (new Vector2( 1f, 0f), Vector2.left),    // 오른쪽에서 왼쪽으로
        };

        private Color _colorBuffer;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // 경고선 인스턴스를 3방향 루프에서 재사용
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                foreach (var (offset, dir) in _sweepData)
                {
                    ct.ThrowIfCancellationRequested();

                    // 발사 기점: 플레이어 위치 기준 맵 외곽
                    Vector2 playerPos2D = ctx.PlayerTransform.position;
                    Vector2 origin      = playerPos2D + offset * _mapEdgeOffset;

                    // 경고선 끝점: 반대쪽 맵 외곽
                    Vector2 endPos = origin + dir * _castDistance;

                    // ── 경고 페이드인 ─────────────────────────────────────
                    // trackTarget은 고정 끝점 반환 (좌우 레이저는 위치 고정)
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () =>origin,
                        () => endPos,
                        _warningColor,
                        _warningDuration,
                        ct);

                    ct.ThrowIfCancellationRequested();

                    // ── 발사 전환 ─────────────────────────────────────────
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, origin, endPos, _fireColor);

                    // ── 발사 판정 ─────────────────────────────────────────
                    LaserUtility.BoxCastAndDamage(
                        origin, dir, _laserThickness, _castDistance,
                        _playerLayer, _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_laserDuration), cancellationToken: ct);

                    LaserUtility.HideLine(warnLine);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
            }
        }
    }

    #endregion


    #region Phase 2 ── 거울 반사 레이저 (Sequential)

    /// <summary>
    /// 보스 레이저 → 거울 반사 → 보스 피격 파훼 기믹 패턴.
    ///
    /// ── 흐름 ────────────────────────────────────────────────────────────
    ///   Mirror 소환 → _chargeTime 대기 → 레이저 발사 시작
    ///   → Mirror.OnReflectedToBoss 이벤트 or 타임아웃까지 대기
    ///   → 파훼 성공 시 보스 데미지 → 정리
    ///
    /// ── UniTaskCompletionSource 브리지 ──────────────────────────────────
    ///   C# 이벤트(OnReflectedToBoss)를 UniTask 흐름에 연결.
    ///   UniTask.WhenAny(laserLoop, reflectTcs.Task)로
    ///   파훼 or CancellationToken 중 먼저 오는 것에 즉시 반응.
    ///
    /// ── 재귀 레이캐스트 (Mirror 컴포넌트) ───────────────────────────────
    ///   Vector2.Reflect(incident, normal) : 수식 r = d - 2(d·n)n
    ///   최대 깊이 5회, 자기 재충돌 epsilon 오프셋 포함.
    ///   → Mirror.cs 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_MirrorLaser",
                     menuName = "BossSystem/Patterns/Phase2_MirrorLaser")]
    public class Phase2MirrorLaserPattern : BossPatternBase
    {
        [Header("Mirror")]
        [SerializeField] private GameObject _mirrorPrefab;
        [SerializeField] private Transform  _mirrorSpawnPoint;  // 맵 하단 가운데 빈 오브젝트
        [SerializeField] private float      _chargeTime        = 2f;
        [SerializeField] private float      _maxLaserDuration  = 12f;

        [Header("Damage")]
        [SerializeField] private float     _playerDPS         = 25f;   // 직사 레이저 초당 데미지
        [SerializeField] private float     _reflectDamage     = 50f;   // 반사 성공 시 보스 데미지
        [SerializeField] private float     _castRadius        = 0.15f;
        [SerializeField] private float     _castDistance      = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _laserLinePrefab;
        [SerializeField] private Color        _laserColor = Color.red;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // ── 거울 소환 ────────────────────────────────────────────────
            Vector3 spawnPos = _mirrorSpawnPoint != null
                ? _mirrorSpawnPoint.position
                : ctx.BossTransform.position + Vector3.down * 6f;

            var mirrorGo = Object.Instantiate(_mirrorPrefab, spawnPos, Quaternion.identity);
            var mirror   = mirrorGo.GetComponent<Mirror>();

            if (mirror == null)
            {
                Debug.LogError("[MirrorLaser] Mirror component missing on prefab!");
                Object.Destroy(mirrorGo);
                return;
            }

            mirror.Initialize(ctx.BossTransform);

            // C# 이벤트 → UniTask 브리지
            var reflectTcs = new UniTaskCompletionSource();
            mirror.OnReflectedToBoss += () => reflectTcs.TrySetResult();

            // 레이저 시각 라인
            LineRenderer laserLine = _laserLinePrefab != null
                ? Object.Instantiate(_laserLinePrefab) : null;

            try
            {
                // ── 충전 대기 ────────────────────────────────────────────
                Log($"Charging laser... ({_chargeTime}s)");
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_chargeTime), cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                // ── 레이저 발사 루프 시작 ────────────────────────────────
                mirror.StartReflecting();

                var laserLoopTask = RunLaserLoopAsync(ctx, laserLine, ct);

                // WhenAny: 파훼 성공 or 타임아웃 or CancellationToken 중 먼저 반응
                var timeoutTask = UniTask.Delay(
                    System.TimeSpan.FromSeconds(_maxLaserDuration), cancellationToken: ct);

                await UniTask.WhenAny(
                    laserLoopTask,
                    reflectTcs.Task.AttachExternalCancellation(ct),
                    timeoutTask);

                // ── 파훼 성공 → 보스 데미지 ──────────────────────────────
                if (reflectTcs.Task.Status == UniTaskStatus.Succeeded)
                {
                    ctx.DamageHandler.TakeDamage(_reflectDamage);
                    Log($"Reflected! Boss took {_reflectDamage} damage.");
                }
            }
            finally
            {
                if (laserLine != null) Object.Destroy(laserLine.gameObject);
                if (mirrorGo  != null) Object.Destroy(mirrorGo);
            }
        }

        /// <summary>
        /// 레이저 시각 라인 갱신 + 플레이어 DPS 판정 루프.
        /// CancellationToken 취소 시 자동 종료.
        /// </summary>
        private async UniTask RunLaserLoopAsync(
            BossContext ctx, LineRenderer line, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Vector2 origin = ctx.BossTransform.position;
                Vector2 dir    = ((Vector2)ctx.PlayerTransform.position - origin).normalized;

                // 레이저 라인 업데이트 (LateUpdate 대신 루프 내 갱신 — 단일 책임)
                if (line != null)
                    LaserUtility.SetLineImmediate(
                        line,
                        ctx.BossTransform.position,
                        ctx.PlayerTransform.position,
                        _laserColor);

                // DPS 판정: 매 프레임 CircleCast
                LaserUtility.CircleCastDPS(
                    origin, dir, _castRadius, _castDistance,
                    _playerLayer, _playerDPS);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }

    #endregion


    #region Phase 2 ── 레이저 난사 (Sequential)

    /// <summary>
    /// 경고 1.5초 → 0.2초 간격 연속 발사 (Lead Targeting 포함).
    ///
    /// ── Lead Targeting 수식 ─────────────────────────────────────────────
    ///   t    = distance(boss, player) / virtualLaserSpeed
    ///   pred = playerPos + playerVelocity × t × leadFactor
    ///
    ///   virtualLaserSpeed: 레이저가 즉발이지만 예측 강도를 제어하는 가상 속도.
    ///   leadFactor(0~1): 0 = 현재 위치, 1 = 완전 예측 (Inspector 조절).
    ///
    /// ── GC 최적화 ────────────────────────────────────────────────────────
    ///   경고 Color 구조체 재사용 (ref colorBuffer).
    ///   LaserUtility 정적 메서드로 매 발사마다 Instantiate 없음 (CircleCast 즉발).
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_RapidLaser",
                     menuName = "BossSystem/Patterns/Phase2_RapidLaser")]
    public class Phase2RapidLaserPattern : BossPatternBase
    {
        [Header("Rapid Laser")]
        [SerializeField] private float _warningDuration = 1.5f;
        [SerializeField] private float _fireInterval    = 0.2f;
        [SerializeField] private int   _burstCount      = 8;

        [Header("Lead Targeting")]
        [SerializeField, Range(0f, 1f)] private float _leadFactor          = 0.35f;
        [SerializeField]                private float _virtualLaserSpeed   = 50f;

        [Header("Damage")]
        [SerializeField] private float     _castRadius   = 0.15f;
        [SerializeField] private float     _castDistance = 30f;
        [SerializeField] private float     _laserDamage  = 20f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color        _warningColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private Color        _fireColor    = Color.red;

        private Color _colorBuffer;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                // ── 경고 페이드인 ────────────────────────────────────────
                Vector3 bossPos = ctx.BossTransform.position;

                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () =>bossPos,
                    () => GetLeadTarget(ctx),   // 예측 조준 방향으로 경고선 표시
                    _warningColor,
                    _warningDuration,
                    ct);

                ct.ThrowIfCancellationRequested();

                // ── 버스트 발사 루프 ─────────────────────────────────────
                for (int i = 0; i < _burstCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Vector3 predictedTarget = GetLeadTarget(ctx);
                    Vector2 origin          = ctx.BossTransform.position;
                    Vector2 dir             = ((Vector2)predictedTarget - origin).normalized;

                    // 발사 색상 전환
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(
                            warnLine,
                            ctx.BossTransform.position,
                            predictedTarget,
                            _fireColor);

                    // CircleCast 즉발 판정
                    LaserUtility.CircleCastAndDamage(
                        origin, dir, _castRadius, _castDistance,
                        _playerLayer, _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireInterval), cancellationToken: ct);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
            }
        }

        /// <summary>
        /// Lead Targeting: 플레이어 속도를 고려한 예측 좌표.
        ///
        /// 수식:
        ///   t    = dist(boss, player) / virtualLaserSpeed
        ///   pred = playerPos + velocity × t × leadFactor
        ///
        /// PlayerVelocity는 BossBlackboard.UpdatePlayerVelocity()가 매 프레임 갱신.
        /// leadFactor = 0이면 현재 위치 조준, 1이면 완전 예측 조준.
        /// </summary>
        private Vector3 GetLeadTarget(BossContext ctx)
        {
            Vector3 playerPos = ctx.PlayerTransform.position;
            Vector3 velocity  = ctx.Blackboard.PlayerVelocity;

            float dist = Vector3.Distance(ctx.BossTransform.position, playerPos);
            float t    = dist / Mathf.Max(_virtualLaserSpeed, 0.01f);

            return playerPos + velocity * t * _leadFactor;
        }
    }


    #endregion
    #endregion
}
