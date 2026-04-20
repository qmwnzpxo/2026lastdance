// ──────────────────────────────────────────────────────────────────────────
// Phase1PanelPattern.cs — PanelHealth 파훼 판정 연동 패치
//
// 변경 요약:
//   · OnExecuteAsync: 각 판넬 Instantiate 후 PanelHealth 등록
//     → 파괴 시 panelAliveCount-- → 0이면 CTS 취소 → 패턴 종료(파훼)
//   · RunSinglePanelAsync: 판넬이 비활성화(HP=0)되면 루프 자동 탈출
// ──────────────────────────────────────────────────────────────────────────

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

            // ★ 파훼 판정용 카운터
            int panelAliveCount = _panelCount;
            // ★ 전부 파괴 시 패턴 루프를 종료할 CTS (패턴 외부 ct와 링크)
            using var clearCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            for (int p = 0; p < _panelCount; p++)
            {
                var panelGo = Object.Instantiate(_panelPrefab,
                                  ctx.BossTransform.position, Quaternion.identity);
                panels[p] = panelGo;

                // ★ PanelHealth 연동
                var health = panelGo.GetComponent<PanelHealth>();
                if (health != null)
                {
                    health.ResetHP();
                    health.OnPanelDestroyed += _ =>
                    {
                        panelAliveCount--;
                        Log($"Panel destroyed by player. Alive={panelAliveCount}/{_panelCount}");

                        if (panelAliveCount <= 0)
                        {
                            Log("All panels cleared! Pattern defeated.");
                            clearCts.Cancel(); // 루프 종료 → 파훼 판정
                        }
                    };
                }
                else
                {
                    Debug.LogWarning("[PanelPattern] PanelHealth 컴포넌트가 없습니다. 프리팹을 확인하세요.");
                }

                int startIndex = p * (_waypoints.Length / _panelCount);
                tasks[p] = RunSinglePanelAsync(panelGo, startIndex, ctx, clearCts.Token);
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
            var warnLine = panelGo.GetComponent<LineRenderer>();

            if (warnLine == null)
                Debug.LogWarning("[PanelPattern] LineRenderer가 패널 프리팹에 없습니다.");

            try
            {
                int i = startIndex;
                while (!ct.IsCancellationRequested)
                {
                    // ★ 플레이어가 이 판넬을 파괴했으면 루프 탈출
                    if (!panelGo.activeInHierarchy) return;

                    if (_waypoints[i] == null)
                    {
                        i = (i + 1) % _waypoints.Length;
                        continue;
                    }

                    // 1. 웨이포인트로 이동
                    await LaserUtility.MoveTowardsAsync(
                        panelTf, _waypoints[i].position, _moveSpeed, ct);

                    if (!panelGo.activeInHierarchy) return;

                    // 2. Lock-on 경고
                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panelTf.position,
                        () => ctx.PlayerTransform.position,
                        _warningColor, _lockOnDuration, ct);

                    ct.ThrowIfCancellationRequested();
                    if (!panelGo.activeInHierarchy) return;

                    // 3. 위치 고정
                    Vector3 lockedTarget = ctx.PlayerTransform.position;
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _warningColor);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_lockFixedDuration), cancellationToken: ct);

                    ct.ThrowIfCancellationRequested();
                    if (!panelGo.activeInHierarchy) return;

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

                    i = (i + 1) % _waypoints.Length;
                }
            }
            finally
            {
                LaserUtility.HideLine(warnLine);
            }
        }
    }
}
