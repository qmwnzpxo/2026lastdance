using Cysharp.Threading.Tasks;
using System.Threading;
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

        [SerializeField] private GameObject _warningLinePrefab;
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
                        if (!droneDestroyed[capturedIdx])
                        {
                            droneDestroyed[capturedIdx] = true;
                            destroyedCount++;
                        }
                    }).Forget();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_spawnInterval), cancellationToken: ct);
            }

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

        private async UniTaskVoid RunDroneAsync(
            GameObject drone,
            PanelHealth health,
            Vector3 targetPos,
            BossContext ctx,
            CancellationToken ct,
            System.Action onDestroyed)
        {
            if (drone == null) return;

            GameObject warnLineGo = null;
            LineRenderer warnLine = null;
            if (_warningLinePrefab != null)
            {
                warnLineGo = Object.Instantiate(_warningLinePrefab);
                warnLine   = warnLineGo.GetComponent<LineRenderer>();
            }

            try
            {
                await LaserUtility.MoveTowardsAsync(
                    drone.transform, targetPos, _droneMoveSpeed, ct);

                if (drone == null || !drone.activeInHierarchy || ct.IsCancellationRequested)
                    return;

                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () => drone != null ? drone.transform.position : Vector3.zero,
                    () => GetLeadTarget(ctx),
                    _warningColor, _warningDuration, ct);

                if (drone == null || !drone.activeInHierarchy || ct.IsCancellationRequested)
                    return;

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

                if (warnLineGo != null) Object.Destroy(warnLineGo);
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
