using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{

    // ───────────────────────────────────────────────────────────────────────
    // Phase 2-3 — 순차적 레이저 난사 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 기획:
    ///   3-1. 보스 좌표에서 레이저 오브젝트를 0.2초 간격으로 15개 생성
    ///   3-2. 플레이어 좌표 근처에 레이저 오브젝트가 이동
    ///   3-3. 각 오브젝트가 플레이어 방향으로 경고(1.5초) 후 레이저 발사
    ///   3-4. 발사 후 오브젝트 파괴됨
    ///   3-5. 플레이어 공격으로 오브젝트 선파괴 가능
    ///   3-6. 모든 오브젝트 파괴 시 파훼 판정
    ///
    /// <summary>
    [CreateAssetMenu(fileName = "Pattern_Phase2_RapidLaser",
                     menuName = "BossSystem/Patterns/Phase2_RapidLaser")]
    public class Phase2RapidLaserPattern : BossPatternBase
    {
        [Header("Rapid Laser")]
        [Tooltip("생성할 레이저 드론 수. 기획: 15개")]
        [SerializeField] private int _droneCount = 15;
        [Tooltip("드론 생성 간격 (초). 기획: 0.2초")]
        [SerializeField] private float _spawnInterval = 0.2f;
        [Tooltip("드론 이동 속도")]
        [SerializeField] private float _droneMoveSpeed = 8f;
        [Tooltip("드론이 플레이어 주변에 퍼지는 반경")]
        [SerializeField] private float _droneSpreadRadius = 3f;
        [Tooltip("드론이 목표 위치 도착 후 경고 시간 (초). 기획: 1.5초")]
        [SerializeField] private float _warningDuration = 1.5f;
        [Tooltip("경고 후 발사 지속 시간")]
        [SerializeField] private float _fireDuration = 0.2f;

        [Header("Lead Targeting")]
        [SerializeField, Range(0f, 1f)] private float _leadFactor = 0.35f;
        [SerializeField] private float _virtualLaserSpeed = 50f;

        [Header("Damage")]
        [SerializeField] private float _castRadius = 0.15f;
        [SerializeField] private float _castDistance = 30f;
        [SerializeField] private float _laserDamage = 20f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private GameObject _dronePrefab;
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private Color _fireColor = Color.red;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            if (_dronePrefab == null)
            {
                Debug.LogWarning("[RapidLaser] _dronePrefab이 없습니다.");
                return;
            }

            var drones = new GameObject[_droneCount];
            var droneDestroyed = new bool[_droneCount];
            int destroyedCount = 0;

            // ── 드론 순차 생성 ────────────────────────────────────────────
            for (int i = 0; i < _droneCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                // 플레이어 주변 랜덤 위치 계산 (원형 분산)
                float angle = i * (360f / _droneCount) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)
                                   * _droneSpreadRadius;
                Vector3 targetPos = ctx.PlayerTransform.position + offset;

                var drone = Object.Instantiate(_dronePrefab,
                                ctx.BossTransform.position, Quaternion.identity);
                drones[i] = drone;

                int capturedIdx = i;

                // 드론 개별 생명주기 (이동 → 경고 → 발사)를 독립 Task로 실행
                RunDroneAsync(drone, targetPos, ctx, ct,
                    onDestroyed: () =>
                    {
                        if (!droneDestroyed[capturedIdx])
                        {
                            droneDestroyed[capturedIdx] = true;
                            destroyedCount++;
                        }
                    }).Forget();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: ct);
            }

            // ── 모든 드론 파괴 대기 ───────────────────────────────────────
            while (!ct.IsCancellationRequested)
            {
                if (destroyedCount >= _droneCount)
                {
                    Log($"All {_droneCount} drones destroyed. Pattern cleared!");
                    break;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 남은 드론 정리
            foreach (var d in drones)
                if (d != null) Object.Destroy(d);
        }

        /// <summary>
        /// 드론 1개의 생명주기: 이동 → 경고 → 발사 → 파괴
        /// </summary>
        private async UniTaskVoid RunDroneAsync(
            GameObject drone,
            Vector3 targetPos,
            BossContext ctx,
            CancellationToken ct,
            System.Action onDestroyed)
        {
            if (drone == null) return;

            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                // 1. 목표 위치로 이동
                await LaserUtility.MoveTowardsAsync(
                    drone.transform, targetPos, _droneMoveSpeed, ct);

                if (drone == null || ct.IsCancellationRequested) return;

                // 2. 경고 페이드인 (플레이어 추적)
                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () => drone != null ? drone.transform.position : Vector3.zero,
                    () => GetLeadTarget(ctx),
                    _warningColor, _warningDuration, ct);

                if (drone == null || ct.IsCancellationRequested) return;

                // 3. 발사
                Vector3 predicted = GetLeadTarget(ctx);
                if (warnLine != null)
                    LaserUtility.SetLineImmediate(warnLine,
                        drone.transform.position, predicted, _fireColor);

                Vector2 origin = drone.transform.position;
                Vector2 dir = ((Vector2)predicted - origin).normalized;

                LaserUtility.CircleCastAndDamage(
                    origin, dir, _castRadius, _castDistance, _playerLayer, _laserDamage);

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
                if (drone != null) Object.Destroy(drone);
                onDestroyed?.Invoke();
            }
        }

        private Vector3 GetLeadTarget(BossContext ctx)
        {
            Vector3 playerPos = ctx.PlayerTransform.position;
            Vector3 velocity = ctx.Blackboard.PlayerVelocity;
            float dist = Vector3.Distance(ctx.BossTransform.position, playerPos);
            float t = dist / Mathf.Max(_virtualLaserSpeed, 0.01f);
            return playerPos + velocity * t * _leadFactor;
        }
    }
}