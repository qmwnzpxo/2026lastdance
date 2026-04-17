using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 2-1 — 좌우 스윕 레이저 (Sequential)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase2_SideLaser",
                     menuName = "BossSystem/Patterns/Phase2_SideLaser")]
    public class Phase2SideLaserPattern : BossPatternBase
    {
        [Header("Side Laser")]
        [SerializeField] private float _warningDuration = 3f;
        [SerializeField] private float _laserDuration = 0.4f;
        [SerializeField] private float _mapEdgeOffset = 20f;
        [Tooltip("기획: 좌→위→오 3방향을 몇 번 반복할지. 기본 3회.")]
        [SerializeField] private int _repeatCount = 3;

        [Header("Damage")]
        [SerializeField] private float _laserThickness = 0.5f;
        [SerializeField] private float _castDistance = 40f;
        [SerializeField] private float _laserDamage = 35f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color _fireColor = Color.red;

        private static readonly (Vector2 offset, Vector2 dir)[] _sweepData =
        {
            (new Vector2(-1f, 0f), Vector2.right),  // 왼쪽에서 오른쪽으로
            (new Vector2(0f,  1f), Vector2.down),   // 위에서 아래로
            (new Vector2(1f,  0f), Vector2.left),   // 오른쪽에서 왼쪽으로
        };

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                for (int repeat = 0; repeat < _repeatCount; repeat++)
                {
                    foreach (var (offset, dir) in _sweepData)
                    {
                        ct.ThrowIfCancellationRequested();

                        Vector2 playerPos2D = ctx.PlayerTransform.position;
                        Vector2 origin = playerPos2D + offset * _mapEdgeOffset;
                        Vector2 endPos = origin + dir * _castDistance;

                        await LaserUtility.FadeInWarningLineAsync(
                            warnLine,
                            () => (Vector3)(Vector2)origin,
                            () => (Vector3)(Vector2)endPos,
                            _warningColor, _warningDuration, ct);

                        ct.ThrowIfCancellationRequested();

                        if (warnLine != null)
                            LaserUtility.SetLineImmediate(warnLine, origin, endPos, _fireColor);

                        LaserUtility.BoxCastAndDamage(
                            origin, dir, _laserThickness, _castDistance,
                            _playerLayer, _laserDamage);

                        await UniTask.Delay(
                            System.TimeSpan.FromSeconds(_laserDuration), cancellationToken: ct);

                        LaserUtility.HideLine(warnLine);
                    }
                }

                Log($"SideLaser pattern complete. ({_repeatCount} repeats)");
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
            }
        }
    }
}