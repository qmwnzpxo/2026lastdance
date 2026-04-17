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

            _blackboard = new BossBlackboard(_damageHandler.MaxHP, _playerTransform);
            _context = new BossContext(transform, _playerTransform, _damageHandler, _blackboard);

            _damageHandler.OnHPChanged += (current, max) =>
            {
                // 깎인 만큼 블랙보드에도 반영 (TakeDamage 등의 방식으로 직접 조작하거나)
                // 가장 직관적인 방법은 Blackboard에 SetHP 메서드를 만들어서 동기화하는 것입니다.
            };

            // 혹은 CheckPhaseTransition() 자체를 블랙보드가 아닌 데미지 핸들러의 HP를 보도록 수정하는 것이 제일 깔끔합니다!
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
        // 2. 페이즈 전환 루프: 조건을 좀 더 엄격하게 체크
        private void CheckPhaseTransition()
        {
            // 1. 가장 확실한 진짜 체력 비율을 가져옵니다. (MaxHP 0 나누기 방어)
            if (_damageHandler.MaxHP <= 0f) return;
            float hpRatio = _damageHandler.CurrentHP / _damageHandler.MaxHP;

            // 2. 마지막 페이즈부터 역순으로 검사합니다.
            // 핵심 조건: i > _currentPhaseIndex
            // 현재 진행 중이거나 이미 지나간 페이즈는 아예 검사조차 하지 않습니다!
            for (int i = _phases.Count - 1; i > _currentPhaseIndex; i--)
            {
                // 다음 페이즈의 체력 조건에 도달했다면?
                if (hpRatio <= _phases[i].hpThreshold)
                {
                    _currentPhaseIndex = i;
                    EnterPhaseAsync(_currentPhaseIndex).Forget();

                    // 더 이상 검사할 필요가 없으니 즉시 루프 탈출
                    break;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 보스 메인 루프
        // ─────────────────────────────────────────────────────────────────

        // 보스 전투 시작 시 최초 1회 호출
        public void StartBossEncounter()
        {
            if (_phases == null || _phases.Count == 0) return;

            _currentPhaseIndex = 0; // 무조건 0번 인덱스(1페이즈) 고정
            EnterPhaseAsync(0).Forget();
        }
        private async UniTaskVoid EnterPhaseAsync(int phaseIndex)
        {
            // [중요] 이미 해당 페이즈가 실행 중이라면 중복 실행 방지
            // (이게 없으면 LateUpdate에서 매 프레임 Cancel을 때리게 됩니다)
            // 단, 디버그를 위해 처음 한 번은 넘어가도록 설계해야 함

            _phaseCts?.Cancel();
            _phaseCts?.Dispose();
            _phaseCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var ct = _phaseCts.Token;

            var phase = _phases[phaseIndex];
            if (phase == null) return;

            Debug.Log($"<color=cyan>[Phase {phaseIndex}] 패턴 그룹 기동 시작</color>");

            // 1. 상시 패턴 실행 (Forget으로 던짐)
            foreach (var p in phase.persistentPatterns)
            {
                if (p != null) p.ExecuteAsync(_context, ct).Forget();
            }

            // 2. 순차 패턴 루프 (안전 장치 추가)
            try
            {
                // 루프 진입 전 로그를 찍어 확인
                Debug.Log($"<color=orange>순차 패턴 루프 진입 성공! (패턴 수: {phase.sequentialPatterns.Count})</color>");

                while (!ct.IsCancellationRequested)
                {
                    if (phase.sequentialPatterns.Count == 0)
                    {
                        await UniTask.Yield(ct); // 리스트가 비어있을 때 프리징 방지
                        continue;
                    }

                    foreach (var pattern in phase.sequentialPatterns)
                    {
                        await pattern.ExecuteAsync(_context, ct);
                    }
                    await UniTask.Yield(ct); // 한 바퀴 돌면 양보
                }
            }
            catch (System.OperationCanceledException) { /* 정상 종료 */ }
            catch (System.Exception e) { Debug.LogError($"패턴 에러: {e}"); }
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
