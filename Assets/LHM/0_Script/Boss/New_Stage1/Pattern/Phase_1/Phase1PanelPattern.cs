using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    [CreateAssetMenu(fileName = "Pattern_Phase1_Panel",
                     menuName = "BossSystem/Patterns/Phase1_Panel")]
    public class Phase1PanelPattern : BossPatternBase
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private int _panelCount = 3;

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
                Debug.LogWarning("[PanelPattern] Waypoints가 없습니다.");
                return;
            }

            var panels = new GameObject[_panelCount];
            var tasks = new UniTask[_panelCount];

            for (int p = 0; p < _panelCount; p++)
            {
                // ★ 여기서만 Instantiate, RunSinglePanelAsync에서는 안 함
                var panelGo = Object.Instantiate(_panelPrefab,
                                  ctx.BossTransform.position, Quaternion.identity);
                panels[p] = panelGo;

                int startIndex = p * (_waypoints.Length / _panelCount);

                // ★ panelGo를 넘겨줌
                tasks[p] = RunSinglePanelAsync(panelGo, startIndex, ctx, ct);
            }

            try
            {
                await UniTask.WhenAll(tasks);
            }
            finally
            {
                foreach (var panel in panels)
                    if (panel != null) Object.Destroy(panel);
            }
        }

        private async UniTask RunSinglePanelAsync(
            GameObject panelGo, int startIndex, BossContext ctx, CancellationToken ct)
        {
            var panelTf = panelGo.transform;
            // ★ 판넬 프리팹에 내장된 LineRenderer 가져옴
            var warnLine = panelGo.GetComponent<LineRenderer>();

            if (warnLine == null)
                Debug.LogWarning("[PanelPattern] LineRenderer가 패널 프리팹에 없습니다.");

            try
            {
                int i = startIndex;
                while (!ct.IsCancellationRequested)
                {
                    if (_waypoints[i] == null)
                    {
                        i = (i + 1) % _waypoints.Length;
                        continue;
                    }

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

                    // 3. 위치 고정
                    Vector3 lockedTarget = ctx.PlayerTransform.position;
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _warningColor);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_lockFixedDuration), cancellationToken: ct);

                    ct.ThrowIfCancellationRequested();

                    // 4. 발사
                    LaserUtility.AimAt2D(panelTf, lockedTarget);

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

                    // 다음 웨이포인트 순환
                    i = (i + 1) % _waypoints.Length;
                }
            }
            finally
            {
                // ★ panelGo는 OnExecuteAsync finally에서 Destroy하므로 여기선 Line만 숨김
                LaserUtility.HideLine(warnLine);
            }
        }
    }
}