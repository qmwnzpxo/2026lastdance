using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 패턴 2. 멀티 타입 판넬 소환 (Strategic Destruction) [Sequential]
    //
    // 파훼 조건: 판넬 A/B/C 전부 파괴
    // 파훼 흐름: destroyedCount >= totalPanels
    //            → ctx.DamageHandler.BreakBarrier()
    //            → DamageHandler.OnBarrierBroken 이벤트 발생
    //            → Phase1ShieldPattern.onBarrierBroken 콜백 호출
    //            → 방어막 5초 해제
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_MultiPanel",
                     menuName = "BossSystem/Patterns/Phase1_MultiPanel")]
    public class Phase1MultiPanelPattern : BossPatternBase
    {
        [Header("Panel Prefabs")]
        [Tooltip("Type A: 공격형 (빨강) — PanelHealth 필요")]
        [SerializeField] private GameObject _panelPrefabA;
        [Tooltip("Type B: 돌진형 (파랑) — PanelHealth 필요")]
        [SerializeField] private GameObject _panelPrefabB;
        [Tooltip("Type C: 2갈래빔형 (초록) — PanelHealth 필요")]
        [SerializeField] private GameObject _panelPrefabC;

        [Header("Spawn & Move")]
        [SerializeField] private float _spawnRiseHeight = 4f;
        [SerializeField] private float _riseSpeed = 8f;
        [SerializeField] private float _moveToSlotSpeed = 5f;
        [SerializeField] private float _spawnInterval = 0.3f;

        [Header("Slot Positions")]
        [SerializeField]
        private Vector2[] _slotPositions = {
            new Vector2(-4f, 3f),
            new Vector2( 0f, 3f),
            new Vector2( 4f, 3f)
        };

        [Header("Type A — Beam")]
        [Tooltip("조준선 프리팹 (얇은 LineRenderer)")]
        [SerializeField] private GameObject _beamLinePrefab;
        [Tooltip("발사 빔 프리팹 (두꺼운 LineRenderer). 비워두면 조준선 프리팹을 재사용합니다.")]
        [SerializeField] private GameObject _fireBeamLinePrefab;
        [SerializeField] private Color _beamColorA = Color.red;
        [SerializeField] private Color _fireBeamColorA = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float _fireBeamWidth = 0.4f;
        [SerializeField] private float _aimDurationA = 1f;
        [SerializeField] private float _fireDurationA = 0.3f;
        [SerializeField] private float _beamDamageA = 15f;
        [SerializeField] private float _randomMoveRangeA = 10f;
        [SerializeField] private float _randomMoveSpeedA = 4f;

        [Header("Type B — Charge")]
        [SerializeField] private float _chargeAimDurationB = 0.5f;
        [SerializeField] private float _chargeMoveSpeedB = 12f;
        [SerializeField] private float _chargeDamageB = 20f;
        [SerializeField] private float _chargeDistanceB = 20f;

        [Header("Type C — Split Beam")]
        [Tooltip("조준선 프리팹 (얇은 LineRenderer)")]
        [SerializeField] private GameObject _splitBeamLinePrefab;
        [Tooltip("발사 빔 프리팹 (두꺼운 LineRenderer). 비워두면 조준선 프리팹을 재사용합니다.")]
        [SerializeField] private GameObject _splitFireBeamLinePrefab;
        [SerializeField] private Color _beamColorC = Color.green;
        [SerializeField] private Color _fireBeamColorC = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private float _splitFireBeamWidth = 0.4f;
        [SerializeField] private float _aimDurationC = 1f;
        [SerializeField] private float _fireDurationC = 0.3f;
        [SerializeField] private float _splitAngle = 20f;
        [SerializeField] private float _beamDamageC = 12f;
        [SerializeField] private float _randomMoveRangeC = 10f;
        [SerializeField] private float _randomMoveSpeedC = 4f;

        [Header("Common")]
        [SerializeField] private float _castRadius = 0.15f;
        [SerializeField] private float _castDistance = 30f;
        [SerializeField] private LayerMask _playerLayer;

        // ─────────────────────────────────────────────────────────────────
        // OnExecuteAsync
        // ─────────────────────────────────────────────────────────────────

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            var configs = new (GameObject prefab, int slotIdx)[]
            {
                (_panelPrefabA, 0),
                (_panelPrefabB, 1),
                (_panelPrefabC, 2),
            };

            int totalPanels   = configs.Length;
            int destroyedCount = 0;
            var panels         = new GameObject[totalPanels];

            // ── 1. 소환 → 위로 치솟음 → 슬롯 이동 ───────────────────────
            for (int i = 0; i < totalPanels; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (prefab, slotIdx) = configs[i];
                if (prefab == null) { destroyedCount++; continue; }

                var panel = Object.Instantiate(
                    prefab, ctx.BossTransform.position, Quaternion.identity);
                panels[i] = panel;

                var ph = panel.GetComponent<PanelHealth>();
                if (ph != null)
                {
                    ph.ResetHP();
                    int captured = i;
                    ph.OnPanelDestroyed += _ =>
                    {
                        destroyedCount++;
                        Log($"Panel {captured} destroyed! ({destroyedCount}/{totalPanels})");
                    };
                }

                int capturedSlot = slotIdx < _slotPositions.Length ? slotIdx : 0;
                SpawnAndMoveAsync(panel, ctx.BossTransform.position, capturedSlot, ct).Forget();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: ct);
            }

            float estimatedMoveTime = (_spawnRiseHeight / _riseSpeed)
                                    + 5f / _moveToSlotSpeed + 0.5f;
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(estimatedMoveTime), cancellationToken: ct);

            // ── 2. 각 타입 공격 루프 (병렬) ──────────────────────────────
            var attackTasks = new List<UniTask>();

            if (panels[0] != null) attackTasks.Add(RunTypeALoopAsync(panels[0], ctx, ct));
            if (panels[1] != null) attackTasks.Add(RunTypeBLoopAsync(panels[1], ctx, ct));
            if (panels[2] != null) attackTasks.Add(RunTypeCLoopAsync(panels[2], ctx, ct));

            // 공격 루프 실행 중 모든 판넬이 파괴되면 즉시 탈출
            await UniTask.WhenAny(
                UniTask.WhenAll(attackTasks).SuppressCancellationThrow(),
                WaitAllDestroyedAsync(() => destroyedCount >= totalPanels, ct)
            );

            // ── 3. 정리 ───────────────────────────────────────────────────
            foreach (var p in panels)
                if (p != null) Object.Destroy(p);

            // ── 4. 파훼 판정 → Phase1ShieldPattern 방어막 OFF ────────────
            // BreakBarrier()
            //   → DamageHandler.OnBarrierBroken 이벤트 발생
            //   → Phase1ShieldPattern.onBarrierBroken 콜백 호출
            //   → 방어막 VFX 제거 + 무적 해제 + 5초 딜타임 시작
            ctx.DamageHandler.BreakBarrier();

            ctx.SetPanelPatternCleared();
            Log("MultiPanel pattern complete — all panels destroyed. Barrier broken!");
        }

        // ─────────────────────────────────────────────────────────────────
        // 소환 이동 헬퍼
        // ─────────────────────────────────────────────────────────────────

        private async UniTaskVoid SpawnAndMoveAsync(
            GameObject panel, Vector3 bossPos, int slotIdx, CancellationToken ct)
        {
            if (panel == null) return;

            Vector3 riseTarget = bossPos + Vector3.up * _spawnRiseHeight;
            await LaserUtility.MoveTowardsAsync(
                panel.transform, riseTarget, _riseSpeed, ct);

            if (panel == null || ct.IsCancellationRequested) return;

            Vector3 slotPos = slotIdx < _slotPositions.Length
                ? (Vector3)_slotPositions[slotIdx]
                : riseTarget;

            await LaserUtility.MoveTowardsAsync(
                panel.transform, slotPos, _moveToSlotSpeed, ct);
        }

        // ─────────────────────────────────────────────────────────────────
        // Type A: 조준 → 발사 빔(두꺼운) → 랜덤 X이동 반복
        // ─────────────────────────────────────────────────────────────────

        private async UniTask RunTypeALoopAsync(
            GameObject panel, BossContext ctx, CancellationToken ct)
        {
            GameObject warnLineGo = null;
            LineRenderer warnLine = null;
            if (_beamLinePrefab != null)
            {
                warnLineGo = Object.Instantiate(_beamLinePrefab);
                warnLine   = warnLineGo.GetComponent<LineRenderer>();
            }

            GameObject fireBeamPrefab = _fireBeamLinePrefab != null
                ? _fireBeamLinePrefab : _beamLinePrefab;

            GameObject fireBeamGo = null;
            LineRenderer fireBeam = null;
            if (fireBeamPrefab != null)
            {
                fireBeamGo = Object.Instantiate(fireBeamPrefab);
                fireBeam   = fireBeamGo.GetComponent<LineRenderer>();

                if (fireBeam != null)
                {
                    fireBeam.startWidth = _fireBeamWidth;
                    fireBeam.endWidth   = _fireBeamWidth;
                    fireBeam.startColor = _fireBeamColorA;
                    fireBeam.endColor   = _fireBeamColorA;
                    fireBeam.enabled    = false;
                }
            }

            try
            {
                while (!ct.IsCancellationRequested && panel != null
                       && panel.activeInHierarchy)
                {
                    // [조준] 얇은 경고선 FadeIn + 플레이어 추적
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panel != null ? panel.transform.position : Vector3.zero,
                        () => ctx.PlayerTransform.position,
                        _beamColorA, _aimDurationA, ct);

                    if (panel == null || !panel.activeInHierarchy) break;
                    ct.ThrowIfCancellationRequested();

                    // [발사 시작] 조준선 끄고 굵은 빔 켜기
                    LaserUtility.HideLine(warnLine);

                    Vector3 fireFrom = panel.transform.position;
                    Vector3 fireTo   = ctx.PlayerTransform.position;
                    Vector2 fireDir  = ((Vector2)fireTo - (Vector2)fireFrom).normalized;

                    if (fireBeam != null)
                    {
                        fireBeam.SetPosition(0, fireFrom);
                        fireBeam.SetPosition(1, fireFrom + (Vector3)(fireDir * _castDistance));
                        fireBeam.enabled = true;
                    }

                    // [발사 지속] 매 프레임 CircleCast DPS 데미지
                    float dps     = _beamDamageA / Mathf.Max(_fireDurationA, 0.01f);
                    float elapsed = 0f;

                    while (elapsed < _fireDurationA
                           && !ct.IsCancellationRequested
                           && panel != null && panel.activeInHierarchy)
                    {
                        LaserUtility.CircleCastAndDamage(
                            fireFrom, fireDir,
                            _castRadius, _castDistance,
                            _playerLayer, dps * Time.deltaTime);

                        elapsed += Time.deltaTime;
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    if (fireBeam != null) fireBeam.enabled = false;

                    if (panel == null || !panel.activeInHierarchy) break;

                    // [이동] 랜덤 X 이동
                    float   targetX = Random.Range(-_randomMoveRangeA, _randomMoveRangeA);
                    Vector3 dest    = new Vector3(targetX, panel.transform.position.y,
                                                  panel.transform.position.z);
                    await LaserUtility.MoveTowardsAsync(
                        panel.transform, dest, _randomMoveSpeedA, ct);
                }
            }
            finally
            {
                if (warnLineGo  != null) Object.Destroy(warnLineGo);
                if (fireBeamGo  != null) Object.Destroy(fireBeamGo);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Type B: 정지 조준 → 돌진
        // ─────────────────────────────────────────────────────────────────

        private async UniTask RunTypeBLoopAsync(
            GameObject panel, BossContext ctx, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && panel != null
                       && panel.activeInHierarchy)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_chargeAimDurationB), cancellationToken: ct);

                    if (panel == null || !panel.activeInHierarchy) break;

                    Vector3 target = ctx.PlayerTransform.position;
                    Vector3 dir    = (target - panel.transform.position).normalized;
                    Vector3 dest   = panel.transform.position + dir * _chargeDistanceB;

                    await ChargeWithDamageAsync(panel, dest, dir, ct);
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private async UniTask ChargeWithDamageAsync(
            GameObject panel, Vector3 dest, Vector3 dir, CancellationToken ct)
        {
            bool hit = false;
            while (!ct.IsCancellationRequested && panel != null && !hit)
            {
                Vector3 prev = panel.transform.position;
                panel.transform.position = Vector3.MoveTowards(
                    prev, dest, _chargeMoveSpeedB * Time.deltaTime);

                LaserUtility.BoxCastAndDamage(
                    panel.transform.position, dir, 0.5f, 0.1f,
                    _playerLayer, _chargeDamageB);

                if (Vector3.Distance(panel.transform.position, dest) < 0.05f)
                    hit = true;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Type C: 조준 → 2갈래 발사 빔(두꺼운) → 랜덤 X이동 반복
        // ─────────────────────────────────────────────────────────────────

        private async UniTask RunTypeCLoopAsync(
            GameObject panel, BossContext ctx, CancellationToken ct)
        {
            // 조준선 (얇음)
            GameObject warnLineGoL = null, warnLineGoR = null;
            LineRenderer warnLineL = null, warnLineR   = null;

            if (_splitBeamLinePrefab != null)
            {
                warnLineGoL = Object.Instantiate(_splitBeamLinePrefab);
                warnLineL   = warnLineGoL.GetComponent<LineRenderer>();

                warnLineGoR = Object.Instantiate(_splitBeamLinePrefab);
                warnLineR   = warnLineGoR.GetComponent<LineRenderer>();
            }

            // 발사 빔 (두꺼움)
            GameObject splitFirePrefab = _splitFireBeamLinePrefab != null
                ? _splitFireBeamLinePrefab : _splitBeamLinePrefab;

            GameObject fireBeamGoL = null, fireBeamGoR = null;
            LineRenderer fireBeamL = null, fireBeamR   = null;

            if (splitFirePrefab != null)
            {
                fireBeamGoL = Object.Instantiate(splitFirePrefab);
                fireBeamL   = fireBeamGoL.GetComponent<LineRenderer>();

                fireBeamGoR = Object.Instantiate(splitFirePrefab);
                fireBeamR   = fireBeamGoR.GetComponent<LineRenderer>();

                foreach (var fb in new[] { fireBeamL, fireBeamR })
                {
                    if (fb == null) continue;
                    fb.startWidth = _splitFireBeamWidth;
                    fb.endWidth   = _splitFireBeamWidth;
                    fb.startColor = _fireBeamColorC;
                    fb.endColor   = _fireBeamColorC;
                    fb.enabled    = false;
                }
            }

            try
            {
                while (!ct.IsCancellationRequested && panel != null
                       && panel.activeInHierarchy)
                {
                    float half = _splitAngle * 0.5f;

                    // [조준] 얇은 경고선 2갈래 FadeIn + 플레이어 추적
                    await UniTask.WhenAll(
                        LaserUtility.FadeInWarningLineAsync(
                            warnLineL,
                            () => panel != null ? panel.transform.position : Vector3.zero,
                            () => GetSplitTarget(panel, ctx, -half),
                            _beamColorC, _aimDurationC, ct),
                        LaserUtility.FadeInWarningLineAsync(
                            warnLineR,
                            () => panel != null ? panel.transform.position : Vector3.zero,
                            () => GetSplitTarget(panel, ctx, +half),
                            _beamColorC, _aimDurationC, ct)
                    );

                    if (panel == null || !panel.activeInHierarchy) break;
                    ct.ThrowIfCancellationRequested();

                    // [발사 시작] 조준선 끄고 굵은 빔 2갈래 켜기
                    LaserUtility.HideLine(warnLineL);
                    LaserUtility.HideLine(warnLineR);

                    Vector3 fireFrom   = panel.transform.position;
                    Vector3 snapDestL  = GetSplitTarget(panel, ctx, -half);
                    Vector3 snapDestR  = GetSplitTarget(panel, ctx, +half);
                    Vector2 fireDirL   = ((Vector2)snapDestL - (Vector2)fireFrom).normalized;
                    Vector2 fireDirR   = ((Vector2)snapDestR - (Vector2)fireFrom).normalized;

                    if (fireBeamL != null)
                    {
                        fireBeamL.SetPosition(0, fireFrom);
                        fireBeamL.SetPosition(1, fireFrom + (Vector3)(fireDirL * _castDistance));
                        fireBeamL.enabled = true;
                    }
                    if (fireBeamR != null)
                    {
                        fireBeamR.SetPosition(0, fireFrom);
                        fireBeamR.SetPosition(1, fireFrom + (Vector3)(fireDirR * _castDistance));
                        fireBeamR.enabled = true;
                    }

                    // [발사 지속] 매 프레임 CircleCast DPS × 2갈래
                    float dps     = _beamDamageC / Mathf.Max(_fireDurationC, 0.01f);
                    float elapsed = 0f;

                    while (elapsed < _fireDurationC
                           && !ct.IsCancellationRequested
                           && panel != null && panel.activeInHierarchy)
                    {
                        float frameDmg = dps * Time.deltaTime;

                        LaserUtility.CircleCastAndDamage(
                            fireFrom, fireDirL,
                            _castRadius, _castDistance, _playerLayer, frameDmg);
                        LaserUtility.CircleCastAndDamage(
                            fireFrom, fireDirR,
                            _castRadius, _castDistance, _playerLayer, frameDmg);

                        elapsed += Time.deltaTime;
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    if (fireBeamL != null) fireBeamL.enabled = false;
                    if (fireBeamR != null) fireBeamR.enabled = false;

                    if (panel == null || !panel.activeInHierarchy) break;

                    // [이동] 랜덤 X 이동
                    float   targetX = Random.Range(-_randomMoveRangeC, _randomMoveRangeC);
                    Vector3 dest    = new Vector3(targetX, panel.transform.position.y,
                                                  panel.transform.position.z);
                    await LaserUtility.MoveTowardsAsync(
                        panel.transform, dest, _randomMoveSpeedC, ct);
                }
            }
            finally
            {
                if (warnLineGoL != null) Object.Destroy(warnLineGoL);
                if (warnLineGoR != null) Object.Destroy(warnLineGoR);
                if (fireBeamGoL != null) Object.Destroy(fireBeamGoL);
                if (fireBeamGoR != null) Object.Destroy(fireBeamGoR);
            }
        }

        private Vector3 GetSplitTarget(GameObject panel, BossContext ctx, float angleOffset)
        {
            if (panel == null) return Vector3.zero;
            Vector3 baseDir  = (ctx.PlayerTransform.position
                                - panel.transform.position).normalized;
            float   baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float   rad       = (baseAngle + angleOffset) * Mathf.Deg2Rad;
            Vector3 dir       = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            return panel.transform.position + dir * _castDistance;
        }

        private async UniTask WaitAllDestroyedAsync(
            System.Func<bool> allDestroyed, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (allDestroyed()) return;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
