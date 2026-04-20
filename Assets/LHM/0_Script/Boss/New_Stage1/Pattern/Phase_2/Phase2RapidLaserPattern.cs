// ──────────────────────────────────────────────────────────────────────────
// Phase2RapidLaserPattern.cs — PanelHealth 파훼 판정 연동 패치
//
// 변경 요약:
//   · RunDroneAsync: 드론 생성 직후 PanelHealth 컴포넌트를 가져와
//     OnPanelDestroyed 이벤트에 onDestroyed 콜백 연결
//   · 플레이어가 드론을 직접 파괴하면 → PanelHealth.Die()
//     → OnPanelDestroyed → onDestroyed() → destroyedCount++
//   · 자연 소멸(경고→발사→finally Destroy)도 onDestroyed 호출 유지
// ──────────────────────────────────────────────────────────────────────────

using Cysharp.Threading.Tasks;
using System.Threading;
using BossSystem;
using UnityEngine;

namespace BossSystem.Patterns
{
    [CreateAssetMenu(fileName = "Pattern_Phase2_RapidLaser",
                     menuName = "BossSystem/Patterns/Phase2_RapidLaser")]
    public class Phase2RapidLaserPattern : BossPatternBase
    {
        [Header("Rapid Laser")]
        [SerializeField] private int _droneCount = 15;
        [SerializeField] private float _spawnInterval = 0.2f;
        [SerializeField] private float _droneMoveSpeed = 8f;
        [SerializeField] private float _droneSpreadRadius = 3f;
        [SerializeField] private float _warningDuration = 1.5f;
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

            for (int i = 0; i < _droneCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                float angle = i * (360f / _droneCount) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _droneSpreadRadius;
                Vector3 targetPos = ctx.PlayerTransform.position + offset;

                var drone = Object.Instantiate(_dronePrefab,
                                ctx.BossTransform.position, Quaternion.identity);
                drones[i] = drone;

                int capturedIdx = i;

                // ★ PanelHealth 연동: 플레이어가 선파괴 시에도 카운트 반영
                var health = drone.GetComponent<PanelHealth>();
                if (health != null)
                {
                    health.ResetHP();
                    health.OnPanelDestroyed += _ =>
                    {
                        if (!droneDestroyed[capturedIdx])
                        {
                            droneDestroyed[capturedIdx] = true;
                            destroyedCount++;
                            Log($"Drone {capturedIdx} destroyed by player. Count={destroyedCount}/{_droneCount}");
                        }
                    };
                }

                RunDroneAsync(drone, health, targetPos, ctx, ct,
                    onDestroyed: () =>
                    {
                        // 자연 소멸(발사 후) 카운트
                        if (!droneDestroyed[capturedIdx])
                        {
                            droneDestroyed[capturedIdx] = true;
                            destroyedCount++;
                        }
                    }).Forget();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: ct);
            }

            // 전부 파괴 대기 → 파훼 판정
            while (!ct.IsCancellationRequested)
            {
                if (destroyedCount >= _droneCount)
                {
                    Log($"All {_droneCount} drones destroyed. Pattern cleared!");
                    break;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            foreach (var d in drones)
                if (d != null) Object.Destroy(d);
        }

        /// <summary>드론 1개 생명주기: 이동 → 경고 → 발사 → 파괴</summary>
        private async UniTaskVoid RunDroneAsync(
            GameObject drone,
            PanelHealth health,
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
                // 1. 이동
                await LaserUtility.MoveTowardsAsync(
                    drone.transform, targetPos, _droneMoveSpeed, ct);

                // 플레이어에게 이미 파괴되었으면 조기 종료
                if (drone == null || !drone.activeInHierarchy || ct.IsCancellationRequested)
                    return;

                // 2. 경고 페이드인
                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () => drone != null ? drone.transform.position : Vector3.zero,
                    () => GetLeadTarget(ctx),
                    _warningColor, _warningDuration, ct);

                if (drone == null || !drone.activeInHierarchy || ct.IsCancellationRequested)
                    return;

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
                onDestroyed?.Invoke(); // 자연 소멸 카운트
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
