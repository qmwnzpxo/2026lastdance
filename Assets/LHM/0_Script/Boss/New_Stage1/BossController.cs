using System.Collections.Generic;
using System.Threading;
using BossSystem.Patterns;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // BossController.cs  —  최상위 오케스트레이터
    //
    // ── 수정 내역 ─────────────────────────────────────────────────────────
    //   [버그 수정] LateUpdate 추가 → _blackboard.Tick() 호출
    //     BossBlackboard가 MonoBehaviour에서 일반 class로 변경되었으므로
    //     Unity 라이프사이클을 BossController가 대신 구동해야 한다.
    // ═══════════════════════════════════════════════════════════════════════

    [RequireComponent(typeof(DamageHandler))]
    public class BossController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform   _playerTransform;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Phase Data")]
        [Tooltip("Index 0 = 1페이즈. hpThreshold 내림차순 정렬.")]
        [SerializeField] private List<BossPhase> _phases;

        // ── Pool 프리로드 ─────────────────────────────────────────────────
        [Header("Pool Preload")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;
        [SerializeField] private Transform _poolRoot;

        // ── 내부 컴포넌트 ─────────────────────────────────────────────────
        private DamageHandler  _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext    _context;

        private int _currentPhaseIndex = -1;

        // ── CTS 계층 ──────────────────────────────────────────────────────
        private CancellationTokenSource _lifetimeCts;
        private CancellationTokenSource _phaseCts;

        public event System.Action<int> OnPhaseChanged;
        public event System.Action      OnBossDead;


        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _damageHandler = GetComponent<DamageHandler>();
            _damageHandler.OnDeath += HandleDeath;

            _lifetimeCts = new CancellationTokenSource();

            // BossBlackboard는 일반 class → new 로 생성 (MonoBehaviour 아님)
            _blackboard = new BossBlackboard(_damageHandler.MaxHP, _playerTransform);
            _context    = new BossContext(transform, _playerTransform, _damageHandler, _blackboard);

            PreloadPools();
        }

        private void Start()
        {
            StartBossEncounter();
        }

        private void Update()
        {
            _blackboard.UpdatePlayerVelocity();
        }

        // ── [수정] BossBlackboard.Tick() 구동 ────────────────────────────
        // BossBlackboard가 MonoBehaviour가 아니므로 여기서 직접 호출.
        // LaserBeamManager.LateUpdate 이후에 실행되도록
        // Script Execution Order에서 BossController를 LaserBeamManager보다
        // 높은 값(나중 실행)으로 설정 권장.
        private void LateUpdate()
        {
            _blackboard.Tick();
            // 매 프레임 혹은 HP 변경 시점에 페이즈 전환 체크
            CheckPhaseTransition();
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
        private void CheckPhaseTransition()
        {
            float hpRatio = _damageHandler.CurrentHP / _damageHandler.MaxHP; // (프로퍼티 이름에 맞게 수정해주세요)

            int targetIndex = -1;

            // HP 임계값에 맞는 페이즈 찾기 (내림차순 정렬 가정)
            for (int i = 0; i < _phases.Count; i++)
            {
                if (hpRatio <= _phases[i].hpThreshold)
                    targetIndex = i;
            }

            // 페이즈가 바뀌어야 한다면 실행
            if (targetIndex != -1 && targetIndex != _currentPhaseIndex)
            {
                _currentPhaseIndex = targetIndex;
                EnterPhaseAsync(_currentPhaseIndex).Forget();
            }
        }
        // ─────────────────────────────────────────────────────────────────
        // 보스 메인 루프
        // ─────────────────────────────────────────────────────────────────

        // 보스 전투 시작 시 최초 1회 호출
        public void StartBossEncounter()
        {
            // 시작할 때 HP 비율 검사를 통해 자동으로 페이즈를 시작하게 하거나
            CheckPhaseTransition();

            // 또는 강제로 0번(1페이즈)을 시작하게 함
            // _currentPhaseIndex = 0;
            // EnterPhaseAsync(0).Forget(); 
        }

        private async UniTaskVoid EnterPhaseAsync(int phaseIndex)
        {
            // 1. 기존 페이즈 패턴들 즉시 중단
            _phaseCts?.Cancel();
            _phaseCts?.Dispose();
            _phaseCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            var ct = _phaseCts.Token;
            var phase = _phases[phaseIndex];

            Debug.Log($"[Phase {phaseIndex}] 진입");

            // 초기화: 스폰 위치 이동 및 HP 회복 등 (필요 시)
            if (phase.spawnPointIndex >= 0 && _spawnPoints != null && phase.spawnPointIndex < _spawnPoints.Length)
            {
                transform.position = _spawnPoints[phase.spawnPointIndex].position;
            }

            // ==========================================================
            // 2. Persistent 패턴 (병렬 실행)
            // ==========================================================
            // 페이즈가 끝날 때까지 백그라운드에서 계속 돌아야 함
            foreach (var pattern in phase.persistentPatterns)
            {
                if (pattern != null)
                    pattern.ExecuteAsync(_context, ct).Forget(); // await 하지 않음
            }

            // ==========================================================
            // 3. Sequential 패턴 (순차 루프 실행)
            // ==========================================================
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    foreach (var pattern in phase.sequentialPatterns)
                    {
                        if (pattern == null) continue;

                        // 하나의 패턴이 완전히 끝날 때까지 대기
                        await pattern.ExecuteAsync(_context, ct);
                    }
                    // 모든 패턴 한 바퀴 돌고 잠시 대기 (무한 루프 방지)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (System.OperationCanceledException)
            {
                // 페이즈 전환으로 인한 취소는 정상적인 흐름
            }
        }

        private async UniTask RunPhasePatternsAsync(BossPhase phase, CancellationToken phaseCt)
        {
            _phaseCts?.Cancel();
            _phaseCts?.Dispose();

            _phaseCts = new CancellationTokenSource();
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

        private async UniTask WatchPhaseTransitionAsync(
            float hpThreshold, CancellationToken phaseCt, CancellationToken lifetimeCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(phaseCt, lifetimeCt);
            var ct = linked.Token;

            while (!ct.IsCancellationRequested)
            {
                if (_damageHandler.HPRatio <= hpThreshold)
                {
                    Debug.Log($"[BossController] Phase transition at HP={_damageHandler.HPRatio:P0}");
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
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

        /// <summary>
        /// BossPatternInjector가 씬 참조를 SO 패턴에 주입할 때 사용.
        /// phases 리스트를 읽기 전용으로 노출.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<BossPhase> GetPhasesForInjection()
            => _phases;

#if UNITY_EDITOR
        [ContextMenu("Debug: Break Barrier")]
        private void Debug_BreakBarrier()
        {
            _damageHandler.ForceResetInvincibility();
            Debug.Log("[Debug] Barrier force-broken.");
        }

        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var sp in _spawnPoints)
                if (sp != null) Gizmos.DrawWireSphere(sp.position, 0.5f);
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
