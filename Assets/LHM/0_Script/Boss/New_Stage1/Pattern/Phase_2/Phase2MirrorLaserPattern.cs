using BossSystem;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{


    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 거울 반사 레이저 기믹 (Sequential)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase2_MirrorLaser",
                     menuName = "BossSystem/Patterns/Phase2_MirrorLaser")]
    public class Phase2MirrorLaserPattern : BossPatternBase
    {
        [Header("Mirror")]
        [SerializeField] private GameObject _mirrorPrefab;

        // ── [수정] _mirrorSpawnPoint: 씬 오브젝트 참조 불가 ───────────────
        // Transform을 SO에 직렬화하면 씬 오브젝트 참조 시 타입 미스.
        // 씬 기준 오프셋 Vector3로 대체하여 SO에서 안전하게 직렬화.
        [Tooltip("보스 위치 기준 거울 스폰 오프셋. (0,-6,0) = 보스 아래 6유닛")]
        [SerializeField] private Vector3 _mirrorSpawnOffset = new Vector3(0f, -6f, 0f);
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
            // 보스 위치 + 오프셋으로 스폰 위치 계산
            Vector3 spawnPos = ctx.BossTransform.position + _mirrorSpawnOffset;

            var mirrorGo = Object.Instantiate(_mirrorPrefab, spawnPos, Quaternion.identity);
            var mirror = mirrorGo.GetComponent<Mirror>();

            if (mirror == null)
            {
                Debug.LogError("[MirrorLaser] Mirror component missing!");
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
                UnityEngine.Vector2 origin = ctx.BossTransform.position;
                UnityEngine.Vector2 dir = ((UnityEngine.Vector2)ctx.PlayerTransform.position - origin).normalized;

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
