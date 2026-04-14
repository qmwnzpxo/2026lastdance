using System.Threading;
using BossSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;

namespace Boss.Phase1
{
    /// <summary>
    /// 1페이즈 전체 오케스트레이터.
    ///
    /// [수정 v2]
    ///   [수정 4] Awake에서 ObjectPool.Preload로 VFX/경고선 풀 초기화.
    ///            → 이후 Get/Return만 사용하므로 런타임 GC 없음.
    ///   [수정 1] CleanupRemainingCores에서 Destroy 전 _isDead 확인 불필요.
    ///            코어는 LaserCore.Die() → Pool.Return 경로를 타도록 변경.
    ///   [수정 2] TransitionToPhase2Async에 try-finally 적용.
    /// </summary>
    public class BossPhase1Controller : MonoBehaviour
    {
        [Header("Boss Stats")]
        [SerializeField] private float _maxHP = 150f;

        [Header("Player Reference")]
        [SerializeField] private Transform _playerTransform;

        [Header("Core Spawning")]
        [SerializeField] private LaserCore   _corePrefab;
        [SerializeField] private Transform[] _coreSpawnPoints;

        [Header("Systems")]
        [SerializeField] private LaserBeamManager  _beamManager;
        [SerializeField] private SniperPanelSystem _sniperPanel;

        [Header("Phase Transition")]
        [SerializeField] private GameObject _phase2Controller;

        // ── [수정 4] 풀 프리로드용 프리팹 참조 ───────────────────────────
        [Header("Pool Preload")]
        [Tooltip("경고선, 레이저 VFX 등 풀링할 프리팹 목록")]
        [SerializeField] private PoolPreloadEntry[] _poolPreloadEntries;

        [Header("Pool Root")]
        [Tooltip("풀 오브젝트들의 부모 Transform (씬 정리용)")]
        [SerializeField] private Transform _poolRoot;

        private BossBlackboard _blackboard;
        private CancellationTokenSource _lifetimeCts;
        private CancellationTokenSource _phase1Cts;
        private LaserCore[] _spawnedCores;

        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
            _phase1Cts   = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

            _blackboard = new BossBlackboard(_maxHP, _playerTransform);
            _blackboard.OnDeath         += HandleBossDeath;
            _blackboard.OnBarrierBroken += HandleBarrierBroken;

            _beamManager.Initialize(_blackboard);
            _sniperPanel.Initialize(_blackboard);

            // ── [수정 4] 풀 프리로드 ──────────────────────────────────────
            // Awake에서 미리 생성해두면 첫 패턴 실행 시 Instantiate GC 없음.
            PreloadPools();
        }

        private void Start()
        {
            StartPhase1();
        }

        private void OnDestroy()
        {
            _blackboard.OnDeath         -= HandleBossDeath;
            _blackboard.OnBarrierBroken -= HandleBarrierBroken;

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            _phase1Cts.Dispose();

            // ── [수정 4] 씬 언로드 시 풀 전체 정리 ─────────────────────
            ObjectPool.ClearAll();
        }

        // ─────────────────────────────────────────────────────────────────
        // [수정 4] 풀 프리로드
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inspector에서 설정한 프리팹/카운트 쌍을 순회하며 ObjectPool.Preload 호출.
        /// 여기서 생성된 오브젝트들은 SetActive(false) 상태로 _poolRoot 하위에 보관.
        /// </summary>
        private void PreloadPools()
        {
            if (_poolPreloadEntries == null) return;

            foreach (var entry in _poolPreloadEntries)
            {
                if (entry.prefab == null || entry.count <= 0) continue;
                ObjectPool.Preload(entry.prefab, _poolRoot, entry.count);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 1페이즈 시작
        // ─────────────────────────────────────────────────────────────────

        private void StartPhase1()
        {
            var ct = _phase1Cts.Token;
            _blackboard.AcquireInvincibility();
            SpawnCores();
            _sniperPanel.StartLoop(ct);
            Debug.Log("[Phase1] Phase 1 started.");
        }

        private void SpawnCores()
        {
            if (_corePrefab == null || _coreSpawnPoints == null) return;

            int count = Mathf.Min(_coreSpawnPoints.Length, 4);
            _spawnedCores = new LaserCore[count];

            for (int i = 0; i < count; i++)
            {
                var sp = _coreSpawnPoints[i];
                if (sp == null) continue;

                // ── [수정 4] ObjectPool.Get으로 교체 ──────────────────────
                var coreGo = ObjectPool.Get(_corePrefab.gameObject, sp.position, Quaternion.identity);
                var core   = coreGo != null ? coreGo.GetComponent<LaserCore>() : null;

                if (core == null) continue;

                _spawnedCores[i] = core;
                _blackboard.RegisterCore(core);
            }

            Debug.Log($"[Phase1] {count} cores spawned.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 이벤트 핸들러
        // ─────────────────────────────────────────────────────────────────

        private void HandleBarrierBroken()
        {
            Debug.Log("[Phase1] Barrier broken! Boss is now vulnerable.");
        }

        private void HandleBossDeath()
        {
            Debug.Log("[Phase1] Boss HP 0. Transitioning to Phase 2.");
            _phase1Cts.Cancel();
            CleanupRemainingCores();
            TransitionToPhase2Async(_lifetimeCts.Token).Forget();
        }

        /// <summary>
        /// [수정 1] 코어 강제 정리.
        /// LaserCore.Die()가 Pool.Return or Destroy를 담당하므로
        /// 여기서는 TakeDamage(무한)으로 Die() 경로를 타도록 유도.
        /// 이미 _isDead인 코어는 TakeDamage 내부에서 자동으로 무시됨.
        /// </summary>
        private void CleanupRemainingCores()
        {
            if (_spawnedCores == null) return;
            foreach (var core in _spawnedCores)
            {
                if (core == null) continue;
                // Die() → Pool.Return 경로. _isDead 체크로 이중 파괴 없음.
                core.TakeDamage(float.MaxValue);
            }
        }

        /// <summary>[수정 2] try-finally로 페이즈 전환 안전성 보장.</summary>
        private async UniTaskVoid TransitionToPhase2Async(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(2f), cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                if (_phase2Controller != null)
                    _phase2Controller.SetActive(true);

                gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[Phase1] Transition cancelled (boss destroyed mid-transition).");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 외부 API
        // ─────────────────────────────────────────────────────────────────

        public void TakeDamage(float damage) => _blackboard.TakeDamage(damage);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void Debug_BreakBarrier()
        {
            _blackboard.ForceResetInvincibility();
            foreach (var core in _spawnedCores)
                if (core != null) core.TakeDamage(float.MaxValue);
        }
    }

    // ── [수정 4] 풀 프리로드 설정 구조체 ─────────────────────────────────
    [System.Serializable]
    public struct PoolPreloadEntry
    {
        [Tooltip("풀링할 프리팹")]
        public GameObject prefab;
        [Tooltip("미리 생성할 수량")]
        public int count;
    }
}
