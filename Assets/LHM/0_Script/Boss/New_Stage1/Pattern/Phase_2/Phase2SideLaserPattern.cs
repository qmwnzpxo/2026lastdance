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
        [SerializeField] private float _castDistance = 40f; // 시각적 길이도 이 값을 따르게 됨
        [SerializeField] private float _laserDamage = 35f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
           
        [SerializeField] private GameObject _warningLinePrefab;
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
            GameObject warnLineGo = null;
            LineRenderer warnLine = null;

            if (_warningLinePrefab != null)
            {
                warnLineGo = Object.Instantiate(_warningLinePrefab);
                warnLine = warnLineGo.GetComponent<LineRenderer>();

                // 생성 직후 라인렌더러 두께를 인스펙터 값으로 초기화
                if (warnLine != null)
                {
                    warnLine.startWidth = _laserThickness;
                    warnLine.endWidth = _laserThickness;
                }
            }

            try
            {
                for (int repeat = 0; repeat < _repeatCount; repeat++)
                {
                    foreach (var (offset, dir) in _sweepData)
                    {
                        ct.ThrowIfCancellationRequested();

                        Vector2 playerPos2D = ctx.PlayerTransform.position;
                        Vector2 origin = playerPos2D + offset * _mapEdgeOffset;
                        Vector2 endPos = origin + dir * _castDistance; // 이 endPos가 길이 정보를 가짐

                        // 경고 선을 그리기 전에 포지션을 명시적으로 설정하여 길이를 맞춤
                        if (warnLine != null)
                        {
                            warnLine.SetPosition(0, origin);
                            warnLine.SetPosition(1, endPos);
                        }

                        await LaserUtility.FadeInWarningLineAsync(
                            warnLine,
                            () => (Vector3)(Vector2)origin,
                            () => (Vector3)(Vector2)endPos,
                            _warningColor, _warningDuration, ct);

                        ct.ThrowIfCancellationRequested();

                        //실제 발사 선을 그리기 전에 다시 포지션을 명시적으로 설정 (확실하게 하기 위해)
                        if (warnLine != null)
                        {
                            warnLine.SetPosition(0, origin);
                            warnLine.SetPosition(1, endPos);
                            LaserUtility.SetLineImmediate(warnLine, origin, endPos, _fireColor);
                        }

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
                //GameObject 기준으로 파괴
                if (warnLineGo != null) Object.Destroy(warnLineGo);
            }
        }
    }
}
