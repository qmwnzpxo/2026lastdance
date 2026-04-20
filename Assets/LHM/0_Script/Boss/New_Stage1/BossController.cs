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
        [Tooltip("Phase1PanelPattern이 사용할 판넬 이동 경로 Transform 배열")]
        [SerializeField] private Transform[] _panelWaypoints;

        [Header("Pool Preload")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;
        [SerializeField] private Transform _poolRoot;

        // ── 내부 컴포넌트 ─────────────────────────────────────────────────
        private DamageHandler _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext _context;

        private int _currentPhaseIndex = -1;

        // ─────────────────────────────────────────────────────────────────
        // 페이즈 전환 플래그
        //
        // _barrierBroken  : Persistent 패턴(Shield) 파훼 여부.
        //                   "딜타임 진입 허용" 용도로만 사용.
        //                   페이즈 전환 조건에서 완전히 분리됨.
        //
        // _forceNextPhase : 외부(UI, 이벤트, 에디터 버튼 등)에서
        //                   true로 세팅하면 HP 조건 무시하고 즉시 페이즈 전환.
        //                   EnterPhaseAsync 진입 시 자동 false 리셋.
        // ─────────────────────────────────────────────────────────────────
        private bool _barrierBroken = false;
        private bool _forceNextPhase = false;

        /// <summary>
        /// true로 설정하면 WatchPhaseTransitionAsync가 HP 조건 무시하고 즉시 다음 페이즈로 전환.
        /// 페이즈 진입 시 자동으로 false 리셋됨.
        /// </summary>
        public bool ForceNextPhase
        {
            get => _forceNextPhase;
            set => _forceNextPhase = value;
        }

        /// <summary>현재 페이즈 Shield(Persistent 패턴)가 파훼되었는지 여부.</summary>
        public bool IsBarrierBroken => _barrierBroken;

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
            _phaseCts?.Cancel();
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

            // 페이즈 진입 시 플래그 초기화
            _barrierBroken = false;
            _forceNextPhase = false;

            OnPhaseChanged?.Invoke(phaseIndex);
            InjectSceneDependencies(phase);

            // Shield 파훼 이벤트 구독 (딜타임 알림 전용 — 페이즈 전환 트리거 아님)
            _blackboard.OnBarrierBroken += HandleBarrierBroken;

            var phaseCt = _phaseCts.Token;
            var patternTask = RunPhasePatternsAsync(phase, phaseCt);

            try
            {
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
                    // 마지막 페이즈: 보스 사망(HandleDeath → phaseCts.Cancel)까지 대기
                    await UniTask.WaitUntilCanceled(phaseCt);
                }
            }
            finally
            {
                _blackboard.OnBarrierBroken -= HandleBarrierBroken;
            }

            _phaseCts?.Cancel();
            await patternTask.SuppressCancellationThrow();

            Debug.Log($"[BossController] Phase {phaseIndex + 1} ended.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 페이즈 전환 감시
        //
        // [수정 핵심] Shield 파훼(_barrierBroken)를 페이즈 전환 조건에서 제거.
        //
        // 전환 조건 (둘 중 하나):
        //   1. _forceNextPhase == true  → HP 무시, 즉시 전환 (외부 bool 트리거)
        //   2. HP <= hpThreshold        → HP 조건 도달 시 전환
        //
        // Shield 파훼는 DamageHandler.BreakBarrier()로 딜타임만 제어.
        // Sequential 패턴(패턴1→2→3 순환)은 phaseCt 취소 전까지 계속 동작.
        // ─────────────────────────────────────────────────────────────────
        private async UniTask WatchPhaseTransitionAsync(
            float hpThreshold,
            CancellationToken phaseCt,
            CancellationToken lifetimeCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(phaseCt, lifetimeCt);
            var ct = linked.Token;

            while (!ct.IsCancellationRequested)
            {
                // 조건 1: 외부 강제 전환
                if (_forceNextPhase)
                {
                    Debug.Log("[BossController] ForceNextPhase triggered → 즉시 페이즈 전환.");
                    return;
                }

                // 조건 2: HP 조건
                if (_damageHandler.HPRatio <= hpThreshold)
                {
                    Debug.Log($"[BossController] HP 조건 충족 ({_damageHandler.HPRatio:P0} <= {hpThreshold:P0}) → 페이즈 전환.");
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // Shield 파훼 핸들러
        // 딜타임 플래그(_barrierBroken)만 세팅. 페이즈 전환과 완전히 무관.
        private void HandleBarrierBroken()
        {
            _barrierBroken = true;
            Debug.Log("[BossController] Barrier broken → 딜타임 시작. (페이즈 전환 아님)");
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
                await UniTask.WhenAll(tasks);
        }

        // Sequential 패턴 순환 루프
        // Shield 파훼 여부와 무관하게 phaseCt 취소 전까지 1→2→3→1→... 순환.
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
                        Debug.LogWarning("[BossController] _panelWaypoints가 비어있습니다. Inspector에서 할당해주세요.");
                    }
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

        // ─────────────────────────────────────────────────────────────────
        // 외부 API
        // ─────────────────────────────────────────────────────────────────

        public void TakeDamage(float damage) => _damageHandler.TakeDamage(damage);

        // ─────────────────────────────────────────────────────────────────
        // Editor 전용 (디버그 / 시각화)
        // ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [Header("Debug (Editor Only)")]
        [Tooltip("이 시간(초)이 지나면 강제로 다음 페이즈로 넘어갑니다. 0 = 비활성")]
        [SerializeField] private float _debugAutoSkipSeconds = 0f;

        [ContextMenu("Debug: Break Barrier (딜타임만 진입)")]
        private void Debug_BreakBarrier()
        {
            _damageHandler.ForceResetInvincibility();
            _blackboard.Debug_ForceBarrierBroken();
            Debug.Log("[Debug] Barrier force-broken. 딜타임 시작 (페이즈 전환 아님).");
        }

        [ContextMenu("Debug: Force Next Phase (즉시 페이즈 전환)")]
        private void Debug_ForceNextPhase()
        {
            _forceNextPhase = true;
            Debug.Log("[Debug] ForceNextPhase = true → 페이즈가 전환됩니다.");
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
