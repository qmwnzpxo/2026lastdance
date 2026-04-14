using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
// BossController.cs  (리팩토링)
// ── 변경 사항 ─────────────────────────────────────────────────────────────
//   - BossBlackboard를 BossContext에 주입 (PlayerVelocity 접근 경로 확보)
//   - Update()에서 Blackboard.UpdatePlayerVelocity() 매 프레임 호출
//   - 네임스페이스 BossSystem 단일 통일
//   - BossContext 생성 시 Blackboard 전달
// ═══════════════════════════════════════════════════════════════════════════

namespace BossSystem
{
    /// <summary>
    /// 보스의 최상위 컨트롤러.
    ///
    /// ── 책임 ──────────────────────────────────────────────────────────────
    ///   ① 페이즈 순서 관리 (HP 임계값 감시 → 페이즈 전환)
    ///   ② CancellationTokenSource 계층 생성 및 해제
    ///   ③ BossBlackboard / BossContext 생성 및 주입
    ///   ④ PlayerVelocity 매 프레임 갱신 (Lead Targeting 지원)
    ///
    /// ── CancellationToken 계층 ────────────────────────────────────────────
    ///   _lifetimeCts  : 보스 오브젝트 수명과 동일 (OnDestroy에서 Cancel)
    ///   _phaseCts     : 현재 페이즈 전용 (_lifetimeCts와 Linked)
    ///   페이즈 전환 시: _phaseCts.Cancel → Dispose → 새 CTS 생성 (Linked)
    /// </summary>
    [RequireComponent(typeof(DamageHandler))]
    public class BossController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform   _playerTransform;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Phase Data")]
        [Tooltip("Index 0 = 1페이즈. hpThreshold 내림차순 정렬.")]
        [SerializeField] private List<BossPhase> _phases;

        // ── 내부 컴포넌트 ─────────────────────────────────────────────────
        private DamageHandler  _damageHandler;
        private BossBlackboard _blackboard;
        private BossContext    _context;

        // ── 페이즈 상태 ───────────────────────────────────────────────────
        private int _currentPhaseIndex = -1;

        // ── CTS 계층 ──────────────────────────────────────────────────────
        private CancellationTokenSource _lifetimeCts;
        private CancellationTokenSource _phaseCts;

        // ── 이벤트 ────────────────────────────────────────────────────────
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

            // ── BossBlackboard 생성 ───────────────────────────────────────
            _blackboard = new BossBlackboard(_damageHandler.MaxHP, _playerTransform);

            // ── BossContext 생성 (Blackboard 주입) ────────────────────────
            _context = new BossContext(
                transform, _playerTransform, _damageHandler, _blackboard);
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
            Debug.Log("[BossController] All phases completed. Boss defeated.");
        }

        private async UniTask EnterPhaseAsync(int phaseIndex, CancellationToken lifetimeCt)
        {
            _currentPhaseIndex = phaseIndex;
            var phase = _phases[phaseIndex];

            Debug.Log($"[BossController] Entering Phase {phaseIndex + 1}");

            // ── 이전 페이즈 정리 ──────────────────────────────────────────
            CancelAndRecreatePhaseCts(lifetimeCt);

            // ── 페이즈 초기화 ─────────────────────────────────────────────
            _damageHandler.ForceResetInvincibility();
            if (phase.hpRecoverRatio > 0f)
                _damageHandler.RecoverHP(phase.hpRecoverRatio);

            if (phase.spawnPointIndex >= 0 && phase.spawnPointIndex < _spawnPoints.Length)
                transform.position = _spawnPoints[phase.spawnPointIndex].position;

            // DestroyedCoreCount 페이즈 전환 시 초기화
            _context.DestroyedCoreCount = 0;

            OnPhaseChanged?.Invoke(phaseIndex);

            var phaseCt = _phaseCts.Token;

            // ── 패턴 실행 ─────────────────────────────────────────────────
            var patternTask = RunPhasePatternsAsync(phase, phaseCt);

            // ── 페이즈 전환 감시 ──────────────────────────────────────────
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
                    Debug.Log($"[BossController] Phase transition. HP={_damageHandler.HPRatio:P0}");
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var sp in _spawnPoints)
                if (sp != null) Gizmos.DrawWireSphere(sp.position, 0.5f);
        }
#endif
    }
}
