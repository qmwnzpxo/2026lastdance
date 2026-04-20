using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{ // ───────────────────────────────────────────────────────────────────────
    // Phase 2-2 — 거울 반사 레이저 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "Pattern_Phase2_MirrorLaser",
                     menuName = "BossSystem/Patterns/Phase2_MirrorLaser")]
    public class Phase2MirrorLaserPattern : BossPatternBase
    {
        [Header("Mirror")]
        [SerializeField] private GameObject _mirrorPrefab;
        [SerializeField] private Transform _mirrorSpawnPoint;
        [SerializeField] private float _chargeTime = 2f;
        [SerializeField] private float _maxLaserDuration = 12f;

        [Header("Damage")]
        [SerializeField] private float _playerDPS = 25f;
        [SerializeField] private float _reflectDamage = 50f;
        [SerializeField] private float _castRadius = 0.15f;
        [SerializeField] private float _castDistance = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _laserLinePrefab;
        [SerializeField] private Color _laserColor = Color.red;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            Vector3 spawnPos = _mirrorSpawnPoint != null
                ? _mirrorSpawnPoint.position
                : ctx.BossTransform.position + Vector3.down * 6f;

            var mirrorGo = Object.Instantiate(_mirrorPrefab, spawnPos, Quaternion.identity);
            var mirror = mirrorGo.GetComponent<Mirror>();

            if (mirror == null)
            {
                Debug.LogError("[MirrorLaser] Mirror component missing on prefab!");
                Object.Destroy(mirrorGo);
                return;
            }

            mirror.Initialize(ctx.BossTransform);

            var reflectTcs = new UniTaskCompletionSource();
            mirror.OnReflectedToBoss += () => reflectTcs.TrySetResult();

            LineRenderer laserLine = _laserLinePrefab != null
                ? Object.Instantiate(_laserLinePrefab) : null;

            try
            {
                // 충전 대기 (전조 연출)
                Log($"Charging laser... ({_chargeTime}s)");
                await UniTask.Delay(System.TimeSpan.FromSeconds(_chargeTime), cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                mirror.StartReflecting();

                await UniTask.WhenAny(
                    RunLaserLoopAsync(ctx, laserLine, ct),
                    reflectTcs.Task.AttachExternalCancellation(ct),
                    UniTask.Delay(System.TimeSpan.FromSeconds(_maxLaserDuration), cancellationToken: ct));

                if (reflectTcs.Task.Status == UniTaskStatus.Succeeded)
                {
                    ctx.DamageHandler.TakeDamage(_reflectDamage);
                    Log($"Reflected! Boss took {_reflectDamage} damage.");
                }
            }
            finally
            {
                if (laserLine != null) Object.Destroy(laserLine.gameObject);
                if (mirrorGo != null) Object.Destroy(mirrorGo);
            }
        }

        private async UniTask RunLaserLoopAsync(
            BossContext ctx, LineRenderer line, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Vector2 origin = ctx.BossTransform.position;
                Vector2 dir = ((Vector2)ctx.PlayerTransform.position - origin).normalized;

                if (line != null)
                    LaserUtility.SetLineImmediate(
                        line,
                        ctx.BossTransform.position,
                        ctx.PlayerTransform.position,
                        _laserColor);

                LaserUtility.CircleCastDPS(origin, dir, _castRadius, _castDistance,
                                           _playerLayer, _playerDPS);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
