using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{

    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 레이저 난사 (Sequential)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase2_RapidLaser",
                     menuName = "BossSystem/Patterns/Phase2_RapidLaser")]
    public class Phase2RapidLaserPattern : BossPatternBase
    {
        [Header("Rapid Laser")]
        [SerializeField] private float _warningDuration = 1.5f;
        [SerializeField] private float _fireInterval = 0.2f;
        [SerializeField] private int _burstCount = 8;

        [Header("Lead Targeting")]
        [SerializeField, Range(0f, 1f)] private float _leadFactor = 0.35f;
        [SerializeField] private float _virtualLaserSpeed = 50f;

        [Header("Damage")]
        [SerializeField] private float _castRadius = 0.15f;
        [SerializeField] private float _castDistance = 30f;
        [SerializeField] private float _laserDamage = 20f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private Color _fireColor = Color.red;
        [SerializeField] private GameObject _laserVfxPrefab;
        [SerializeField] private float _laserVfxLifetime = 0.15f;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                await LaserUtility.FadeInWarningLineAsync(
                    warnLine,
                    () => ctx.BossTransform.position,
                    () => GetLeadTarget(ctx),
                    _warningColor, _warningDuration, ct);

                ct.ThrowIfCancellationRequested();

                for (int i = 0; i < _burstCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Vector3 predicted = GetLeadTarget(ctx);
                    UnityEngine.Vector2 origin = ctx.BossTransform.position;
                    UnityEngine.Vector2 dir = ((UnityEngine.Vector2)predicted - origin).normalized;

                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, ctx.BossTransform.position,
                            predicted, _fireColor);

                    LaserUtility.CircleCastAndDamage(origin, dir, _castRadius,
                        _castDistance, _playerLayer, _laserDamage);

                    if (_laserVfxPrefab != null)
                    {
                        var laser = ObjectPool.HasPool(_laserVfxPrefab)
                            ? ObjectPool.Get(_laserVfxPrefab, ctx.BossTransform.position,
                                  Quaternion.LookRotation(Vector3.forward, dir))
                            : Object.Instantiate(_laserVfxPrefab, ctx.BossTransform.position,
                                  Quaternion.LookRotation(Vector3.forward, dir));

                        LaserUtility.DestroyAfterDelayAsync(laser, _laserVfxLifetime, ct).Forget();
                    }

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireInterval), cancellationToken: ct);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
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