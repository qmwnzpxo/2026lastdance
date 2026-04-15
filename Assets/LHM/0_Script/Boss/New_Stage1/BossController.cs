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
    // ── 중복 제거 내역 ─────────────────────────────────────────────────────
    //   Before: BossPhase1Controller.cs 가 별도 오케스트레이터로 존재.
    //           페이즈 시작/전환/CTS 관리 로직이 두 곳에 분산되어 있었음.
    //   After:  BossController 단일 오케스트레이터.
    //           BossPhase1Controller 삭제 (Pool 프리로드는 이 파일로 흡수).
    //
    // ── CancellationToken 계층 ────────────────────────────────────────────
    //   _lifetimeCts  : 보스 오브젝트 수명 (OnDestroy에서 Cancel)
    //   _phaseCts     : 현재 페이즈 전용 (_lifetimeCts와 Linked)
    //   페이즈 전환 시: _phaseCts.Cancel → Dispose → 새 CTS 생성 (Linked)
    //
    // ── ObjectPool 프리로드 ────────────────────────────────────────────────
    //   기존 BossPhase1Controller에서 담당하던 Pool.Preload 로직을 흡수.
    //   _poolPreloadEntries를 Inspector에 등록하면 Awake에서 일괄 프리로드.
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

        

        // ── Pool 프리로드 (BossPhase1Controller에서 흡수) ──────────────────
        [Header("Pool Preload")]
        [Tooltip("경고선, 레이저 VFX 등 풀링할 프리팹 목록")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;

        [Tooltip("풀 오브젝트들의 부모 Transform (씬 정리용)")]
        [SerializeField] private Transform _poolRoot;

        // ── 내부 컴포넌트 ─────────────────────────────────────────────────
        private DamageHandler  _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext    _context;

        private BossPatternBase _pattern;
        Phase1PanelPattern _phase1PanelPattern;

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

            _blackboard = new BossBlackboard(_damageHandler.MaxHP, _playerTransform);
            _context    = new BossContext(transform, _playerTransform, _damageHandler, _blackboard);

            Debug.Log("try setpo");

            // Pool 프리로드 (런타임 첫 Get의 GC 스파이크 제거)
            PreloadPools();
        }

        private void Start()
        {
            StartBossAsync(_lifetimeCts.Token).Forget();
        }

        private void Update()
        {
            // Lead Targeting을 위한 플레이어 속도 매 프레임 갱신
            _blackboard.UpdatePlayerVelocity();
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
            OnPhaseChanged?.Invoke(phaseIndex);

            var phaseCt = _phaseCts.Token;

            var patternTask = RunPhasePatternsAsync(phase, phaseCt);

            // 마지막 페이즈가 아니면 HP 임계값 감시
            if (phaseIndex < _phases.Count - 1)
            {
                var nextPhase = _phases[phaseIndex + 1];
                await WatchPhaseTransitionAsync(nextPhase.hpThreshold, phaseCt, lifetimeCt);
            }
            else
            {
                await UniTask.WaitUntilCanceled(phaseCt);
            }

            _phaseCts?.Cancel();
            await patternTask.SuppressCancellationThrow();

            Debug.Log($"[BossController] Phase {phaseIndex + 1} ended.");
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

    // ── Pool 프리로드 설정 구조체 (BossPhase1Controller에서 이동) ──────────
    [System.Serializable]
    public struct PoolPreloadEntry
    {
        [Tooltip("풀링할 프리팹")]
        public GameObject Prefab;
        [Tooltip("미리 생성할 수량")]
        public int Count;
    }
}
