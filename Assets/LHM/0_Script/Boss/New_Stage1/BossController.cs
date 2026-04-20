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

        // ── Phase1PanelPattern에 주입할 웨이포인트 ──────────────────────
        [Header("Panel Waypoints")]
        [Tooltip("Phase1PanelPattern이 사용할 판넬 이동 경로 Transform 배열")]
        [SerializeField] private Transform[] _panelWaypoints;

        // ── Pool 프리로드 ─────────────────────────────────────────────────
        [Header("Pool Preload")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;
        [SerializeField] private Transform _poolRoot;

        // ── 내부 컴포넌트 ─────────────────────────────────────────────────
        private DamageHandler _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext _context;

        private int _currentPhaseIndex = -1;

        // ── CTS 계층 ──────────────────────────────────────────────────────
        private CancellationTokenSource _lifetimeCts;
        private CancellationTokenSource _phaseCts;

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

        private void Start()
        {
            StartBossAsync(_lifetimeCts.Token).Forget();
        }

        private void Update()
        {
            _blackboard.UpdatePlayerVelocity();
        }

        private void LateUpdate()
        {
            _blackboard?.Tick();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _phaseCts?.Dispose();
            ObjectPool.ClearAll();
        }

        // ─────────────────────────────────────────────────────────────────
        // Pool 프리로드
        // ─────────────────────────────────────────────────────────────────

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

        //private async UniTask EnterPhaseAsync(int phaseIndex, CancellationToken lifetimeCt)
        //{
        //    _currentPhaseIndex = phaseIndex;
        //    var phase = _phases[phaseIndex];

        //    Debug.Log($"[BossController] Entering Phase {phaseIndex + 1}");

        //    CancelAndRecreatePhaseCts(lifetimeCt);

        //    _damageHandler.ForceResetInvincibility();
        //    if (phase.hpRecoverRatio > 0f)
        //        _damageHandler.RecoverHP(phase.hpRecoverRatio);

        //    if (phase.spawnPointIndex >= 0 && phase.spawnPointIndex < _spawnPoints.Length)
        //        transform.position = _spawnPoints[phase.spawnPointIndex].position;

        //    _context.DestroyedCoreCount = 0;
        //    OnPhaseChanged?.Invoke(phaseIndex);


        //    InjectSceneDependencies(phase);

        //    var phaseCt = _phaseCts.Token;
        //    var patternTask = RunPhasePatternsAsync(phase, phaseCt);

        //    if (phaseIndex < _phases.Count - 1)
        //    {
        //        var nextPhase = _phases[phaseIndex + 1];
        //        await WatchPhaseTransitionAsync(nextPhase.hpThreshold, phaseCt, lifetimeCt);
        //    }
        //    else
        //    {
        //        await UniTask.WaitUntilCanceled(phaseCt);
        //    }

        //    _phaseCts?.Cancel();
        //    await patternTask.SuppressCancellationThrow();

        //    Debug.Log($"[BossController] Phase {phaseIndex + 1} ended.");
        //}

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

            _context.DestroyedCoreCount = 0;
            _phaseCleared = false;          // ★ 추가
            OnPhaseChanged?.Invoke(phaseIndex);

            InjectSceneDependencies(phase);

            // ★ 배리어 파훼 이벤트 구독
            _blackboard.OnBarrierBroken += HandleBarrierBroken;

            var phaseCt = _phaseCts.Token;
            var patternTask = RunPhasePatternsAsync(phase, phaseCt);

            if (phaseIndex < _phases.Count - 1)
            {
                var nextPhase = _phases[phaseIndex + 1];

#if UNITY_EDITOR
                if (_debugAutoSkipSeconds > 0f)
                {
                    await UniTask.WhenAny(
                        WatchPhaseTransitionAsync(nextPhase.hpThreshold, phaseCt, lifetimeCt),
                        UniTask.Delay(System.TimeSpan.FromSeconds(_debugAutoSkipSeconds),
                                      cancellationToken: lifetimeCt)
                    );
                    Debug.Log($"[Debug] Auto-skip triggered after {_debugAutoSkipSeconds}s");
                }
                else
#endif
                {
                    await WatchPhaseTransitionAsync(nextPhase.hpThreshold, phaseCt, lifetimeCt);
                }
            }
            else
            {
                await UniTask.WaitUntilCanceled(phaseCt);
            }

            // ★ 구독 해제
            _blackboard.OnBarrierBroken -= HandleBarrierBroken;

            _phaseCts?.Cancel();
            await patternTask.SuppressCancellationThrow();

            Debug.Log($"[BossController] Phase {phaseIndex + 1} ended.");
        }
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
                        Debug.LogWarning("[BossController] _panelWaypoints가 비어있습니다. Inspector에서 할당해주세요.");
                    }
                }
            }
        }

        private async UniTask RunPhasePatternsAsync(BossPhase phase, CancellationToken phaseCt)
        {
            var tasks = new List<UniTask>(phase.persistentPatterns.Count + 1);

            foreach (var p in phase.persistentPatterns)
                if (p != null) tasks.Add(p.ExecuteAsync(_context, phaseCt));

            if (phase.sequentialPatterns.Count > 0)
                tasks.Add(RunSequentialLoopAsync(phase.sequentialPatterns, phaseCt));

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
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

        // ─────────────────────────────────────────────────────────────────
        // 외부 API
        // ─────────────────────────────────────────────────────────────────

        public void TakeDamage(float damage) => _damageHandler.TakeDamage(damage);

#if UNITY_EDITOR
        [Header("Debug (Editor Only)")]
        [Tooltip("이 시간(초)이 지나면 강제로 다음 패턴으로 넘어갑니다. 0 = 비활성")]
        [SerializeField] private float _debugAutoSkipSeconds = 0f;

        [ContextMenu("Debug: Break Barrier / Skip Pattern")]
        private void Debug_BreakBarrier()
        {
            _damageHandler.ForceResetInvincibility();
            _blackboard.Debug_ForceBarrierBroken();   // ← Blackboard에 추가
            Debug.Log("[Debug] Barrier force-broken.");
        }



        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var sp in _spawnPoints)
                if (sp != null) Gizmos.DrawWireSphere(sp.position, 0.5f);

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

        // ★ 필드 추가
        private bool _phaseCleared = false;
        private void HandleBarrierBroken() => _phaseCleared = true;

        // ★ WatchPhaseTransitionAsync 수정 — HP AND 파훼 조건 둘 다 체크
        private async UniTask WatchPhaseTransitionAsync(
            float hpThreshold, CancellationToken phaseCt, CancellationToken lifetimeCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(phaseCt, lifetimeCt);
            var ct = linked.Token;

            while (!ct.IsCancellationRequested)
            {
                // HP 조건 AND 패턴 파훼 조건 둘 다 충족해야 전환
                if (_damageHandler.HPRatio <= hpThreshold && _phaseCleared)
                {
                    Debug.Log($"[BossController] Phase transition: HP={_damageHandler.HPRatio:P0}, Cleared={_phaseCleared}");
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
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
