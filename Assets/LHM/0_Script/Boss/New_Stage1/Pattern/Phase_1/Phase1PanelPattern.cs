using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{


    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 판넬 조준 사격 (Sequential)
    // ───────────────────────────────────────────────────────────────────────
    //
    // ── Waypoints 주입 방식 변경 ────────────────────────────────────────────
    //   ScriptableObject는 씬 오브젝트(Transform)를 직렬화할 수 없다.
    //   따라서 _waypoints를 SO에 저장하지 않고,
    //   씬의 MonoBehaviour(예: BossStageSetup)에서
    //   pattern.SetWaypoints(waypointTransforms) 를 호출해 주입한다.
    //
    //   주입 시점: BossController.Start() → EnterPhaseAsync() 전에 호출.
    //   또는 BossController Inspector에서 패턴 레퍼런스를 가져와 주입.
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_Panel",
                     menuName = "BossSystem/Patterns/Phase1_Panel")]
    public class Phase1PanelPattern : BossPatternBase
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelPrefab;

        // ── [수정] _waypoints는 SO에 직렬화하지 않음 ─────────────────────
        // ScriptableObject에 Transform[] (씬 오브젝트)를 직렬화하면
        // Inspector에 "타입 미스" 경고가 발생하고 씬 저장 시 참조가 소실된다.
        // 런타임에 SetWaypoints()로 주입받는 방식으로 변경.
        [System.NonSerialized]
        private Transform[] _waypoints;

        [SerializeField] private float _moveSpeed = 5f;

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
        // ── [수정] LineRenderer 프리팹 슬롯 ──────────────────────────────
        // Inspector에서 Project 패널의 LineRenderer 컴포넌트가 붙은
        // 프리팹 루트 오브젝트를 드래그해서 할당할 것.
        // 씬에 존재하는 오브젝트를 드래그하면 타입 미스 발생.
        [SerializeField] private LineRenderer _warningLinePrefab;
        [SerializeField] private Color _warningColor = new Color(1f, 0.3f, 0f, 1f);
        [SerializeField] private Color _fireColor = Color.red;
        [SerializeField] private GameObject _muzzleVfxPrefab;

        /// <summary>
        /// 씬의 MonoBehaviour에서 웨이포인트를 주입한다.
        /// BossController.Start() 또는 씬 초기화 코드에서 호출.
        /// 
        /// 예시:
        ///   var panelPattern = phase.sequentialPatterns
        ///       .OfType&lt;Phase1PanelPattern&gt;().FirstOrDefault();
        ///   panelPattern?.SetWaypoints(waypointTransformsArray);
        /// </summary>
        public void SetWaypoints(Transform[] transforms)
        {
            _waypoints = transforms;
            Debug.Log($"[PanelPattern] Waypoints set: {transforms?.Length ?? 0}개");
        }

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            if (_waypoints == null || _waypoints.Length == 0 || _waypoints[0] == null)
            {
                Debug.LogWarning("[PanelPattern] Waypoints가 설정되지 않았습니다. " +
                                 "BossController 또는 씬 초기화 코드에서 SetWaypoints()를 호출하세요.");
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

                    await LaserUtility.MoveTowardsAsync(
                        panelTf, _waypoints[i].position, _moveSpeed, ct);

                    await LaserUtility.FadeInWarningLineAsync(
                        warnLine,
                        () => panelTf.position,
                        () => ctx.PlayerTransform.position,
                        _warningColor, _lockOnDuration, ct);

                    ct.ThrowIfCancellationRequested();

                    Vector3 lockedTarget = ctx.PlayerTransform.position;
                    if (warnLine != null)
                        LaserUtility.SetLineImmediate(warnLine, panelTf.position,
                            lockedTarget, _warningColor);

                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_lockFixedDuration), cancellationToken: ct);

                    ct.ThrowIfCancellationRequested();

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