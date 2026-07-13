using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// Phase 2 보스 미사일 난사 패턴 (UniTask 기반 비동기 흐름)
/// ─────────────────────────────────────────────────────────────
/// 시퀀스 (totalCycles 횟수 반복):
///   1. 보스 투명화 (SpriteRenderer / Collider 비활성)
///   2. 스폰 포인트 3개 중 랜덤 텔레포트 (직전 위치 제외)
///   3. 가시화 + 차지 애니메이션
///   4. 1.0초 시각 전보
///      ├─ 반투명 경고 박스 (화면 전체 가로 범위, 보스 Y 위치)
///      └─ 스파크가 보스 위치 → 화면 끝으로 EaseOutQuad 스윕
///   5. 미사일 발사
///   6. 다음 사이클
/// ─────────────────────────────────────────────────────────────
/// [기존 시스템 통합]
///   AttackPattern 열거형(BossController.cs)에 추가:
///     Phase2MissileBarrage
///
///   BossAttackPatterns.cs에 필드 추가:
///     [SerializeField] private Phase2BossMissilePattern missilePattern;
///
///   ExecutePattern() switch에 케이스 추가:
///     case AttackPattern.Phase2MissileBarrage:
///         yield return StartCoroutine(missilePattern.ExecuteAsCoroutine()); break;
///
///   BuildBehaviorTrees()의 Phase2 BT 원거리 콤보 또는 별도 콤보에 추가.
/// </summary>
public class Phase2BossMissilePattern : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    //  Inspector – 스폰 포인트
    // ═══════════════════════════════════════════════════════

    [Header("━━ 스폰 포인트 (아래→위 순서, 3개) ━━")]
    [Tooltip("보스가 텔레포트할 빈 GameObject 3개 (1=하단, 2=중단, 3=상단 순서로 할당).")]
    [SerializeField] private Transform[] spawnPoints = new Transform[3];

    // ═══════════════════════════════════════════════════════
    //  Inspector – 보스 컴포넌트 참조
    // ═══════════════════════════════════════════════════════

    [Header("━━ 보스 컴포넌트 참조 ━━")]
    [Tooltip("투명화 시 toggle할 보스의 모든 SpriteRenderer.")]
    [SerializeField] private SpriteRenderer[] bossSprites;
    [Tooltip("투명화 시 toggle할 보스의 모든 Collider2D.")]
    [SerializeField] private Collider2D[] bossColliders;
    [SerializeField] private Animator       bossAnimator;
    [SerializeField] private BossController bossController;
    [SerializeField] private Transform      player;

    // ═══════════════════════════════════════════════════════
    //  Inspector – 미사일 설정
    // ═══════════════════════════════════════════════════════

    [Header("━━ 미사일 설정 ━━")]
    [Tooltip("Phase2Missile + Rigidbody2D + BoxCollider2D(isTrigger) 가 있는 프리팹.")]
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private float missileSpeed  = 28f;
    [SerializeField] private float missileDamage = 25f;
    [Tooltip("미사일 판정 세로 폭 (경고 박스 높이 기준에도 사용됨).")]
    [SerializeField] private float missileHeight = 1.4f;
    [SerializeField] private int   missilePoolSize = 6;

    // ═══════════════════════════════════════════════════════
    //  Inspector – 경고 박스 설정
    // ═══════════════════════════════════════════════════════

    [Header("━━ 경고 박스 설정 ━━")]
    [Tooltip("SpriteRenderer가 있는 흰색 Quad 프리팹 (런타임에 색상/크기 지정됨).")]
    [SerializeField] private GameObject warningBoxPrefab;
    [SerializeField] private Color      warningColor = new Color(1f, 0.12f, 0f, 0.30f);
    [SerializeField] private int        warningBoxPoolSize = 3;

    // ═══════════════════════════════════════════════════════
    //  Inspector – 스윕 스파크 설정
    // ═══════════════════════════════════════════════════════

    [Header("━━ 스윕 스파크 설정 ━━")]
    [Tooltip("보스 위치 → 화면 끝으로 이동하는 레이저/스파크 SpriteRenderer 프리팹.")]
    [SerializeField] private GameObject sparkPrefab;
    [SerializeField] private Color      sparkColor = new Color(1f, 0.6f, 0f, 1f);
    [SerializeField] private int        sparkPoolSize = 3;

    // ═══════════════════════════════════════════════════════
    //  Inspector – 아레나 경계
    // ═══════════════════════════════════════════════════════

    [Header("━━ 아레나 경계 ━━")]
    [Tooltip("화면 왼쪽 끝 X 좌표.")]
    [SerializeField] private float arenaLeftX  = -12f;
    [Tooltip("화면 오른쪽 끝 X 좌표.")]
    [SerializeField] private float arenaRightX =  12f;

    // ═══════════════════════════════════════════════════════
    //  Inspector – 타이밍
    // ═══════════════════════════════════════════════════════

    [Header("━━ 타이밍 ━━")]
    [Tooltip("사이클 간 투명 상태 유지 시간 (초).")]
    [SerializeField] private float invisibilityDuration = 0.25f;
    [Tooltip("시각 전보(스윕) 지속 시간 (초). 이 시간 이후 정확히 미사일 발사.")]
    [SerializeField] private float telegraphDuration    = 1.0f;
    [Tooltip("미사일 발사 후 다음 사이클까지 대기 시간.")]
    [SerializeField] private float betweenCyclePause    = 0.4f;
    [Tooltip("전체 반복 횟수 (기본값 = 3).")]
    [SerializeField] private int   totalCycles          = 3;

    [Header("━━ 귀환 설정 ━━")]
    [Tooltip("패턴 종료 후 보스가 이동할 중앙 위치 (빈 GameObject).")]
    [SerializeField] private Transform returnPoint;
    [Tooltip("귀환 이동 속도 (units/sec). 0 이하면 즉시 이동.")]
    [SerializeField] private float returnSpeed = 6f;

    [Header("━━ 충돌 레이어 ━━")]
    [SerializeField] private LayerMask playerLayer;


    // ═══════════════════════════════════════════════════════
    //  Private – 오브젝트 풀
    // ═══════════════════════════════════════════════════════

    private readonly Queue<GameObject> _missilePool = new Queue<GameObject>();
    private readonly Queue<GameObject> _warningPool = new Queue<GameObject>();
    private readonly Queue<GameObject> _sparkPool   = new Queue<GameObject>();

    // ═══════════════════════════════════════════════════════
    //  Private – 런타임 상태
    // ═══════════════════════════════════════════════════════

    private int   _lastSpawnIndex = -1;
    private float ArenaWidth => arenaRightX - arenaLeftX;

    // ═══════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ═══════════════════════════════════════════════════════

    private void Awake()
    {
        if (bossController == null)
            bossController = GetComponent<BossController>();

        if (player == null)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        if (playerLayer == 0)
            playerLayer = LayerMask.GetMask("player");

        InitializePools();
    }

    // ═══════════════════════════════════════════════════════
    //  오브젝트 풀 관리
    // ═══════════════════════════════════════════════════════

    private void InitializePools()
    {
        CreatePool(missilePrefab,    _missilePool, missilePoolSize);
        CreatePool(warningBoxPrefab, _warningPool, warningBoxPoolSize);
        CreatePool(sparkPrefab,      _sparkPool,   sparkPoolSize);
    }

    private static void CreatePool(GameObject prefab, Queue<GameObject> pool, int size)
    {
        if (prefab == null) return;
        for (int i = 0; i < size; i++)
        {
            var go = Instantiate(prefab);
            go.SetActive(false);
            pool.Enqueue(go);
        }
    }

    /// <summary>풀에서 오브젝트 대여. 풀이 비면 동적 생성(fallback).</summary>
    private static GameObject Rent(Queue<GameObject> pool, GameObject fallbackPrefab)
    {
        GameObject go;
        if (pool.Count > 0)
        {
            go = pool.Dequeue();
        }
        else
        {
            if (fallbackPrefab == null)
            {
                Debug.LogWarning("[Phase2Missile] 풀 소진 및 프리팹 없음 — null 반환");
                return null;
            }
            go = Instantiate(fallbackPrefab);
        }
        go.SetActive(true);
        return go;
    }

    /// <summary>오브젝트를 비활성화하고 풀로 반환.</summary>
    private static void Return(Queue<GameObject> pool, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        pool.Enqueue(go);
    }

    // ═══════════════════════════════════════════════════════
    //  공개 진입점 (Public Entry Points)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// BossAttackPatterns.ExecutePattern()에서 StartCoroutine()으로 호출 시 사용.
    /// UniTask를 기존 코루틴 시스템과 호환되도록 래핑.
    /// </summary>
    public IEnumerator ExecuteAsCoroutine()
    {
        bool done = false;
        var cts   = new CancellationTokenSource();

        ExecutePatternAsync(cts.Token)
            .ContinueWith(() => done = true)
            .Forget();

        yield return new WaitUntil(() => done);
    }

    /// <summary>
    /// 메인 비동기 진입점 — 전체 totalCycles 사이클 순차 실행.
    /// UniTask 환경에서 직접 await 가능.
    /// </summary>
    public async UniTask ExecutePatternAsync(CancellationToken ct = default)
    {
        Debug.Log("[Phase2MissilePattern] 패턴 시작");

        for (int cycle = 0; cycle < totalCycles; cycle++)
        {
            ct.ThrowIfCancellationRequested();

            Debug.Log($"[Phase2MissilePattern] 사이클 {cycle + 1}/{totalCycles}");
            await RunSingleCycleAsync(ct);

            // 마지막 사이클 이후에는 대기 없음
            if (cycle < totalCycles - 1)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(betweenCyclePause),
                    cancellationToken: ct
                );
            }
        }

        // ── 모든 사이클 완료 → 중앙 귀환 ──────────────────────────────
        await ReturnToCenterAsync(ct);

        Debug.Log("[Phase2MissilePattern] 패턴 완료");
    }


    // ═══════════════════════════════════════════════════════
    //  귀환 (Return to Center)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 패턴 종료 후 보스를 returnPoint 위치로 이동.
    /// returnSpeed > 0 이면 부드럽게 슬라이딩, 0 이하면 즉시 이동.
    /// returnPoint가 미지정이면 건너뜀.
    /// </summary>
    private async UniTask ReturnToCenterAsync(CancellationToken ct)
    {
        if (returnPoint == null)
        {
            Debug.LogWarning("[Phase2MissilePattern] returnPoint가 지정되지 않았습니다 — 귀환 건너뜀");
            return;
        }

        Vector3 destination = returnPoint.position;

        // 이미 목적지에 있으면 바로 종료
        if (Vector3.Distance(transform.position, destination) < 0.01f)
            return;

        SetBossVisible(true);
        Debug.Log($"[Phase2MissilePattern] 중앙 귀환 시작 → {destination}");

        if (returnSpeed <= 0f)
        {
            // 즉시 이동
            transform.position = destination;
            await UniTask.Yield(ct);
            return;
        }

        // 귀환 시작 시 부스터 작동 (불꽃 파티클 + 사운드 + BoosterJump 애니메이션 재생)
        if (bossController != null)
        {
            bossController.SetBoosterActive(true);
        }

        // 프레임마다 MoveTowards로 부드럽게 이동
        while (Vector3.Distance(transform.position, destination) > 0.01f)
        {
            ct.ThrowIfCancellationRequested();

            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                returnSpeed * Time.deltaTime
            );

            await UniTask.Yield(ct);
        }

        // 오차 제거 — 목적지에 정확히 스냅
        transform.position = destination;

        // 귀환 완료 시 부스터 정지
        if (bossController != null)
        {
            bossController.SetBoosterActive(false);
        }

        Debug.Log("[Phase2MissilePattern] 중앙 귀환 완료");
    }


    // ═══════════════════════════════════════════════════════
    //  단일 사이클
    // ═══════════════════════════════════════════════════════

    private async UniTask RunSingleCycleAsync(CancellationToken ct)
    {
        // ── 1. 투명화 ────────────────────────────────────────────────
        SetBossVisible(false);
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(invisibilityDuration),
            cancellationToken: ct
        );

        // ── 2. 랜덤 스폰 포인트 텔레포트 ────────────────────────────
        int     spawnIdx = PickSpawnPoint();
        Vector3 spawnPos = spawnPoints[spawnIdx].position;
        transform.position = spawnPos;

        // 플레이어 방향에 따라 발사 방향 결정
        float fireDirX = ResolveFireDirection(spawnPos);
        ApplyFacingFlip(fireDirX);

        // ── 3. 가시화 + 차지 애니메이션 ─────────────────────────────
        SetBossVisible(true);
        TriggerAnim("MissileCharge");

        // ── 4. 시각 전보 (정확히 telegraphDuration 초) ───────────────
        await RunTelegraphAsync(spawnPos, fireDirX, ct);

        // ── 5. 미사일 발사 ───────────────────────────────────────────
        LaunchMissile(spawnPos, fireDirX);
        TriggerAnim("MissileFire");
    }

    // ═══════════════════════════════════════════════════════
    //  시각 전보 (Visual Telegraph)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// telegraphDuration 동안 경고 박스 페이드인 + 스파크 스윕을 동시에 실행.
    /// DOTween 없이 UniTask 프레임 루프 + Mathf.Lerp로 처리.
    /// </summary>
    private async UniTask RunTelegraphAsync(Vector3 bossPos, float fireDirX, CancellationToken ct)
    {
        // ── A. 경고 박스 스폰 ────────────────────────────────────────
        var warnBox = Rent(_warningPool, warningBoxPrefab);
        SpriteRenderer warnSR = null;

        if (warnBox != null)
        {
            // 아레나 전체 가로 너비로 크기 설정, 보스 Y 위치에 배치
            float centerX = (arenaLeftX + arenaRightX) * 0.5f;
            warnBox.transform.position   = new Vector3(centerX, bossPos.y, bossPos.z + 0.05f);
            warnBox.transform.localScale = new Vector3(ArenaWidth, missileHeight * 2.2f, 1f);

            warnSR = warnBox.GetComponent<SpriteRenderer>();
            if (warnSR != null)
            {
                // 처음엔 완전 투명으로 시작 → 페이드인
                Color startColor = warningColor;
                startColor.a = 0f;
                warnSR.color = startColor;
            }
        }

        // ── B. 스윕 스파크 스폰 ──────────────────────────────────────
        var   spark  = Rent(_sparkPool, sparkPrefab);
        float startX = bossPos.x;
        float endX   = fireDirX > 0f ? arenaRightX : arenaLeftX;

        SpriteRenderer sparkSR = null;
        if (spark != null)
        {
            // 스파크 초기 위치 = 보스 위치, 경고 박스보다 앞 레이어
            spark.transform.position = new Vector3(startX, bossPos.y, bossPos.z - 0.1f);
            sparkSR = spark.GetComponent<SpriteRenderer>();
            if (sparkSR != null) sparkSR.color = sparkColor;
        }

        // ── C. telegraphDuration 동안 프레임 단위 보간 ───────────────
        float elapsed = 0f;

        while (elapsed < telegraphDuration)
        {
            ct.ThrowIfCancellationRequested();

            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / telegraphDuration);
            float easedT = EaseOutQuad(t); // 시작이 빠르고 끝이 부드럽게 감속

            // 스파크: 보스 위치 → 화면 끝으로 수평 이동
            if (spark != null)
            {
                float sparkX = Mathf.Lerp(startX, endX, easedT);
                spark.transform.position = new Vector3(
                    sparkX, bossPos.y, spark.transform.position.z);

                // 고주파 깜박임으로 에너지 잔상 연출
                if (sparkSR != null)
                {
                    float flicker = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(elapsed * Mathf.PI * 18f));
                    Color sc      = sparkColor;
                    sc.a          = flicker;
                    sparkSR.color = sc;
                }
            }

            // 경고 박스: 초반 15% 페이드인 → 이후 저주파 맥동
            if (warnSR != null)
            {
                float fadeIn = Mathf.Clamp01(t / 0.15f);
                float pulse  = 0.65f + 0.35f * Mathf.Sin(elapsed * Mathf.PI * 5.5f);
                Color wc     = warningColor;
                wc.a         = warningColor.a * fadeIn * pulse;
                warnSR.color = wc;
            }

            await UniTask.Yield(ct);
        }

        // ── D. 전보 오브젝트 정리 ─────────────────────────────────────
        Return(_sparkPool,   spark);
        Return(_warningPool, warnBox);
    }

    // ═══════════════════════════════════════════════════════
    //  미사일 발사
    // ═══════════════════════════════════════════════════════

    private void LaunchMissile(Vector3 origin, float fireDirX)
    {
        var missile = Rent(_missilePool, missilePrefab);
        if (missile == null)
        {
            Debug.LogWarning("[Phase2MissilePattern] 미사일 풀 소진 — 발사 건너뜀");
            return;
        }

        missile.transform.position = origin;

        // 미사일 스프라이트 방향 플립
        Vector3 sc = missile.transform.localScale;
        sc.x = Mathf.Abs(sc.x) * (fireDirX > 0f ? 1f : -1f);
        missile.transform.localScale = sc;

        var comp = missile.GetComponent<Phase2Missile>();
        if (comp != null)
        {
            comp.Initialize(
                direction:   new Vector2(fireDirX, 0f),
                speed:       missileSpeed,
                damage:      missileDamage,
                returnPool:  _missilePool,
                playerLayer: playerLayer
            );
        }
        else
        {
            Debug.LogError("[Phase2MissilePattern] missilePrefab에 Phase2Missile 컴포넌트 없음!");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  유틸리티 헬퍼
    // ═══════════════════════════════════════════════════════

    /// <summary>직전 스폰 인덱스 제외 랜덤 선택.</summary>
    private int PickSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return 0;
        if (spawnPoints.Length == 1)                        return 0;

        int idx;
        int attempts = 0;
        do
        {
            idx = Random.Range(0, spawnPoints.Length);
            attempts++;
        }
        while (idx == _lastSpawnIndex && attempts < 20);

        _lastSpawnIndex = idx;
        return idx;
    }

    /// <summary>플레이어가 보스 오른쪽이면 +1f, 왼쪽이면 -1f.</summary>
    private float ResolveFireDirection(Vector3 fromPos)
    {
        if (player == null) return 1f;
        return player.position.x >= fromPos.x ? 1f : -1f;
    }

    private void SetBossVisible(bool visible)
    {
        if (bossSprites != null)
            foreach (var sr in bossSprites)
                if (sr != null) sr.enabled = visible;

        if (bossColliders != null)
            foreach (var col in bossColliders)
                if (col != null) col.enabled = visible;
    }

    /// <summary>
    /// 기존 BossController.FacePlayer()와 동일한 localScale.x 부호 반전 방식.
    /// 프로젝트 컨벤션: 음수 X → 오른쪽 바라봄.
    /// </summary>
    private void ApplyFacingFlip(float dirX)
    {
        Vector3 scale = transform.localScale;
        float   absX  = Mathf.Abs(scale.x);
        scale.x = dirX > 0f ? -absX : absX;
        transform.localScale = scale;
    }

    private void TriggerAnim(string triggerName)
    {
        if (bossAnimator != null)
            bossAnimator.SetTrigger(triggerName);
    }

    /// <summary>Ease-Out Quadratic: 빠른 시작 후 부드럽게 감속.</summary>
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}
