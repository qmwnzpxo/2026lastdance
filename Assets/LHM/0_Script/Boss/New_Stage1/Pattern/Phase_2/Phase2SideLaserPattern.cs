using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 2 — 좌우 스윕 레이저 (Sequential)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase2_SideLaser",
                     menuName = "BossSystem/Patterns/Phase2_SideLaser")]
    public class Phase2SideLaserPattern : BossPatternBase
    {
        [Header("Side Laser")]
        [SerializeField] private float _warningDuration = 3f;
        [SerializeField] private float _laserDuration = 0.4f;
        [SerializeField] private float _mapEdgeOffset = 20f;

        [Header("Damage")]
        [SerializeField] private float _laserThickness = 0.5f;
        [SerializeField] private float _castDistance = 40f;
        [SerializeField] private float _laserDamage = 35f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color _fireColor = Color.red;

        private static readonly (UnityEngine.Vector2 offset, UnityEngine.Vector2 dir)[] _sweepData =
        {
            (new UnityEngine.Vector2(-1f, 0f), UnityEngine.Vector2.right),
            (new UnityEngine.Vector2(0f,  1f), UnityEngine.Vector2.down),
            (new UnityEngine.Vector2(1f,  0f), UnityEngine.Vector2.left),
        };

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                foreach (var (offset, dir) in _sweepData)
                {
                    ct.ThrowIfCancellationRequested();

                    UnityEngine.Vector2 playerPos2D = ctx.PlayerTransform.position;
                    UnityEngine.Vector2 origin = playerPos2D + offset * _mapEdgeOffset;
                    UnityEngine.Vector2 endPos = origin + dir * _castDistance;

                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine, () => (Vector3)(Vector2)origin, () => (Vector3)(Vector2)endPos,
                        _warningColor, _warningDuration, ct);

                    ct.ThrowIfCancellationRequested();

                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, origin, endPos, _fireColor);

                    LaserUtility.BoxCastAndDamage(
                        origin, dir, _laserThickness, _castDistance, _playerLayer, _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_laserDuration), cancellationToken: ct);

                    LaserUtility.HideLine(warnLine);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
            }
        }
    }
}