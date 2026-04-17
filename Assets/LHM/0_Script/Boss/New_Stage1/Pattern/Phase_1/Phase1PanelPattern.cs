using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{


    // ───────────────────────────────────────────────────────────────────────
    // Phase 1-3 — 판넬 조준 사격 (Sequential)
    // ───────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "Pattern_Phase1_Panel",
                     menuName = "BossSystem/Patterns/Phase1_Panel")]
    public class Phase1PanelPattern : BossPatternBase
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private float _moveSpeed = 5f;

      
        [HideInInspector] public Transform[] _waypoints;

        [Header("Telegraphing")]
        [SerializeField] private float _lockOnDuration = 1.5f;
        [SerializeField] private float _lockFixedDuration = 0.5f;
        [SerializeField] private float _fireDuration = 0.3f;
        [SerializeField] private float _postFireDelay = 0.7f;

        [Header("Laser")]
        [SerializeField] private float _castRadius = 0.2f;
        [SerializeField] private float _castDistance = 30f;
        [SerializeField] private float _laserDamage = 25f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("VFX")]
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0.3f, 0f, 1f);
        [SerializeField] private Color _fireColor = Color.red;
        [SerializeField] private GameObject _muzzleVfxPrefab;

        public void SetPoints(Transform[] transforms)
        {
            _waypoints = transforms;
            Debug.Log($"[PanelPattern] SetPoints called. Count={transforms?.Length ?? 0}");
        }

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            if (_waypoints == null || _waypoints.Length == 0 || _waypoints[0] == null)
            {
                Debug.LogWarning("[PanelPattern] Waypoints가 없습니다. BossController._panelWaypoints를 Inspector에서 할당해주세요.");
                return;
            }

            var panelGo = Object.Instantiate(_panelPrefab,
                              ctx.BossTransform.position, Quaternion.identity);
            var panelTf = panelGo.transform;

            LineRenderer warnLine = _warningLinePrefab != null
                ? Object.Instantiate(_warningLinePrefab) : null;

            try
            {
                for (int i = 0; i < _waypoints.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (_waypoints[i] == null) continue;

                    // 1. 웨이포인트로 이동
                    await LaserUtility.MoveTowardsAsync(
                        panelTf, _waypoints[i].position, _moveSpeed, ct);

                    // 2. Lock-on 경고 (플레이어 추적)
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panelTf.position,
                        () => ctx.PlayerTransform.position,
                        _warningColor, _lockOnDuration, ct);

                    ct.ThrowIfCancellationRequested();

                    // 3. 위치 고정 (발사 전 힌트)
                    Vector3 lockedTarget = ctx.PlayerTransform.position;
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _warningColor);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_lockFixedDuration), cancellationToken: ct);

                    ct.ThrowIfCancellationRequested();

                    // 4. 발사
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _fireColor);

                    if (_muzzleVfxPrefab != null)
                        LaserUtility.DestroyAfterDelayAsync(
                            Object.Instantiate(_muzzleVfxPrefab, panelTf.position, panelTf.rotation),
                            _fireDuration, ct).Forget();

                    LaserUtility.CircleCastAndDamage(
                        panelTf.position, panelTf.right,
                        _castRadius, _castDistance, _playerLayer, _laserDamage);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_fireDuration), cancellationToken: ct);
                    LaserUtility.HideLine(warnLine);

                    // 5. 딜 타임
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_postFireDelay), cancellationToken: ct);
                }
            }
            finally
            {
                if (warnLine != null) Object.Destroy(warnLine.gameObject);
                if (panelGo != null) Object.Destroy(panelGo);
            }
        }
    }
}