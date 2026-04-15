using System;
using System.Threading;
using BossSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Boss.Phase1
{
    // ═══════════════════════════════════════════════════════════════════════
    // SniperPanelSystem.cs  —  저격 판넬 시스템
    //
    // ── 위치 확정 ─────────────────────────────────────────────────────────
    //   BossPhase1Controller.cs가 제거되면서 이 파일이 판넬 시스템의
    //   유일한 MonoBehaviour 구현체가 된다.
    //   BossController.cs 에서 Initialize(_blackboard) + StartLoop(ct) 호출.
    //
    // ── 기획 의도 ─────────────────────────────────────────────────────────
    //   판넬 N개가 panelTransforms[] 위치를 순환.
    //   currentOffset += 1 마다 모든 판넬이 "다음 슬롯"으로 이동.
    //   판넬 i의 목표 = panelTransforms[(i + offset) % N]
    //
    // ── 실행 흐름 ─────────────────────────────────────────────────────────
    //   StartLoop(ct)
    //     PanelSpawnAsync   : 보스 위치에서 순차 활성화
    //     MoveToSlotsAsync  : 초기 슬롯으로 이동
    //     AttackLoopAsync   : (Lock-on 경고 → 발사 → offset+1 → 이동) 반복
    // ═══════════════════════════════════════════════════════════════════════
    public class SniperPanelSystem : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("판넬 오브젝트 배열.")]
        [SerializeField] private GameObject[] _panels;

        [Tooltip("판넬이 순환 이동할 슬롯 위치 배열. _panels와 길이가 같아야 함.")]
        [SerializeField] private Transform[] _panelTransforms;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed     = 4f;
        [SerializeField] private float _spawnInterval = 0.2f;

        [Header("Warning Line")]
        [SerializeField] private LineRenderer[] _lineRenderers;
        [SerializeField] private float _warningDuration = 2f;
        [SerializeField] private Color _warningColor    = new Color(1f, 0.3f, 0f, 1f);
        [SerializeField] private Color _fireColor       = Color.red;
        [SerializeField] private float _lineWidth       = 0.05f;

        [Header("Projectile")]
        [Tooltip("발사할 투사체 프리팹 (PanelProjectile 컴포넌트 필요)")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float      _projectileSpeed    = 5f;
        [SerializeField] private float      _projectileLifetime = 5f;

        // ── 런타임 상태 ──────────────────────────────────────────────────
        private BossBlackboard _blackboard;
        private bool           _initialized;
        private bool[]         _isDestroyed;
        private int            _currentOffset;

        public bool IsPanelCleared { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // 초기화 API
        // ─────────────────────────────────────────────────────────────────

        public void Initialize(BossBlackboard blackboard)
        {
            _blackboard  = blackboard;
            _initialized = true;
        }

        public void StartLoop(CancellationToken ct)
        {
            if (!_initialized)
            {
                Debug.LogError("[SniperPanel] Initialize() 를 먼저 호출해야 합니다.");
                return;
            }
            if (_panels == null || _panels.Length == 0) { Debug.LogWarning("[SniperPanel] _panels 비어있음."); return; }
            if (_panelTransforms == null || _panelTransforms.Length != _panels.Length)
            {
                Debug.LogWarning("[SniperPanel] _panelTransforms 길이가 _panels와 다릅니다.");
                return;
            }

            int n = _panels.Length;
            _isDestroyed   = new bool[n];
            _currentOffset = 0;
            IsPanelCleared = false;

            EnsureLineRenderers(n);
            HideAllLines();

            foreach (var p in _panels)
                if (p != null) p.SetActive(false);

            RunAsync(ct).Forget();
        }

        /// <summary>외부(판넬 HP 스크립트)에서 파괴 발생 시 호출.</summary>
        public void NotifyDestroyed(int index)
        {
            if (index < 0 || index >= _isDestroyed.Length) return;
            _isDestroyed[index] = true;
            Debug.Log($"[SniperPanel] Panel {index} destroyed.");

            bool allDead = true;
            for (int i = 0; i < _isDestroyed.Length; i++)
                if (!_isDestroyed[i]) { allDead = false; break; }

            if (allDead)
            {
                IsPanelCleared = true;
                Debug.Log("[SniperPanel] All panels cleared.");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 메인 비동기 루프
        // ─────────────────────────────────────────────────────────────────

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                await PanelSpawnAsync(ct);
                await MoveToSlotsAsync(_currentOffset, ct);
                await AttackLoopAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[SniperPanel] Loop cancelled.");
            }
            finally
            {
                Cleanup();
            }
        }

        private async UniTask PanelSpawnAsync(CancellationToken ct)
        {
            Vector3 bossPos = transform.position;
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] == null || _isDestroyed[i]) continue;
                _panels[i].transform.position = bossPos;
                _panels[i].SetActive(true);
                await UniTask.Delay(TimeSpan.FromSeconds(_spawnInterval), cancellationToken: ct);
            }
        }

        /// <summary>
        /// 판넬 i의 목표 = _panelTransforms[(i + offset) % N]
        /// 살아있는 판넬이 모두 도달할 때까지 반복.
        /// </summary>
        private async UniTask MoveToSlotsAsync(int offset, CancellationToken ct)
        {
            int    n       = _panels.Length;
            var    arrived = new bool[n];

            for (int i = 0; i < n; i++)
                if (_isDestroyed[i] || _panels[i] == null) arrived[i] = true;

            while (!AllArrived(arrived) && !ct.IsCancellationRequested)
            {
                for (int i = 0; i < n; i++)
                {
                    if (arrived[i]) continue;
                    int     slotIndex = (i + offset) % n;
                    Vector2 current   = _panels[i].transform.position;
                    Vector2 target    = _panelTransforms[slotIndex].position;

                    _panels[i].transform.position = Vector2.MoveTowards(
                        current, target, _moveSpeed * Time.deltaTime);

                    if (Vector2.Distance(current, target) < 0.01f)
                    {
                        _panels[i].transform.position = target;
                        arrived[i] = true;
                    }
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private async UniTask AttackLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !IsPanelCleared)
            {
                if (_blackboard?.PlayerTransform == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    continue;
                }

                await ShowWarningLinesAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (IsPanelCleared) break;

                FireAll(_blackboard.PlayerPosition);
                HideAllLines();

                _currentOffset = (_currentOffset + 1) % _panels.Length;
                await MoveToSlotsAsync(_currentOffset, ct);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 경고선 (페이드인 + 플레이어 추적)
        // ─────────────────────────────────────────────────────────────────

        private async UniTask ShowWarningLinesAsync(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < _panels.Length; i++)
                    if (_lineRenderers[i] != null && !_isDestroyed[i])
                        _lineRenderers[i].enabled = true;

                float elapsed = 0f;
                while (elapsed < _warningDuration && !ct.IsCancellationRequested)
                {
                    float alpha = Mathf.Lerp(0f, _warningColor.a, elapsed / _warningDuration);
                    for (int i = 0; i < _panels.Length; i++)
                    {
                        if (_lineRenderers[i] == null || !_lineRenderers[i].enabled) continue;
                        if (_isDestroyed[i]) { _lineRenderers[i].enabled = false; continue; }

                        Color c = _warningColor;
                        c.a = alpha;
                        _lineRenderers[i].startColor = c;
                        _lineRenderers[i].endColor   = c;
                        _lineRenderers[i].SetPosition(0, _panels[i].transform.position);
                        _lineRenderers[i].SetPosition(1, _blackboard.PlayerPosition);
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // 완전 불투명 고정
                for (int i = 0; i < _panels.Length; i++)
                {
                    if (_lineRenderers[i] == null || !_lineRenderers[i].enabled) continue;
                    _lineRenderers[i].startColor = _warningColor;
                    _lineRenderers[i].endColor   = _warningColor;
                }
            }
            catch (OperationCanceledException)
            {
                HideAllLines();
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 발사
        // ─────────────────────────────────────────────────────────────────

        private void FireAll(Vector3 targetPos)
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] == null || _isDestroyed[i]) continue;
                FireProjectile(_panels[i].transform.position, targetPos);
            }
        }

        private void FireProjectile(Vector3 from, Vector3 to)
        {
            if (_projectilePrefab == null) return;
            GameObject proj = Instantiate(_projectilePrefab, from, Quaternion.identity);
            var projectile = proj.GetComponent<PanelProjectile>();
            if (projectile != null)
                projectile.Init((Vector2)to, _projectileSpeed);
            Destroy(proj, _projectileLifetime);
        }

        // ─────────────────────────────────────────────────────────────────
        // 유틸
        // ─────────────────────────────────────────────────────────────────

        private void HideAllLines()
        {
            if (_lineRenderers == null) return;
            foreach (var lr in _lineRenderers)
                if (lr != null) lr.enabled = false;
        }

        private void Cleanup()
        {
            HideAllLines();
            if (_panels != null)
                foreach (var p in _panels)
                    if (p != null) p.SetActive(false);
        }

        private static bool AllArrived(bool[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
                if (!arr[i]) return false;
            return true;
        }

        private void EnsureLineRenderers(int n)
        {
            if (_lineRenderers == null || _lineRenderers.Length != n)
                _lineRenderers = new LineRenderer[n];

            for (int i = 0; i < n; i++)
            {
                if (_lineRenderers[i] != null) continue;
                if (_panels[i] == null) continue;

                var lr = _panels[i].GetComponent<LineRenderer>()
                      ?? _panels[i].AddComponent<LineRenderer>();

                lr.positionCount = 2;
                lr.startWidth    = _lineWidth;
                lr.endWidth      = _lineWidth;
                lr.useWorldSpace = true;
                lr.material      = new Material(Shader.Find("Sprites/Default"));
                lr.startColor    = _warningColor;
                lr.endColor      = _warningColor;
                lr.enabled       = false;

                _lineRenderers[i] = lr;
            }
        }

        private void OnDestroy() => Cleanup();

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_panelTransforms == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _panelTransforms.Length; i++)
            {
                if (_panelTransforms[i] == null) continue;
                Gizmos.DrawWireSphere(_panelTransforms[i].position, 0.3f);
                int next = (i + 1) % _panelTransforms.Length;
                if (_panelTransforms[next] != null)
                    Gizmos.DrawLine(_panelTransforms[i].position, _panelTransforms[next].position);
            }
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────────────
    // PanelProjectile  —  판넬에서 발사되는 투사체 (신규 추가)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SniperPanelSystem에서 FireProjectile이 호출하는 투사체 컴포넌트.
    /// 기존 코드에서 GetComponent&lt;PanelProjectile&gt;()를 호출했으나
    /// 클래스 정의가 누락되어 컴파일 오류가 발생하던 부분을 추가.
    /// </summary>
    public class PanelProjectile : MonoBehaviour
    {
        private Vector2 _direction;
        private float   _speed;
        private bool    _initialized;

        /// <summary>발사 시 방향과 속도를 초기화.</summary>
        public void Init(Vector2 targetPos, float speed)
        {
            _direction   = (targetPos - (Vector2)transform.position).normalized;
            _speed       = speed;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            transform.Translate(_direction * _speed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            //other.GetComponent<LaserBeamManager.IPlayerHealth>()?.TakeDamage(10f);
            Destroy(gameObject);
        }
    }
}
