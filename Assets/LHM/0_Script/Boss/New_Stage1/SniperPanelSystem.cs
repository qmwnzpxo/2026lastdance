using System.Threading;
using BossSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;

namespace Boss.Phase1
{
    /// <summary>
    /// 저격 판넬 시스템.
    ///
    /// [수정 v2]
    ///   1. RunPanelLoopAsync 전체를 try-catch(OperationCanceledException)-finally로 감쌈.
    ///      → 페이즈 전환/CancellationToken 취소 시 경고선이 화면에 잔류하는 버그 해결.
    ///   2. AimAtPlayer의 _bossBlackboard 오타 수정 (_blackboard로 통일).
    ///   3. UpdateWarningLinePositions의 _blackboard null 체크 통일.
    ///   4. 각 비동기 스텝에도 개별 try-finally로 LineRenderer 즉시 정리 보장.
    /// </summary>
    public class SniperPanelSystem : MonoBehaviour
    {
        [Header("Waypoints")]
        [SerializeField] private Transform[] _waypoints;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;

        [Header("Warning Line")]
        [SerializeField] private LineRenderer _warningLine;
        [SerializeField] private float _warningDuration = 2f;
        [SerializeField] private Color _warningColorFull = new Color(1f, 0.3f, 0f, 1f);

        [Header("Laser Fire")]
        [SerializeField] private float _fireDuration    = 0.25f;
        [SerializeField] private float _castRadius      = 0.2f;
        [SerializeField] private float _castMaxDistance = 30f;
        [SerializeField] private LayerMask _playerLayer;
        [SerializeField] private float _laserDamage     = 25f;

        [Header("Visual")]
        [SerializeField] private Color _fireColor = Color.red;
        [SerializeField] private ParticleSystem _muzzleVfx;

        private BossBlackboard _blackboard; // [수정 2] _bossBlackboard → _blackboard 통일
        private int  _currentWaypointIndex;
        private bool _initialized;
        private Color _warningColorCache;

        // ─────────────────────────────────────────────────────────────────
        // 초기화
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
                Debug.LogError("[SniperPanel] Not initialized. Call Initialize() first.");
                return;
            }

            if (_waypoints == null || _waypoints.Length == 0)
            {
                Debug.LogWarning("[SniperPanel] No waypoints assigned.");
                return;
            }

            if (_warningLine != null)
                _warningLine.enabled = false;

            RunPanelLoopAsync(ct).Forget();
        }

        // ─────────────────────────────────────────────────────────────────
        // 메인 비동기 루프
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// [수정 2] 전체 루프를 try-catch-finally로 감싸서 취소 시 잔류 리소스 제거.
        ///
        /// OperationCanceledException이 발생하는 경우:
        ///   - 페이즈 전환으로 _phase1Cts가 취소될 때
        ///   - 보스 사망으로 _lifetimeCts가 취소될 때
        ///   모든 경우 finally 블록에서 경고선 / VFX가 반드시 정리된다.
        /// </summary>
        private async UniTaskVoid RunPanelLoopAsync(CancellationToken ct)
        {
            _currentWaypointIndex = 0;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var targetWaypoint = _waypoints[_currentWaypointIndex];
                    await MoveToWaypointAsync(targetWaypoint.position, ct);

                    ct.ThrowIfCancellationRequested();

                    AimAtPlayer();

                    await ShowWarningLineAsync(ct);

                    ct.ThrowIfCancellationRequested();

                    await FireLaserAsync(ct);

                    HideWarningLine();
                    AdvanceWaypoint();
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소 경로: 예외를 삼키고 finally에서 정리
                Debug.Log("[SniperPanel] Loop cancelled.");
            }
            finally
            {
                // ── [수정 2] 취소 여부와 무관하게 반드시 실행 ──────────
                // 경고선이 화면에 남아있지 않도록 강제 비활성화
                HideWarningLine();
                if (_muzzleVfx != null) _muzzleVfx.Stop();

                Debug.Log("[SniperPanel] Cleanup complete.");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 스텝별 구현
        // ─────────────────────────────────────────────────────────────────

        private async UniTask MoveToWaypointAsync(Vector3 target, CancellationToken ct)
        {
            const float ArrivalThresholdSqr = 0.01f * 0.01f;

            while (!ct.IsCancellationRequested)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target, _moveSpeed * Time.deltaTime);

                if ((transform.position - target).sqrMagnitude <= ArrivalThresholdSqr)
                {
                    transform.position = target;
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>[수정 2] _bossBlackboard → _blackboard 오타 수정</summary>
        private void AimAtPlayer()
        {
            if (_blackboard == null) return; // [수정 2] 오타 수정

            Vector3 dir = _blackboard.PlayerPosition - transform.position;
            if (dir.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// [수정 2] ShowWarningLineAsync에 개별 try-finally 추가.
        /// 이 스텝이 중간에 취소되어도 경고선 즉시 비활성화.
        /// </summary>
        private async UniTask ShowWarningLineAsync(CancellationToken ct)
        {
            if (_warningLine == null)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_warningDuration), cancellationToken: ct);
                return;
            }

            try
            {
                _warningLine.enabled = true;
                UpdateWarningLinePositions();

                float elapsed = 0f;
                while (elapsed < _warningDuration && !ct.IsCancellationRequested)
                {
                    float t = elapsed / _warningDuration;

                    _warningColorCache   = _warningColorFull;
                    _warningColorCache.a = Mathf.Lerp(0f, _warningColorFull.a, t);

                    _warningLine.startColor = _warningColorCache;
                    _warningLine.endColor   = _warningColorCache;

                    UpdateWarningLinePositions();

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // 완전 불투명 고정
                _warningLine.startColor = _warningColorFull;
                _warningLine.endColor   = _warningColorFull;
            }
            catch (OperationCanceledException)
            {
                // 취소 시 경고선 즉시 숨김 후 예외 재전파
                HideWarningLine();
                throw;
            }
        }

        /// <summary>
        /// [수정 2] FireLaserAsync에도 try-finally 적용.
        /// VFX가 발사 도중 취소돼도 muzzleVfx.Stop() 보장.
        /// </summary>
        private async UniTask FireLaserAsync(CancellationToken ct)
        {
            try
            {
                if (_warningLine != null)
                {
                    _warningLine.startColor = _fireColor;
                    _warningLine.endColor   = _fireColor;
                    UpdateWarningLinePositions();
                }

                if (_muzzleVfx != null) _muzzleVfx.Play();

                Vector2 fireDir = transform.right;
                Vector2 origin  = transform.position;

                var hit = Physics2D.CircleCast(
                    origin:    origin,
                    radius:    _castRadius,
                    direction: fireDir,
                    distance:  _castMaxDistance,
                    layerMask: _playerLayer
                );

                if (hit.collider != null)
                {
                    hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(_laserDamage);
                    Debug.Log($"[SniperPanel] Hit player. Damage={_laserDamage}");
                }

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);
            }
            finally
            {
                // ── [수정 2] 취소 시 VFX 반드시 정지 ──────────────────
                if (_muzzleVfx != null) _muzzleVfx.Stop();
            }
        }

        private void HideWarningLine()
        {
            if (_warningLine != null)
                _warningLine.enabled = false;
        }

        private void AdvanceWaypoint()
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;
        }

        /// <summary>[수정 2] _blackboard 통일 (원본의 _blackboard / _bossBlackboard 혼재 수정)</summary>
        private void UpdateWarningLinePositions()
        {
            if (_warningLine == null || _blackboard == null) return; // [수정 2]
            _warningLine.SetPosition(0, transform.position);
            _warningLine.SetPosition(1, _blackboard.PlayerPosition);
        }

        private void OnDestroy()
        {
            HideWarningLine();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_waypoints == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(_waypoints[i].position, 0.3f);
                if (i + 1 < _waypoints.Length && _waypoints[i + 1] != null)
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
            }
        }
#endif
    }
}
