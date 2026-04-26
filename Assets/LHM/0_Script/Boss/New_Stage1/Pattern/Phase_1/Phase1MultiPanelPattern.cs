using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 패턴 2. 멀티 타입 판넬 소환 (Strategic Destruction) [Sequential]
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
        [Tooltip("소환 시 위로 치솟는 높이 (보스 기준 +Y)")]
        [SerializeField] private float _spawnRiseHeight  = 4f;
        [Tooltip("치솟는 속도")]
        [SerializeField] private float _riseSpeed        = 8f;
        [Tooltip("슬롯으로 이동하는 속도")]
        [SerializeField] private float _moveToSlotSpeed  = 5f;
        [Tooltip("소환 간 딜레이 (초)")]
        [SerializeField] private float _spawnInterval    = 0.3f;

        [Header("Slot Positions")]
        [Tooltip("각 판넬이 이동할 슬롯 월드 좌표 (순서: A, B, C ...)")]
        [SerializeField] private Vector2[] _slotPositions = {
            new Vector2(-4f, 3f),
            new Vector2( 0f, 3f),
            new Vector2( 4f, 3f)
        };

        [Header("Type A — Beam")]
        // ★ LineRenderer → GameObject 로 변경: 인스펙터에서 에셋(프리팹) 자유롭게 연결 가능
        [SerializeField] private GameObject _beamLinePrefab;
        [SerializeField] private Color      _beamColorA        = Color.red;
        [SerializeField] private float      _aimDurationA      = 1f;
        [SerializeField] private float      _fireDurationA     = 0.3f;
        [SerializeField] private float      _beamDamageA       = 15f;
        [SerializeField] private float      _randomMoveRangeA  = 10f;
        [SerializeField] private float      _randomMoveSpeedA  = 4f;

        [Header("Type B — Charge")]
        [SerializeField] private float _chargeAimDurationB = 0.5f;
        [SerializeField] private float _chargeMoveSpeedB   = 12f;
        [SerializeField] private float _chargeDamageB      = 20f;
        [SerializeField] private float _chargeDistanceB    = 20f;

        [Header("Type C — Split Beam")]
        // ★ LineRenderer → GameObject 로 변경
        [SerializeField] private GameObject _splitBeamLinePrefab;
        [SerializeField] private Color      _beamColorC         = Color.green;
        [SerializeField] private float      _aimDurationC       = 1f;
        [SerializeField] private float      _fireDurationC      = 0.3f;
        [SerializeField] private float      _splitAngle         = 20f;
        [SerializeField] private float      _beamDamageC        = 12f;
        [SerializeField] private float      _randomMoveRangeC   = 10f;
        [SerializeField] private float      _randomMoveSpeedC   = 4f;

        [Header("Common")]
        [SerializeField] private float     _castRadius    = 0.15f;
        [SerializeField] private float     _castDistance  = 30f;
        [SerializeField] private LayerMask _playerLayer;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            var configs = new (GameObject prefab, int slotIdx)[]
            {
                (_panelPrefabA, 0),
                (_panelPrefabB, 1),
                (_panelPrefabC, 2),
            };

            int totalPanels    = configs.Length;
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

            if (panels[0] != null)
                attackTasks.Add(RunTypeALoopAsync(panels[0], ctx, ct));
            if (panels[1] != null)
                attackTasks.Add(RunTypeBLoopAsync(panels[1], ctx, ct));
            if (panels[2] != null)
                attackTasks.Add(RunTypeCLoopAsync(panels[2], ctx, ct));

            await UniTask.WhenAny(
                UniTask.WhenAll(attackTasks).SuppressCancellationThrow(),
                WaitAllDestroyedAsync(() => destroyedCount >= totalPanels, ct)
            );

            // ── 3. 정리 ───────────────────────────────────────────────────
            foreach (var p in panels)
                if (p != null) Object.Destroy(p);

            ctx.SetPanelPatternCleared();
            Log("MultiPanel pattern complete — all panels destroyed.");
        }

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

        // ── Type A: 조준 → 빔 → 랜덤 X이동 반복 ─────────────────────────

        private async UniTask RunTypeALoopAsync(
            GameObject panel, BossContext ctx, CancellationToken ct)
        {
            // ★ GameObject로 생성 후 GetComponent로 LineRenderer 획득
            GameObject warnLineGo = null;
            LineRenderer warnLine = null;
            if (_beamLinePrefab != null)
            {
                warnLineGo = Object.Instantiate(_beamLinePrefab);
                warnLine   = warnLineGo.GetComponent<LineRenderer>();
            }

            try
            {
                while (!ct.IsCancellationRequested && panel != null
                       && panel.activeInHierarchy)
                {
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panel != null ? panel.transform.position : Vector3.zero,
                        () => ctx.PlayerTransform.position,
                        _beamColorA, _aimDurationA, ct);

                    if (panel == null || !panel.activeInHierarchy) break;
                    ct.ThrowIfCancellationRequested();

                    Vector3 from  = panel.transform.position;
                    Vector2 dir2D = ((Vector2)ctx.PlayerTransform.position - (Vector2)from).normalized;
                    LaserUtility.SetLineImmediate(
                        warnLine, from, ctx.PlayerTransform.position, _beamColorA);
                    LaserUtility.CircleCastAndDamage(
                        from, dir2D, _castRadius, _castDistance, _playerLayer, _beamDamageA);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireDurationA), cancellationToken: ct);

                    LaserUtility.HideLine(warnLine);

                    if (panel == null || !panel.activeInHierarchy) break;
                    float targetX = Random.Range(-_randomMoveRangeA, _randomMoveRangeA);
                    Vector3 dest  = new Vector3(targetX, panel.transform.position.y,
                                                panel.transform.position.z);
                    await LaserUtility.MoveTowardsAsync(
                        panel.transform, dest, _randomMoveSpeedA, ct);
                }
            }
            finally
            {
                // ★ GameObject 기준으로 파괴
                if (warnLineGo != null) Object.Destroy(warnLineGo);
            }
        }

        // ── Type B: 정지 조준 → 돌진 ─────────────────────────────────────

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

        // ── Type C: 조준 → 2갈래 빔 → 랜덤 X이동 반복 ───────────────────

        private async UniTask RunTypeCLoopAsync(
            GameObject panel, BossContext ctx, CancellationToken ct)
        {
            // ★ GameObject로 생성 후 GetComponent로 LineRenderer 획득 (2갈래)
            GameObject warnLineGoL = null, warnLineGoR = null;
            LineRenderer warnLineL = null, warnLineR = null;

            if (_splitBeamLinePrefab != null)
            {
                warnLineGoL = Object.Instantiate(_splitBeamLinePrefab);
                warnLineL   = warnLineGoL.GetComponent<LineRenderer>();

                warnLineGoR = Object.Instantiate(_splitBeamLinePrefab);
                warnLineR   = warnLineGoR.GetComponent<LineRenderer>();
            }

            try
            {
                while (!ct.IsCancellationRequested && panel != null
                       && panel.activeInHierarchy)
                {
                    float half = _splitAngle * 0.5f;
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

                    FireSplitBeam(panel, ctx, half, warnLineL, warnLineR);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireDurationC), cancellationToken: ct);

                    LaserUtility.HideLine(warnLineL);
                    LaserUtility.HideLine(warnLineR);

                    if (panel == null || !panel.activeInHierarchy) break;
                    float targetX = Random.Range(-_randomMoveRangeC, _randomMoveRangeC);
                    Vector3 dest  = new Vector3(targetX, panel.transform.position.y,
                                                panel.transform.position.z);
                    await LaserUtility.MoveTowardsAsync(
                        panel.transform, dest, _randomMoveSpeedC, ct);
                }
            }
            finally
            {
                // ★ GameObject 기준으로 파괴
                if (warnLineGoL != null) Object.Destroy(warnLineGoL);
                if (warnLineGoR != null) Object.Destroy(warnLineGoR);
            }
        }

        private Vector3 GetSplitTarget(GameObject panel, BossContext ctx, float angleOffset)
        {
            if (panel == null) return Vector3.zero;
            Vector3 baseDir = (ctx.PlayerTransform.position
                               - panel.transform.position).normalized;
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float rad       = (baseAngle + angleOffset) * Mathf.Deg2Rad;
            Vector3 dir     = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            return panel.transform.position + dir * _castDistance;
        }

        private void FireSplitBeam(
            GameObject panel, BossContext ctx, float halfAngle,
            LineRenderer lineL, LineRenderer lineR)
        {
            Vector3 from = panel.transform.position;

            Vector3 destL = GetSplitTarget(panel, ctx, -halfAngle);
            Vector3 destR = GetSplitTarget(panel, ctx, +halfAngle);

            LaserUtility.SetLineImmediate(lineL, from, destL, _beamColorC);
            LaserUtility.SetLineImmediate(lineR, from, destR, _beamColorC);

            Vector2 dirL = ((Vector2)destL - (Vector2)from).normalized;
            Vector2 dirR = ((Vector2)destR - (Vector2)from).normalized;

            LaserUtility.CircleCastAndDamage(
                from, dirL, _castRadius, _castDistance, _playerLayer, _beamDamageC);
            LaserUtility.CircleCastAndDamage(
                from, dirR, _castRadius, _castDistance, _playerLayer, _beamDamageC);
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
