using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using BossSystem;

// ═══════════════════════════════════════════════════════════════════════
// 1페이즈 패턴 3: 판넬 조준 사격 (Sequential)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// 판넬이 보스 위치에서 소환되어 지정된 위치로 이동 후 플레이어를 조준 발사하는 Sequential 패턴.
///
/// 기획 상세:
///   1. 보스 위치에 판넬 소환
///   2. panelTransforms[i] 위치로 이동
///   3. 플레이어 방향으로 LineRenderer 경고 표시 (2초)
///   4. 레이저 발사
///   5. panelTransforms[i+1]로 재이동 후 반복
///
/// Sequential 패턴이므로 한 사이클 완료 후 BossController의
/// RunSequentialPatternsAsync 루프에 의해 다음 패턴으로 넘어간다.
/// </summary>
[CreateAssetMenu(fileName = "Pattern_Phase1_Panel", menuName = "BossSystem/Patterns/Phase1_Panel")]
public class Phase1PanelPattern : BossPatternBase
{
    [Header("Panel Settings")]
    [SerializeField] private GameObject _panelPrefab;
    [Tooltip("판넬이 순서대로 이동할 위치 트랜스폼 배열")]
    [SerializeField] private Transform[] _panelTransforms;
    [Tooltip("판넬 이동 속도 (유닛/초)")]
    [SerializeField] private float _moveSpeed = 5f;
    [Tooltip("조준 경고 지속 시간 (초)")]
    [SerializeField] private float _warningDuration = 2f;
    [Tooltip("레이저 발사 지속 시간 (초)")]
    [SerializeField] private float _fireDuration = 0.3f;
    [Tooltip("발사 후 다음 위치로 이동 전 대기 (초)")]
    [SerializeField] private float _postFireDelay = 0.5f;

    [Header("VFX")]
    [SerializeField] private LineRenderer _warningLinePrefab;
    [SerializeField] private GameObject _laserVfxPrefab;

    protected override async UniTask OnExecuteAsync(BossContext context, CancellationToken ct)
    {
        if (_panelTransforms == null || _panelTransforms.Length == 0)
        {
            Debug.LogWarning("[PanelPattern] No panel transforms assigned.");
            return;
        }

        // 판넬 소환 (보스 위치)
        var panelGo = Instantiate(_panelPrefab, context.BossTransform.position, Quaternion.identity);


        try
        {
            // 각 위치를 순환하며 조준 + 발사
            for (int i = 0; i < _panelTransforms.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var targetPos = _panelTransforms[i].position;

                // ── 1. 지정 위치로 이동 ──────────────────────────────
                await MovePanelAsync(panelGo.transform, targetPos, ct);

                // ── 2. 플레이어 방향으로 경고 LineRenderer 표시 ──────
                var warningLine = SpawnWarningLine(
                    panelGo.transform.position,
                    context.PlayerTransform.position
                );

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_warningDuration),
                    cancellationToken: ct
                );

                if (warningLine != null) Destroy(warningLine.gameObject);

                // ── 3. 레이저 발사 ───────────────────────────────────
                await FireLaserAsync(
                    panelGo.transform.position,
                    context.PlayerTransform.position,
                    ct
                );

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_postFireDelay),
                    cancellationToken: ct
                );
            }
        }
        finally
        {
            // CancellationToken 취소 시에도 판넬 오브젝트 반드시 정리
            if (panelGo != null) Destroy(panelGo);
        }
    }

    /// <summary>
    /// MoveTowards 방식으로 목표 위치까지 이동. 매 프레임 GC 없이 Vector3 연산.
    /// </summary>
    private async UniTask MovePanelAsync(Transform panel, Vector3 target, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            panel.position = Vector3.MoveTowards(panel.position, target, _moveSpeed * Time.deltaTime);
            if (Vector3.SqrMagnitude(panel.position - target) < 0.01f)
            {
                panel.position = target;
                return;
            }
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }

    private LineRenderer SpawnWarningLine(Vector3 from, Vector3 to)
    {
        if (_warningLinePrefab == null) return null;
        var lr = Instantiate(_warningLinePrefab);
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        return lr;
    }

    private async UniTask FireLaserAsync(Vector3 from, Vector3 to, CancellationToken ct)
    {
        if (_laserVfxPrefab == null) return;
        var laser = Instantiate(_laserVfxPrefab, from, Quaternion.LookRotation(to - from));
        await UniTask.Delay(System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);
        if (laser != null) Destroy(laser);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 2페이즈 패턴 1: 좌우 레이저 (Sequential)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// 맵 밖에서 좌 → 위 → 우 순서로 3번 반복 발사하는 Sequential 패턴.
///
/// 기획 상세:
///   1. 플레이어 좌표 수신
///   2. 맵 밖에서 LineRenderer로 3초 전 경고
///   3. 레이저 발사
///   4. 좌 → 위 → 오른쪽 기준 3번 반복
/// </summary>
[CreateAssetMenu(fileName = "Pattern_Phase2_SideLaser", menuName = "BossSystem/Patterns/Phase2_SideLaser")]
public class Phase2SideLaserPattern : BossPatternBase
{
    [Header("Side Laser Settings")]
    [SerializeField] private float _warningDuration = 3f;
    [SerializeField] private float _laserDuration = 0.5f;
    [SerializeField] private float _mapEdgeOffset = 20f; // 맵 밖 소환 오프셋

    [Header("VFX")]
    [SerializeField] private LineRenderer _warningLinePrefab;
    [SerializeField] private GameObject _laserVfxPrefab;

    // 발사 방향 순서: 좌 → 위 → 우
    private static readonly Vector3[] _fireDirections = new Vector3[]
    {
        Vector3.left, Vector3.up, Vector3.right
    };

    protected override async UniTask OnExecuteAsync(BossContext context, CancellationToken ct)
    {
        var playerPos = context.PlayerTransform.position;

        foreach (var direction in _fireDirections)
        {
            ct.ThrowIfCancellationRequested();

            // 맵 밖 출발점 계산 (플레이어 Y 좌표 기준)
            var startPos = new Vector3(
                playerPos.x + direction.x * _mapEdgeOffset,
                playerPos.y + direction.y * _mapEdgeOffset,
                playerPos.z
            );

            // ── 경고 LineRenderer ────────────────────────────────────
            var warningLine = SpawnWarningLine(startPos, playerPos);
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_warningDuration),
                cancellationToken: ct
            );
            if (warningLine != null) Destroy(warningLine.gameObject);

            // ── 레이저 발사 ──────────────────────────────────────────
            await FireLaserAsync(startPos, -direction, ct);
        }
    }

    private LineRenderer SpawnWarningLine(Vector3 from, Vector3 to)
    {
        if (_warningLinePrefab == null) return null;
        var lr = Instantiate(_warningLinePrefab);
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        return lr;
    }

    private async UniTask FireLaserAsync(Vector3 from, Vector3 direction, CancellationToken ct)
    {
        if (_laserVfxPrefab == null) return;
        var laser = Instantiate(_laserVfxPrefab, from, Quaternion.LookRotation(direction));
        await UniTask.Delay(System.TimeSpan.FromSeconds(_laserDuration), cancellationToken: ct);
        if (laser != null) Destroy(laser);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 2페이즈 패턴 2: 거울 반사 레이저 기믹 (Sequential)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// 보스가 플레이어 방향으로 레이저를 발사하면, 플레이어가 거울을 조작해
/// 반사된 레이저를 보스에게 맞혀야 하는 파훼 기믹 패턴.
///
/// 기획 상세:
///   1. 맵 하단 가운데에 거울 소환
///   2. 2초 준비 후 플레이어 방향으로 레이저 지속 발사
///   3. 플레이어는 거울을 공격해 각도 조정 → 반사 레이저가 보스를 맞추면 파훼
///
/// 파훼 조건 감지는 Mirror 컴포넌트가 담당하며,
/// 패턴은 OnDefeated 콜백 또는 UniTask.WaitUntil로 폴링.
/// </summary>
[CreateAssetMenu(fileName = "Pattern_Phase2_MirrorLaser", menuName = "BossSystem/Patterns/Phase2_MirrorLaser")]
public class Phase2MirrorLaserPattern : BossPatternBase
{
    [Header("Mirror Settings")]
    [SerializeField] private GameObject _mirrorPrefab;
    [SerializeField] private Transform _mirrorSpawnPoint; // 맵 하단 가운데
    [Tooltip("레이저 발사 전 준비 시간 (초)")]
    [SerializeField] private float _chargeTime = 2f;
    [Tooltip("레이저 지속 최대 시간 (초) — 파훼 못하면 이 시간 후 패턴 종료")]
    [SerializeField] private float _maxLaserDuration = 10f;
    [Tooltip("반사 레이저 판정 폴링 간격 (초)")]
    [SerializeField] private float _reflectCheckInterval = 0.1f;

    [Header("VFX")]
    [SerializeField] private GameObject _laserVfxPrefab;

    protected override async UniTask OnExecuteAsync(BossContext context, CancellationToken ct)
    {
        // ── 거울 소환 ────────────────────────────────────────────────
        var mirrorPos = _mirrorSpawnPoint != null
            ? _mirrorSpawnPoint.position
            : context.BossTransform.position + Vector3.down * 5f;

        var mirrorGo = Instantiate(_mirrorPrefab, mirrorPos, Quaternion.identity);
        var mirror = mirrorGo.GetComponent<Mirror>();

        // 보스에게 데미지를 줄 수 있는 로컬 플래그
        bool mirrorDefeated = false;
        if (mirror != null)
            mirror.OnReflectedToBoss += () => { mirrorDefeated = true; };

        try
        {
            // ── 충전 대기 ────────────────────────────────────────────
            Debug.Log("[MirrorLaserPattern] Charging laser...");
            await UniTask.Delay(System.TimeSpan.FromSeconds(_chargeTime), cancellationToken: ct);

            // ── 레이저 발사 시작 ─────────────────────────────────────
            var laserDir = (context.PlayerTransform.position - context.BossTransform.position).normalized;
            var laserGo = _laserVfxPrefab != null
                ? Instantiate(_laserVfxPrefab, context.BossTransform.position, Quaternion.LookRotation(laserDir))
                : null;

            // ── 반사 파훼 또는 타임아웃 대기 ─────────────────────────
            float elapsed = 0f;
            while (!ct.IsCancellationRequested && elapsed < _maxLaserDuration)
            {
                if (mirrorDefeated)
                {
                    Debug.Log("[MirrorLaserPattern] Reflected! Boss takes damage.");
                    // 거울 반사에 의한 보스 데미지 처리
                    // 무적 해제 후 데미지 적용 (기획: 반사 레이저는 무적 무시 or
                    // 방어막 이미 없는 상태 — 2페이즈이므로 무적 없음)
                    context.DamageHandler.TakeDamage(50f); // 예시 데미지 값
                    break;
                }
                elapsed += _reflectCheckInterval;
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_reflectCheckInterval),
                    cancellationToken: ct
                );
            }

            if (laserGo != null) Destroy(laserGo);
        }
        finally
        {
            if (mirrorGo != null) Destroy(mirrorGo);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 2페이즈 패턴 3: 순차 레이저 난사 (Sequential)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// 0.2초 간격으로 플레이어를 향해 레이저를 연속 발사하는 Sequential 패턴.
///
/// 기획 상세:
///   1. 라인랜더러 경고 줄 표시 (1.5초)
///   2. 0.2초 간격 레이저 연속 발사
///   3. 일정 발사 횟수 후 패턴 완료 → Sequential 루프에서 다음 패턴으로
/// </summary>
[CreateAssetMenu(fileName = "Pattern_Phase2_RapidLaser", menuName = "BossSystem/Patterns/Phase2_RapidLaser")]
public class Phase2RapidLaserPattern : BossPatternBase
{
    [Header("Rapid Laser Settings")]
    [SerializeField] private float _warningDuration = 1.5f;
    [Tooltip("레이저 발사 간격 (초)")]
    [SerializeField] private float _fireInterval = 0.2f;
    [Tooltip("한 사이클의 총 발사 횟수")]
    [SerializeField] private int _burstCount = 8;

    [Header("VFX")]
    [SerializeField] private LineRenderer _warningLinePrefab;
    [SerializeField] private GameObject _laserVfxPrefab;
    [SerializeField] private float _laserLifetime = 0.15f;

    protected override async UniTask OnExecuteAsync(BossContext context, CancellationToken ct)
    {
        // ── 경고 표시 ────────────────────────────────────────────────
        var playerPos = context.PlayerTransform.position;
        var bossPos = context.BossTransform.position;

        var warningLine = SpawnWarningLine(bossPos, playerPos);
        await UniTask.Delay(System.TimeSpan.FromSeconds(_warningDuration), cancellationToken: ct);
        if (warningLine != null) Destroy(warningLine.gameObject);

        // ── 연속 발사 루프 ───────────────────────────────────────────
        for (int i = 0; i < _burstCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            // 발사 시점의 플레이어 위치로 재계산 (추적 사격)
            var currentPlayerPos = context.PlayerTransform.position;
            var dir = (currentPlayerPos - context.BossTransform.position).normalized;

            if (_laserVfxPrefab != null)
            {
                var laser = Instantiate(
                    _laserVfxPrefab,
                    context.BossTransform.position,
                    Quaternion.LookRotation(dir)
                );
                // 레이저 자동 파괴를 UniTask로 관리 (Forget: 메인 루프 블로킹 없이 병렬 처리)
                DestroyAfterDelayAsync(laser, _laserLifetime, ct).Forget();
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(_fireInterval), cancellationToken: ct);
        }
    }

    private LineRenderer SpawnWarningLine(Vector3 from, Vector3 to)
    {
        if (_warningLinePrefab == null) return null;
        var lr = Instantiate(_warningLinePrefab);
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        return lr;
    }

    /// <summary>
    /// 레이저 오브젝트를 일정 시간 후 파괴하는 Fire-and-Forget UniTask.
    /// Forget()으로 호출하되, CancellationToken으로 보스 파괴 시 정리 보장.
    /// </summary>
    private static async UniTaskVoid DestroyAfterDelayAsync(
        GameObject go,
        float delay,
        CancellationToken ct)
    {
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(delay),
            cancellationToken: ct
        ).SuppressCancellationThrow();

        if (go != null) Destroy(go);
    }
}

