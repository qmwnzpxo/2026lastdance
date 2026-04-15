using BossSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Boss.Phase1
{
    /// <summary>
    /// 레이저 코어 간 빔 연결을 관리하는 컴포넌트.
    ///
    /// [수정 v2]
    ///   1. LateUpdate 코어 순회 시 null / _isDead 이중 검사
    ///   2. RebuildBeams도 유효 코어만 카운트
    ///   3. BossBlackboard의 FlushPendingRemovals 이후에 LateUpdate가
    ///      호출되므로 _aliveCores는 항상 최신 상태
    ///   ※ LateUpdate 실행 순서:
    ///      BossBlackboard.LateUpdate (Remove 처리) → LaserBeamManager.LateUpdate
    ///      (Script Execution Order로 BossBlackboard를 더 낮은 값으로 설정 권장)
    /// </summary>
    public class LaserBeamManager : MonoBehaviour
    {

        [Header("Beam Visual")]
        [SerializeField] private Material _laserMaterial;
        [SerializeField] private float _beamWidth = 0.15f;
        [SerializeField] private Color _beamColor = Color.cyan;

        [Header("Damage")]
        [SerializeField] private float _damagePerSecond = 20f;
        [SerializeField] private float _castHalfWidth   = 0.06f;
        [SerializeField] private LayerMask _playerLayer;

        private BossBlackboard _blackboard;
        private readonly LineRenderer[] _beamRenderers = new LineRenderer[4];
        private Vector3[] _corePositionCache = new Vector3[4];
        private bool _isActive;

        // ─────────────────────────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────────────────────────

        public void Initialize(BossBlackboard blackboard)
        {
            _blackboard = blackboard;
            _blackboard.OnCoreListChanged += RebuildBeams;
            _blackboard.OnBarrierBroken   += DeactivateAllBeams;

            for (int i = 0; i < _beamRenderers.Length; i++)
            {
                var go = new GameObject($"LaserBeam_{i}");
                go.transform.SetParent(transform);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth    = _beamWidth;
                lr.endWidth      = _beamWidth;
                lr.material      = _laserMaterial;
                lr.startColor    = _beamColor;
                lr.endColor      = _beamColor;
                lr.enabled       = false;

                _beamRenderers[i] = lr;
            }

            _isActive = true;
            RebuildBeams();
        }

        private void OnDestroy()
        {
            if (_blackboard != null)
            {
                _blackboard.OnCoreListChanged -= RebuildBeams;
                _blackboard.OnBarrierBroken   -= DeactivateAllBeams;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // LateUpdate
        // ─────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_isActive) return;

            var cores = _blackboard.AliveCores;
            int count = cores.Count;

            // ── [수정 1] 유효 코어만 카운트 ──────────────────────────────
            // BossBlackboard.LateUpdate가 먼저 실행되어 _pendingRemove를
            // 이미 플러시했더라도, Destroy()는 아직 프레임 말에 처리 중일 수 있다.
            // null 체크로 파괴 직후 코어를 방어한다.
            int validCount = 0;
            for (int i = 0; i < count; i++)
                if (cores[i] != null) validCount++;

            if (validCount < 2)
            {
                DisableBeamsFrom(0);
                return;
            }

            // ── 빔 위치 갱신 ─────────────────────────────────────────────
            for (int i = 0; i < count; i++)
            {
                // ── [수정 1] null 방어 ─────────────────────────────────
                if (cores[i] == null) continue;

                var lr = _beamRenderers[i];
                if (lr == null || !lr.enabled) continue;

                int nextIdx = GetNextValidIndex(cores, i, count);
                if (nextIdx < 0) continue; // 유효한 다음 코어 없음

                Vector3 from = cores[i].Position;
                Vector3 to   = cores[nextIdx].Position;

                lr.SetPosition(0, from);
                lr.SetPosition(1, to);

                _corePositionCache[i]       = from;
                _corePositionCache[nextIdx] = to;
            }

            ApplyLaserDamage(cores);
        }

        /// <summary>
        /// i 이후 첫 번째 null이 아닌 코어 인덱스를 순환 탐색.
        /// 모두 null이면 -1 반환.
        /// </summary>
        private static int GetNextValidIndex(IReadOnlyList<LaserCore> cores, int from, int count)
        {
            for (int offset = 1; offset < count; offset++)
            {
                int idx = (from + offset) % count;
                if (cores[idx] != null) return idx;
            }
            return -1;
        }

        // ─────────────────────────────────────────────────────────────────
        // Circular Link 재구성
        // ─────────────────────────────────────────────────────────────────

        private void RebuildBeams()
        {
            var cores = _blackboard.AliveCores;

            // ── [수정 1] null 코어를 제외한 실제 유효 수로 빔 활성화 결정
            int validCount = 0;
            foreach (var c in cores)
                if (c != null) validCount++;

            Debug.Log($"[BeamManager] Rebuild. Valid cores={validCount}");

            for (int i = 0; i < _beamRenderers.Length; i++)
                if (_beamRenderers[i] != null)
                    _beamRenderers[i].enabled = (i < validCount && validCount >= 2);
        }

        private void DeactivateAllBeams()
        {
            _isActive = false;
            DisableBeamsFrom(0);
            Debug.Log("[BeamManager] All beams deactivated.");
        }

        private void DisableBeamsFrom(int startIndex)
        {
            for (int i = startIndex; i < _beamRenderers.Length; i++)
                if (_beamRenderers[i] != null)
                    _beamRenderers[i].enabled = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // 레이저 판정 (BoxCast)
        // ─────────────────────────────────────────────────────────────────

        private void ApplyLaserDamage(IReadOnlyList<LaserCore> cores)
        {
            int count = cores.Count;
            if (count < 2) return;

            float damage  = _damagePerSecond * Time.deltaTime;
            var castSize  = new Vector2(_castHalfWidth * 2f, _castHalfWidth * 2f);

            for (int i = 0; i < count; i++)
            {
                // ── [수정 1] null 방어 ─────────────────────────────────
                if (cores[i] == null) continue;
                if (_beamRenderers[i] == null || !_beamRenderers[i].enabled) continue;

                int nextIdx = GetNextValidIndex(cores, i, count);
                if (nextIdx < 0 || cores[nextIdx] == null) continue;

                Vector3 from = cores[i].Position;
                Vector3 to   = cores[nextIdx].Position;
                Vector3 dir  = to - from;
                float   dist = dir.magnitude;

                if (dist < 0.01f) continue;

                var hit = Physics2D.BoxCast(
                    origin:    from,
                    size:      castSize,
                    angle:     Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                    direction: dir.normalized,
                    distance:  dist,
                    layerMask: _playerLayer
                );

                if (hit.collider != null)
                    hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(damage);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_blackboard == null) return;
            var cores = _blackboard.AliveCores;
            int count = cores.Count;
            if (count < 2) return;

            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            for (int i = 0; i < count; i++)
            {
                if (cores[i] == null) continue;
                int next = GetNextValidIndex(cores, i, count);
                if (next < 0 || cores[next] == null) continue;
                Gizmos.DrawLine(cores[i].Position, cores[next].Position);
            }
        }
#endif
    }

    public interface IPlayerHealth { void TakeDamage(float damage); }
}
