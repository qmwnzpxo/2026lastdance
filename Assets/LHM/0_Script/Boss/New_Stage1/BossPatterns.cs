using System.Threading;
using Boss.Phase1;
using Cysharp.Threading.Tasks;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
// BossPatterns.cs  —  전체 패턴 통합 (Phase1 + Phase2)
//
// ── 중복 제거 내역 ─────────────────────────────────────────────────────────
//   Before:
//     - Phase2_Patterns.cs: Phase2SideLaser / Phase2MirrorLaser /
//       Phase2RapidLaser 별도 파일에 완전 중복 구현
//     - BossPhase1Controller.cs 하단: Phase1PanelPattern 중복 정의
//     - 각 패턴마다 SpawnWarningLine / FireLaserAsync / DestroyAfterDelayAsync
//       로컬 메서드로 각각 복사
//   After:
//     - Phase2_Patterns.cs 삭제
//     - BossPhase1Controller.cs 하단 Phase1PanelPattern 삭제
//     - 모든 공통 로직 → LaserUtility 정적 메서드로 위임
//     - 이 파일이 모든 패턴의 단일 소스 (Single Source of Truth)
//
// ── 네임스페이스 ───────────────────────────────────────────────────────────
//   BossSystem.Patterns  단일 통일
//   (Before: Boss.Phase1 / BossSystem.Patterns 혼재)
// ═══════════════════════════════════════════════════════════════════════════

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 방어막 (Persistent)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 레이저 코어 4개가 모두 파괴될 때까지 보스를 무적으로 유지.
    ///
    /// ── CancellationToken 보장 ───────────────────────────────────────────
    ///   InvincibilityHandle을 using 스코프로 묶어 ct 취소 시에도
    ///   무적 스택 누수 없이 자동 Release.
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
            using var invHandle = ctx.DamageHandler.CreateInvincibilityHandle();

            GameObject vfx = null;
            if (_shieldVfxPrefab != null)
                vfx = Object.Instantiate(_shieldVfxPrefab,
                          ctx.BossTransform.position, Quaternion.identity,
                          ctx.BossTransform);

            // 코어 전부 파괴까지 폴링
            while (!ct.IsCancellationRequested)
            {
                if (ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
                {
                    Log("All cores destroyed. Shield deactivated.");
                    break;
                }
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_pollInterval), cancellationToken: ct);
            }

            if (vfx != null) Object.Destroy(vfx);
            // invHandle.Dispose() → ReleaseInvincibility 자동 호출
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 360도 레이저 (Persistent)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 4개 코어를 보스 중심으로 회전시키며 코어 간 레이저 빔으로 연결.
    ///
    /// ── Circular Link 재연결 알고리즘 ────────────────────────────────────
    ///   매 프레임 "살아있는 코어"만 _aliveBuffer에 수집 → 순환 연결.
    ///   정적 버퍼(_aliveBuffer)로 매 프레임 GC 없음.
    ///
    /// ── 1개 남은 경우 예외 처리 ──────────────────────────────────────────
    ///   aliveCount < 2이면 빔 비활성화.
    ///   aliveCount == 1이면 DestroyedCoreCount가 TotalCoreCount-1 → 
    ///   Phase1ShieldPattern 폴링이 자연히 종료 조건에 근접.
    ///   기획 연장 옵션: 마지막 코어를 _soloCoreSelfDestructDelay 초 후
    ///   TakeDamage(float.MaxValue)로 자폭시킬 수 있음 (아래 주석 참조).
    ///
    /// ── 재연결 Fade 연출 ─────────────────────────────────────────────────
    ///   코어 파괴 시 빔 개수가 변경되는데, 급격한 궤적 변화를 방지하기 위해
    ///   OnCoreDestroyed 콜백에서 0.2초 Fade 코루틴을 Forget으로 실행.
    ///
    /// ── 수학: 회전 위치 ───────────────────────────────────────────────────
    ///   각도_i = (globalAngle + i × 90°) × Deg2Rad
    ///   pos.x  = center.x + cos(rad) × radius
    ///   pos.y  = center.y + sin(rad) × radius
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase1_360Laser",
                     menuName = "BossSystem/Patterns/Phase1_360Laser")]
    public class Phase1LaserPattern : BossPatternBase
    {
        [Header("Laser Cores")]
        [SerializeField] private GameObject    _laserCorePrefab;
        [SerializeField] private LineRenderer  _laserBeamPrefab;
        [SerializeField] private float         _radius          = 5f;
        [SerializeField] private float         _rotationSpeed   = 30f;  // 도/초

        [Header("Damage")]
        [SerializeField] private float     _damagePerSecond = 20f;
        [SerializeField] private float     _laserThickness  = 0.5f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Reconnect Fade")]
        [Tooltip("코어 파괴 후 빔 재연결 페이드 시간 (초)")]
        [SerializeField, Range(0.05f, 0.5f)] private float _fadeReconnectDuration = 0.2f;

        // ── [수정] 자폭 옵션 ──────────────────────────────────────────────
        // 기획 의도에 따라 마지막 코어 혼자 남았을 때
        // 일정 시간 후 자폭시키고 싶다면 이 값을 > 0 으로 설정.
        // 0이면 자폭 없이 그냥 남아있는다.
        [Header("Last Core Behavior")]
        [Tooltip("코어 1개만 남았을 때 자폭까지 대기 (초). 0이면 자폭 없음.")]
        [SerializeField, Range(0f, 10f)] private float _soloCoreSelfDestructDelay = 3f;

        // GC-safe 정적 버퍼
        private static readonly int[] _aliveBuffer = new int[4];

        private static readonly Vector3[] _spawnDirs =
        {
            Vector3.up, Vector3.right, Vector3.down, Vector3.left
        };

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            var bossPos = ctx.BossTransform.position;
            var beams = new LineRenderer[4];
            // ── 코어 스폰 ─────────────────────────────────────────────────
            var cores = new LaserCore[4];
            for (int i = 0; i < 4; i++)
            {
                var go = ObjectPool.HasPool(_laserCorePrefab)
                    ? ObjectPool.Get(_laserCorePrefab, bossPos + _spawnDirs[i] * _radius, Quaternion.identity)
                    : Object.Instantiate(_laserCorePrefab, bossPos + _spawnDirs[i] * _radius, Quaternion.identity);

                cores[i] = go.GetComponent<LaserCore>();
                cores[i].Reset(); // 풀에서 꺼낸 경우 상태 초기화

                int capturedIdx = i;
                cores[capturedIdx].OnCoreDestroyed += (destroyedCore) =>
                {
                    ctx.DestroyedCoreCount++;
                    Log($"Core {capturedIdx} destroyed. Destroyed={ctx.DestroyedCoreCount}");
                    // Fade 재연결 연출 (Forget = 메인 루프 비차단)
                    FadeReconnectAsync(beams, capturedIdx, ct).Forget();
                };

                _blackboard_ref?.RegisterCore(cores[i]);
            }

            // ── 빔 스폰 ───────────────────────────────────────────────────
           
            for (int i = 0; i < 4; i++)
            {
                beams[i] = ObjectPool.HasPool(_laserBeamPrefab.gameObject)
                    ? ObjectPool.GetLineRenderer(_laserBeamPrefab.gameObject, Vector3.zero, Quaternion.identity)
                    : Object.Instantiate(_laserBeamPrefab);
                if (beams[i] != null) beams[i].enabled = false;
            }

            // ── 메인 루프 ─────────────────────────────────────────────────
            float angle = 0f;
            while (!ct.IsCancellationRequested)
            {
                angle += _rotationSpeed * Time.deltaTime;
                UpdateCorePositions(ctx.BossTransform.position, cores, angle);
                UpdateBeamsAndDamage(cores, beams, _laserThickness, _playerLayer, _damagePerSecond);

                // 마지막 코어 자폭 처리
                if (_soloCoreSelfDestructDelay > 0f)
                    CheckSoloCoreDestruct(cores, ctx, ct);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Cleanup(cores, beams);
        }

        // BossBlackboard 참조 (패턴 실행 전 외부에서 주입 or ctx.Blackboard 사용)
        private BossBlackboard _blackboard_ref;

        /// <summary>
        /// 외부에서 Blackboard를 주입할 때 사용.
        /// BossController가 ExecuteAsync 호출 전에 SetBlackboard를 호출.
        /// </summary>
        public void SetBlackboard(BossBlackboard bb) => _blackboard_ref = bb;

        // ─────────────────────────────────────────────────────────────────

        private void UpdateCorePositions(Vector3 center, LaserCore[] cores, float angle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i] == null || !cores[i].gameObject.activeInHierarchy) continue;
                float rad = (angle + i * 90f) * Mathf.Deg2Rad;
                cores[i].transform.position = new Vector3(
                    center.x + Mathf.Cos(rad) * _radius,
                    center.y + Mathf.Sin(rad) * _radius,
                    center.z);
            }
        }

        private static void UpdateBeamsAndDamage(
            LaserCore[] cores, LineRenderer[] beams,
            float thickness, LayerMask playerLayer, float dps)
        {
            // 살아있는 코어 인덱스 수집
            int aliveCount = 0;
            for (int i = 0; i < cores.Length; i++)
                if (cores[i] != null && cores[i].gameObject.activeInHierarchy)
                    _aliveBuffer[aliveCount++] = i;

            // 전체 빔 끄기
            for (int i = 0; i < beams.Length; i++)
                if (beams[i] != null) beams[i].enabled = false;

            // 2개 미만이면 빔 연결 불가
            if (aliveCount < 2) return;

            var castSize = new UnityEngine.Vector2(0.1f, thickness);
            float damage = dps * Time.deltaTime;

            for (int i = 0; i < aliveCount; i++)
            {
                int next  = (i + 1) % aliveCount;
                Vector3 from = cores[_aliveBuffer[i]].transform.position;
                Vector3 to   = cores[_aliveBuffer[next]].transform.position;
                Vector3 dir  = to - from;
                float   dist = dir.magnitude;
                if (dist < 0.01f) continue;

                // 시각 연결
                if (beams[i] != null)
                {
                    beams[i].enabled = true;
                    beams[i].SetPosition(0, from);
                    beams[i].SetPosition(1, to);
                }

                // 물리 판정 (FixedUpdate 타이밍 동기화를 위해 deltaTime 스케일링)
                var hit = Physics2D.BoxCast(
                    from, castSize,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                    dir.normalized, dist, playerLayer);

                //if (hit.collider != null)
                //    hit.collider.GetComponent<LaserBeamManager.IPlayerHealth>()?.TakeDamage(damage);
            }
        }

        /// <summary>
        /// 마지막 코어 1개만 남았을 때 일정 시간 후 자폭.
        /// 기획 의도: 코어 1개 남은 상태에서 플레이어가 장시간 버티는 것을 방지.
        /// </summary>
        private void CheckSoloCoreDestruct(LaserCore[] cores, BossContext ctx, CancellationToken ct)
        {
            int aliveCount = 0;
            LaserCore lastCore = null;
            foreach (var c in cores)
            {
                if (c != null && c.gameObject.activeInHierarchy)
                {
                    aliveCount++;
                    lastCore = c;
                }
            }

            if (aliveCount == 1 && lastCore != null && _soloCoreSelfDestructDelay > 0f)
            {
                SoloCoreSelfDestructAsync(lastCore, _soloCoreSelfDestructDelay, ct).Forget();
                _soloCoreSelfDestructDelay = 0f; // 한 번만 실행
            }
        }

        private static async UniTaskVoid SoloCoreSelfDestructAsync(
            LaserCore core, float delay, CancellationToken ct)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: ct)
                         .SuppressCancellationThrow();
            if (core != null && core.gameObject.activeInHierarchy)
                core.TakeDamage(float.MaxValue);
        }

        /// <summary>
        /// 코어 파괴 시 해당 빔을 0.2초 Fade-out 후 비활성화.
        /// 급격한 궤적 변화 방지 (UX 기획 요구사항).
        /// </summary>
        private async UniTaskVoid FadeReconnectAsync(
            LineRenderer[] beams, int destroyedIdx, CancellationToken ct)
        {
            if (destroyedIdx < 0 || destroyedIdx >= beams.Length || beams[destroyedIdx] == null)
                return;

            var lr       = beams[destroyedIdx];
            float elapsed = 0f;
            var startColor = lr.startColor;

            while (elapsed < _fadeReconnectDuration && !ct.IsCancellationRequested)
            {
                float t = elapsed / _fadeReconnectDuration;
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                lr.startColor = lr.endColor = c;
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (lr != null) lr.enabled = false;
        }

        private static void Cleanup(LaserCore[] cores, LineRenderer[] beams)
        {
            foreach (var c in cores)
                if (c != null)
                {
                    if (ObjectPool.HasPool(c.gameObject)) ObjectPool.Return(c.gameObject);
                    else Object.Destroy(c.gameObject);
                }

            foreach (var b in beams)
                if (b != null)
                {
                    if (ObjectPool.HasPool(b.gameObject)) ObjectPool.Return(b.gameObject);
                    else Object.Destroy(b.gameObject);
                }
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 판넬 조준 사격 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 판넬이 waypoint를 순환 이동 → 조준 경고 → 발사.
    ///
    /// ── 흐름 (1 사이클) ─────────────────────────────────────────────────
    ///   for each waypoint:
    ///     MoveTowards → FadeInWarningLine (추적) → 위치 고정 0.5초 → 발사
    ///
    /// ── 기획 상세 (전조 연출) ────────────────────────────────────────────
    ///   - 이동 후 1.5초간 플레이어 추적 경고선 (Lock-on)
    ///   - 마지막 0.5초는 경고선 고정 (위치 확정 신호)
    ///   - 발사 후 _postFireDelay 동안 딜 타임 제공
    ///
    /// ── 이전 코드와의 차이 ──────────────────────────────────────────────
    ///   Before: BossPhase1Controller.cs 하단에 중복으로 정의됨
    ///   After:  LaserUtility 위임으로 로직 단일화, Lock-on 분리 추가
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase1_Panel",
                     menuName = "BossSystem/Patterns/Phase1_Panel")]
    public class Phase1PanelPattern : BossPatternBase
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelPrefab;
        public Transform[] _waypoints;
        [SerializeField] private float _moveSpeed        = 5f;

        [Header("Telegraphing (전조 연출)")]
        [Tooltip("경고선 Lock-on 지속 시간 (초). 플레이어를 추적함.")]
        [SerializeField] private float _lockOnDuration   = 1.5f;
        [Tooltip("마지막 위치 고정 후 발사까지 대기 (초). 플레이어 회피 힌트.")]
        [SerializeField] private float _lockFixedDuration= 0.5f;
        [SerializeField] private float _fireDuration     = 0.3f;
        [Tooltip("발사 후 딜 타임 (초). 공격 패턴 보상 시간.")]
        [SerializeField] private float _postFireDelay    = 0.7f;

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
        public void SetPoints(Transform[] transforms)
        {
            Debug.Log("setpo");
            _waypoints = transforms;
        }
        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            if (_waypoints == null || _waypoints.Length == 0 || _waypoints[0]==null)
            {
                Debug.LogWarning("[PanelPattern] No waypoints.");
                return;
            }

            var panelGo = Object.Instantiate(_panelPrefab,
                              ctx.BossTransform.position, Quaternion.identity);
            var panelTf = panelGo.transform;

            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                for (int i = 0; i < _waypoints.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    // 1. 이동
                    await LaserUtility.MoveTowardsAsync(
                        panelTf, _waypoints[i].position, _moveSpeed, ct);

                    // 2. Lock-on 경고: 플레이어 위치 추적 (전조 시작)
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panelTf.position,
                        () => ctx.PlayerTransform.position,
                        _warningColor, _lockOnDuration, ct);

                    ct.ThrowIfCancellationRequested();

                    // 3. 위치 고정 (발사 전 힌트: "이제 피해야 해")
                    Vector3 lockedTarget = ctx.PlayerTransform.position;
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _warningColor);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_lockFixedDuration), cancellationToken: ct);

                    ct.ThrowIfCancellationRequested();

                    // 4. 발사
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _fireColor);

                    if (_muzzleVfxPrefab != null)
                        LaserUtility.DestroyAfterDelayAsync(
                            Object.Instantiate(_muzzleVfxPrefab, panelTf.position, panelTf.rotation),
                            _fireDuration, ct).Forget();

                    LaserUtility.CircleCastAndDamage(
                        panelTf.position, panelTf.right,
                        _castRadius, _castDistance, _playerLayer, _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);
                    LaserUtility.HideLine(warnLine);

                    // 5. 딜 타임 (보상 시간)
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_postFireDelay), cancellationToken: ct);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
                if (panelGo  != null) Object.Destroy(panelGo);
            }
        }
       

    }

    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 좌우 스윕 레이저 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 맵 외곽 세 방향(좌→위→우)에서 순차 레이저를 발사.
    ///
    /// ── 기존 Phase2_Patterns.cs에서 완전 흡수 ────────────────────────────
    ///   LaserUtility.BoxCastAndDamage + FadeInWarningLineAsync 위임.
    ///   경고선 인스턴스를 3방향 루프에서 재사용 (GC 최소화).
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_SideLaser",
                     menuName = "BossSystem/Patterns/Phase2_SideLaser")]
    public class Phase2SideLaserPattern : BossPatternBase
    {
        [Header("Side Laser")]
        [SerializeField] private float _warningDuration = 3f;
        [SerializeField] private float _laserDuration   = 0.4f;
        [SerializeField] private float _mapEdgeOffset   = 20f;

        [Header("Damage")]
        [SerializeField] private float     _laserThickness = 0.5f;
        [SerializeField] private float     _castDistance   = 40f;
        [SerializeField] private float     _laserDamage    = 35f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color        _warningColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color        _fireColor    = Color.red;

        // (origin 오프셋, 발사 방향)
        private static readonly (UnityEngine.Vector2 offset, UnityEngine.Vector2 dir)[] _sweepData =
        {
            (new UnityEngine.Vector2(-1f, 0f), UnityEngine.Vector2.right),
            (new UnityEngine.Vector2(0f,  1f), UnityEngine.Vector2.down),
            (new UnityEngine.Vector2(1f,  0f), UnityEngine.Vector2.left),
        };

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                foreach (var (offset, dir) in _sweepData)
                {
                    ct.ThrowIfCancellationRequested();

                    UnityEngine.Vector2 playerPos2D = ctx.PlayerTransform.position;
                    UnityEngine.Vector2 origin      = playerPos2D + offset * _mapEdgeOffset;
                    UnityEngine.Vector2 endPos      = origin + dir * _castDistance;

                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine, () => (Vector3)(Vector2)origin, () => (Vector3)(Vector2)endPos,
                        _warningColor, _warningDuration, ct);

                    ct.ThrowIfCancellationRequested();

                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, origin, endPos, _fireColor);

                    LaserUtility.BoxCastAndDamage(
                        origin, dir, _laserThickness, _castDistance, _playerLayer, _laserDamage);

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

    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 거울 반사 레이저 기믹 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 보스 레이저 → 거울 반사 → 보스 피격 파훼 기믹.
    ///
    /// ── UniTaskCompletionSource 브리지 ──────────────────────────────────
    ///   C# 이벤트(OnReflectedToBoss)를 UniTask 흐름에 연결.
    ///   WhenAny(laserLoop, reflectTcs, timeout)로 먼저 오는 것에 반응.
    ///
    /// ── Mirror 회전 경합 방지 ────────────────────────────────────────────
    ///   Mirror.SnapRotate()는 동기 메서드이므로 경합 없음.
    ///   단, Mirror.StartReflecting() / NotifyReflectedToBoss()는
    ///   _isReflecting bool 플래그로 중복 이벤트 방지.
    ///
    /// ── 반사 판정 수학 (Mirror.cs) ──────────────────────────────────────
    ///   r = Vector2.Reflect(incident, normal) = d - 2(d·n)n
    ///   파훼 조건: Vector2.Angle(r, toBoss) ≤ angleTolerance
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_MirrorLaser",
                     menuName = "BossSystem/Patterns/Phase2_MirrorLaser")]
    public class Phase2MirrorLaserPattern : BossPatternBase
    {
        [Header("Mirror")]
        [SerializeField] private GameObject _mirrorPrefab;
        [SerializeField] private Transform  _mirrorSpawnPoint;
        [SerializeField] private float      _chargeTime       = 2f;
        [SerializeField] private float      _maxLaserDuration = 12f;

        [Header("Damage")]
        [SerializeField] private float     _playerDPS      = 25f;
        [SerializeField] private float     _reflectDamage  = 50f;
        [SerializeField] private float     _castRadius     = 0.15f;
        [SerializeField] private float     _castDistance   = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _laserLinePrefab;
        [SerializeField] private Color        _laserColor = Color.red;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            Vector3 spawnPos = _mirrorSpawnPoint != null
                ? _mirrorSpawnPoint.position
                : ctx.BossTransform.position + Vector3.down * 6f;

            var mirrorGo = Object.Instantiate(_mirrorPrefab, spawnPos, Quaternion.identity);
            var mirror   = mirrorGo.GetComponent<Mirror>();

            if (mirror == null)
            {
                Debug.LogError("[MirrorLaser] Mirror component missing!");
                Object.Destroy(mirrorGo);
                return;
            }

            mirror.Initialize(ctx.BossTransform);

            // C# 이벤트 → UniTask 브리지
            var reflectTcs = new UniTaskCompletionSource();
            mirror.OnReflectedToBoss += () => reflectTcs.TrySetResult();

            LineRenderer laserLine = _laserLinePrefab != null
                ? Object.Instantiate(_laserLinePrefab) : null;

            try
            {
                // 충전 대기 (전조 연출)
                Log($"Charging laser... ({_chargeTime}s)");
                await UniTask.Delay(System.TimeSpan.FromSeconds(_chargeTime), cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                mirror.StartReflecting();

                // WhenAny: 파훼 성공 | 타임아웃 | ct 취소 중 먼저 오는 것
                await UniTask.WhenAny(
                    RunLaserLoopAsync(ctx, laserLine, ct),
                    reflectTcs.Task.AttachExternalCancellation(ct),
                    UniTask.Delay(System.TimeSpan.FromSeconds(_maxLaserDuration), cancellationToken: ct));

                // 파훼 성공 시 보스 데미지 (무적 없는 2페이즈)
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
        /// ct 취소 시 자동 종료.
        /// </summary>
        private async UniTask RunLaserLoopAsync(
            BossContext ctx, LineRenderer line, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UnityEngine.Vector2 origin = ctx.BossTransform.position;
                UnityEngine.Vector2 dir    = ((UnityEngine.Vector2)ctx.PlayerTransform.position - origin).normalized;

                if (line != null)
                    LaserUtility.SetLineImmediate(
                        line,
                        ctx.BossTransform.position,
                        ctx.PlayerTransform.position,
                        _laserColor);

                LaserUtility.CircleCastDPS(origin, dir, _castRadius, _castDistance,
                                           _playerLayer, _playerDPS);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 레이저 난사 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 경고 1.5초 → 0.2초 간격 연속 발사 (Lead Targeting 포함).
    ///
    /// ── Lead Targeting 수식 ─────────────────────────────────────────────
    ///   t    = distance(boss, player) / virtualLaserSpeed
    ///   pred = playerPos + playerVelocity × t × leadFactor
    ///   BossBlackboard.PlayerVelocity: BossController.Update에서 매 프레임 갱신.
    ///   leadFactor 0 = 현재 위치, 1 = 완전 예측 (Inspector 조절).
    ///
    /// ── 기존 Phase2_Patterns.cs와의 차이 ────────────────────────────────
    ///   Before: DestroyAfterDelayAsync 로컬 중복, ObjectPool 미사용
    ///   After:  LaserUtility.DestroyAfterDelayAsync 위임
    ///           레이저 VFX → ObjectPool.Get/Return
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
        [SerializeField, Range(0f, 1f)] private float _leadFactor        = 0.35f;
        [SerializeField]                private float _virtualLaserSpeed = 50f;

        [Header("Damage")]
        [SerializeField] private float     _castRadius   = 0.15f;
        [SerializeField] private float     _castDistance = 30f;
        [SerializeField] private float     _laserDamage  = 20f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color        _warningColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private Color        _fireColor    = Color.red;
        [SerializeField] private GameObject   _laserVfxPrefab;
        [SerializeField] private float        _laserVfxLifetime = 0.15f;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                // 경고 페이드인 (Lead Targeting 방향 표시)
                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () => ctx.BossTransform.position,
                    () => GetLeadTarget(ctx),
                    _warningColor, _warningDuration, ct);

                ct.ThrowIfCancellationRequested();

                // 버스트 발사 루프
                for (int i = 0; i < _burstCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Vector3 predicted = GetLeadTarget(ctx);
                    UnityEngine.Vector2 origin = ctx.BossTransform.position;
                    UnityEngine.Vector2 dir    = ((UnityEngine.Vector2)predicted - origin).normalized;

                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, ctx.BossTransform.position,
                            predicted, _fireColor);

                    // 즉발 판정
                    LaserUtility.CircleCastAndDamage(origin, dir, _castRadius,
                        _castDistance, _playerLayer, _laserDamage);

                    // VFX (ObjectPool 사용)
                    if (_laserVfxPrefab != null)
                    {
                        var laser = ObjectPool.HasPool(_laserVfxPrefab)
                            ? ObjectPool.Get(_laserVfxPrefab, ctx.BossTransform.position,
                                  Quaternion.LookRotation(Vector3.forward, dir))
                            : Object.Instantiate(_laserVfxPrefab, ctx.BossTransform.position,
                                  Quaternion.LookRotation(Vector3.forward, dir));

                        LaserUtility.DestroyAfterDelayAsync(laser, _laserVfxLifetime, ct).Forget();
                    }

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
        ///   pred = playerPos + velocity × (dist / virtualLaserSpeed) × leadFactor
        /// </summary>
        private Vector3 GetLeadTarget(BossContext ctx)
        {
            Vector3 playerPos = ctx.PlayerTransform.position;
            Vector3 velocity  = ctx.Blackboard.PlayerVelocity;
            float   dist      = Vector3.Distance(ctx.BossTransform.position, playerPos);
            float   t         = dist / Mathf.Max(_virtualLaserSpeed, 0.01f);
            return playerPos + velocity * t * _leadFactor;
        }
    }
}
