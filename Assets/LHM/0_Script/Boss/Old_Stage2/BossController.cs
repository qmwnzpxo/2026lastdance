using System;
using System.Collections.Generic;
using System.Threading;
using BossSystem.Patterns;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    [RequireComponent(typeof(DamageHandler))]
    public class BossController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Phase Data")]
        [Tooltip("Index 0 = 1페이즈. hpThreshold 내림차순 정렬.")]
        [SerializeField] private List<BossPhase> _phases;

        [Header("Panel Waypoints")]
        [SerializeField] private Transform[] _panelWaypoints;

        [Header("Pool Preload")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;
        [SerializeField] private Transform _poolRoot;

        [SerializeField] private QTEUIController _qteUIController;

        private DamageHandler _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext _context;
        private int _currentPhaseIndex = -1;

        private CancellationTokenSource _lifetimeCts;
        private CancellationTokenSource _phaseCts;

        // ── 페이즈 전환 조건 플래그 ───────────────────────────────────────
        // _barrierCleared : 배리어(코어/쉴드) 파훼 중 = true, 재활성화 = false
        private bool _barrierCleared = false;
        private void HandleBarrierBroken() => _barrierCleared = true;
        private void HandleBarrierRestored() => _barrierCleared = false;

        public event System.Action<int> OnPhaseChanged;
        public event System.Action OnBossDead;

        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _damageHandler = GetComponent<DamageHandler>();
            _damageHandler.OnDeath += HandleDeath;

            _lifetimeCts = new CancellationTokenSource();
            _blackboard = new BossBlackboard(_playerTransform);
            _context = new BossContext(transform, _playerTransform, _damageHandler, _blackboard);

            PreloadPools();
        }

        private void Start() => StartBossAsync(_lifetimeCts.Token).Forget();
        private void Update() => _blackboard.UpdatePlayerVelocity();
        private void LateUpdate() => _blackboard?.Tick();

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _phaseCts?.Dispose();
            ObjectPool.ClearAll();
        }

        private void PreloadPools()
        {
            if (_poolPreloadEntries == null) return;
            foreach (var entry in _poolPreloadEntries)
            {
                if (entry.Prefab == null || entry.Count <= 0) continue;
                ObjectPool.Preload(entry.Prefab, _poolRoot, entry.Count);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 보스 메인 루프
        // ─────────────────────────────────────────────────────────────────

        private async UniTaskVoid StartBossAsync(CancellationToken lifetimeCt)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                if (lifetimeCt.IsCancellationRequested) return;
                await EnterPhaseAsync(i, lifetimeCt);
            }
            Debug.Log("[BossController] All phases completed.");
        }

        private async UniTask EnterPhaseAsync(int phaseIndex, CancellationToken lifetimeCt)
        {
            _currentPhaseIndex = phaseIndex;
            var phase = _phases[phaseIndex];

            Debug.Log($"[BossController] Entering Phase {phaseIndex + 1}");

            CancelAndRecreatePhaseCts(lifetimeCt);
            _damageHandler.ForceResetInvincibility();

            if (phase.hpRecoverRatio > 0f)
                _damageHandler.RecoverHP(phase.hpRecoverRatio);

            if (phase.spawnPointIndex >= 0 && phase.spawnPointIndex < _spawnPoints.Length)
                transform.position = _spawnPoints[phase.spawnPointIndex].position;

            // ── 플래그 리셋 ───────────────────────────────────────────────
            _context.DestroyedCoreCount = 0;
            _context.ResetPanelPatternCleared(); // 판넬 클리어 플래그도 리셋
            _barrierCleared = false;
            OnPhaseChanged?.Invoke(phaseIndex);
            InjectSceneDependencies(phase);

            // ── 이벤트 구독 ───────────────────────────────────────────────
            _blackboard.OnBarrierBroken += HandleBarrierBroken;
            _damageHandler.OnBarrierBroken += HandleBarrierBroken;
            _damageHandler.OnBarrierRestored += HandleBarrierRestored;

            var phaseCt = _phaseCts.Token;

            if (phaseIndex < _phases.Count - 1)
            {
                var nextPhase = _phases[phaseIndex + 1];

#if UNITY_EDITOR
                if (_debugAutoSkipSeconds > 0f)
                {
                    await UniTask.WhenAny(
                        RunPhasePatternsAsync(phase, phaseCt),
                        WatchPhaseTransitionAsync(phase, nextPhase.hpThreshold, phaseCt, lifetimeCt),
                        UniTask.Delay(System.TimeSpan.FromSeconds(_debugAutoSkipSeconds),
                                      cancellationToken: lifetimeCt)
                    );
                }
                else
#endif
                {
                    await UniTask.WhenAny(
                        RunPhasePatternsAsync(phase, phaseCt),
                        WatchPhaseTransitionAsync(phase, nextPhase.hpThreshold, phaseCt, lifetimeCt)
                    );
                }
            }
            else
            {
                await RunPhasePatternsAsync(phase, phaseCt);
            }

            // ── 구독 해제 ─────────────────────────────────────────────────
            _blackboard.OnBarrierBroken -= HandleBarrierBroken;
            _damageHandler.OnBarrierBroken -= HandleBarrierBroken;
            _damageHandler.OnBarrierRestored -= HandleBarrierRestored;

            // phaseCts 취소 → 아직 돌고 있는 패턴 Task들에 ct 전파
            _phaseCts?.Cancel();

            Debug.Log($"[BossController] Phase {phaseIndex + 1} ended.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 페이즈 전환 감시
        //
        // 전환 조건 (AND):
        //   1. HP <= hpThreshold
        //   2. _barrierCleared == true  (배리어 파훼 딜타임 중)
        //   3. 해당 페이즈에 PanelPattern이 있다면 ctx.IsPanelPatternCleared == true
        //
        // PanelPattern이 없는 페이즈는 조건 3을 건너뜀.
        // ─────────────────────────────────────────────────────────────────
        private async UniTask WatchPhaseTransitionAsync(
            BossPhase currentPhase,
            float hpThreshold,
            CancellationToken phaseCt,
            CancellationToken lifetimeCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(phaseCt, lifetimeCt);
            var ct = linked.Token;

            // 이 페이즈에 PanelPattern이 있는지 미리 판단
            bool hasPanelPattern = false;
            foreach (var p in currentPhase.sequentialPatterns)
                if (p is Phase1PanelPattern) { hasPanelPattern = true; break; }
            foreach (var p in currentPhase.persistentPatterns)
                if (p is Phase1PanelPattern) { hasPanelPattern = true; break; }

            Debug.Log($"[BossController] WatchPhase start. hasPanelPattern={hasPanelPattern}, hpThreshold={hpThreshold:P0}");

            while (!ct.IsCancellationRequested)
            {
                bool hpOk = _damageHandler.HPRatio <= hpThreshold;
                bool barrierOk = _barrierCleared;
                bool panelOk = !hasPanelPattern || _context.IsPanelPatternCleared;

                if (hpOk && barrierOk && panelOk)
                {
                    Debug.Log($"[BossController] Phase transition! " +
                              $"HP={_damageHandler.HPRatio:P0} barrier={barrierOk} panel={panelOk}");
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 패턴 실행
        // ─────────────────────────────────────────────────────────────────

        private async UniTask RunPhasePatternsAsync(BossPhase phase, CancellationToken phaseCt)
        {
            var tasks = new List<UniTask>(phase.persistentPatterns.Count + 1);

            foreach (var p in phase.persistentPatterns)
                if (p != null) tasks.Add(p.ExecuteAsync(_context, phaseCt));

            if (phase.sequentialPatterns.Count > 0)
                tasks.Add(RunSequentialLoopAsync(phase.sequentialPatterns, phaseCt));

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks).SuppressCancellationThrow();
        }

        private async UniTask RunSequentialLoopAsync(
            List<BossPatternBase> patterns, CancellationToken ct)
        {
            int index = 0;
            while (!ct.IsCancellationRequested)
            {
                if (patterns[index] != null)
                    await patterns[index].ExecuteAsync(_context, ct);

                ct.ThrowIfCancellationRequested();
                index = (index + 1) % patterns.Count;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 씬 의존성 주입
        // ─────────────────────────────────────────────────────────────────

        private void InjectSceneDependencies(BossPhase phase)
        {
            var allPatterns = new List<BossPatternBase>();
            allPatterns.AddRange(phase.persistentPatterns);
            allPatterns.AddRange(phase.sequentialPatterns);

            foreach (var pattern in allPatterns)
            {
                if (pattern == null) continue;

                if (pattern is Phase1LaserPattern laserPattern)
                {
                    laserPattern.SetBlackboard(_blackboard);
                    Debug.Log("[BossController] Injected Blackboard → Phase1LaserPattern");
                }

                if (pattern is Phase1PanelPattern panelPattern)
                {
                    if (_panelWaypoints != null && _panelWaypoints.Length > 0)
                    {
                        panelPattern.SetPoints(_panelWaypoints);
                        Debug.Log($"[BossController] Injected {_panelWaypoints.Length} waypoints → Phase1PanelPattern");
                    }
                    else
                    {
                        Debug.LogWarning("[BossController] _panelWaypoints가 비어있습니다.");
                    }
                }
                if (pattern is Phase1NarrowingLaserPattern narrowingPattern)
                {
                    narrowingPattern.SetQTEUI(_qteUIController);
                    Debug.Log("[BossController] Injected QTEUIController → NarrowingLaserPattern");
                }
            }

        }

        // ─────────────────────────────────────────────────────────────────
        // CTS 관리
        // ─────────────────────────────────────────────────────────────────

        private void CancelAndRecreatePhaseCts(CancellationToken lifetimeCt)
        {
            _phaseCts?.Cancel();
            _phaseCts?.Dispose();
            _phaseCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCt);
        }

        private void HandleDeath()
        {
            Debug.Log("[BossController] Boss died.");
            _phaseCts?.Cancel();
            OnBossDead?.Invoke();
        }

        public void TakeDamage(float damage) => _damageHandler.TakeDamage(damage);

        // ─────────────────────────────────────────────────────────────────
        // 에디터 전용
        // ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [Header("Debug (Editor Only)")]
        [Tooltip("이 시간(초)이 지나면 강제로 다음 패턴으로 넘어갑니다. 0 = 비활성")]
        [SerializeField] private float _debugAutoSkipSeconds = 0f;

        [ContextMenu("Debug: Break Barrier / Skip Pattern")]
        private void Debug_BreakBarrier()
        {
            _damageHandler.ForceResetInvincibility();
            _blackboard.Debug_ForceBarrierBroken();
            Debug.Log("[Debug] Barrier force-broken.");
        }

        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var sp in _spawnPoints)
                    if (sp != null) Gizmos.DrawWireSphere(sp.position, 0.5f);
            }
            if (_panelWaypoints == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _panelWaypoints.Length; i++)
            {
                if (_panelWaypoints[i] == null) continue;
                Gizmos.DrawWireSphere(_panelWaypoints[i].position, 0.4f);
                if (i + 1 < _panelWaypoints.Length && _panelWaypoints[i + 1] != null)
                    Gizmos.DrawLine(_panelWaypoints[i].position, _panelWaypoints[i + 1].position);
            }
        }
#endif
    }

    [System.Serializable]
    public struct PoolPreloadEntry
    {
        [Tooltip("풀링할 프리팹")]
        public GameObject Prefab;
        [Tooltip("미리 생성할 수량")]
        public int Count;
    }
}
