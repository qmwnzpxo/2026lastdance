//using Cysharp.Threading.Tasks;
//using System.Threading;
//using UnityEngine;

//namespace BossSystem.Patterns
//{
//    [CreateAssetMenu(fileName = "Pattern_Phase1_NarrowingLaser",
//                     menuName = "BossSystem/Patterns/Phase1_NarrowingLaser")]
//    public class Phase1NarrowingLaserPattern : BossPatternBase
//    {
//        [Header("Charge")]
//        [SerializeField] private ParticleSystem _chargeVfxPrefab;
//        [SerializeField] private float _chargeDuration = 2f;

//        [Header("Laser Walls")]
//        [SerializeField] private GameObject _laserWallPrefab;
//        [SerializeField] private Color _laserWallColor = Color.red;

//        [Tooltip("벽 시작 X 위치 (절댓값). 왼쪽=-wallStartX, 오른쪽=+wallStartX\n예) 24 → 왼쪽 -24, 오른쪽 +24")]
//        [SerializeField] private float _wallStartX = 24f;

//        [Tooltip("벽이 멈추는 X 위치 (절댓값). 왼쪽=-wallStopX, 오른쪽=+wallStopX\n예) 8 → 왼쪽 -8, 오른쪽 +8 에서 정지 → 맞닿음")]
//        [SerializeField] private float _wallStopX = 8f;

//        [SerializeField] private float _wallHalfHeight = 15f;
//        [SerializeField] private float _wallMoveSpeed = 1.5f;

//        [Tooltip("두 벽 사이 거리가 이 값 이하면 압사 판정 (wallStopX 0일 때 사용)")]
//        [SerializeField] private float _crushDistance = 0.5f;

//        [Header("Wall Damage")]
//        [SerializeField] private float _wallDPS = 40f;
//        [SerializeField] private float _wallThickness = 0.5f;
//        [SerializeField] private float _wallCastLen = 30f;
//        [SerializeField] private LayerMask _playerLayer;

//        [Header("Interact Object")]
//        [SerializeField] private GameObject _interactObjPrefab;
//        [SerializeField] private Vector2 _interactOffset = new Vector2(0f, -3f);

//        [Header("QTE")]
//        [SerializeField] private float _qteTimeLimit = 5f;

//        [Header("Result VFX")]
//        [SerializeField] private ParticleSystem _successVfxPrefab;
//        [SerializeField] private ParticleSystem _failVfxPrefab;

//        [Tooltip("파훼 후 딜타임 지속 시간(초).")]
//        [SerializeField] private float _dealTimeDuration = 5f;

//        [Header("UI")]
//        [SerializeField] private QTEUIController _qteUI;

//        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
//        {
//            // ── 1. 차지 연출 ──────────────────────────────────────────────
//            ParticleSystem chargeVfx = null;
//            if (_chargeVfxPrefab != null)
//            {
//                chargeVfx = Object.Instantiate(
//                    _chargeVfxPrefab,
//                    ctx.BossTransform.position, Quaternion.identity,
//                    ctx.BossTransform);
//                chargeVfx.Play();
//            }

//            await UniTask.Delay(
//                System.TimeSpan.FromSeconds(_chargeDuration), cancellationToken: ct);

//            if (chargeVfx != null) Object.Destroy(chargeVfx.gameObject);
//            ct.ThrowIfCancellationRequested();

//            // ── 2. 레이저 벽 생성 ─────────────────────────────────────────
//            var (wallLeftGo, wallLeft, wallRightGo, wallRight) = CreateWalls();

//            // ── 3. 상호작용 오브젝트 배치 ────────────────────────────────
//            GameObject interactObj = null;
//            NarrowingQTETrigger qteTrigger = null;

//            if (_interactObjPrefab != null)
//            {
//                Vector3 interactPos = ctx.BossTransform.position + (Vector3)_interactOffset;
//                interactObj = Object.Instantiate(_interactObjPrefab, interactPos, Quaternion.identity);
//                qteTrigger = interactObj.GetComponent<NarrowingQTETrigger>();
//            }
//            else
//            {
//                Debug.LogError("[NarrowingLaser] _interactObjPrefab이 null입니다!");
//            }

//            var successTcs = new UniTaskCompletionSource<bool>();

//            if (qteTrigger != null)
//            {
//                qteTrigger.Initialize(ctx.PlayerTransform, _qteTimeLimit, _qteUI);
//                qteTrigger.OnQTESuccess += () => successTcs.TrySetResult(true);
//                qteTrigger.OnQTEFail += () => successTcs.TrySetResult(false);
//            }
//            else
//            {
//                FailAfterTimeout(successTcs, ct).Forget();
//            }

//            // ── 4. 벽 이동 + QTE 병렬 ────────────────────────────────────
//            bool qteSuccess = false;

//            try
//            {
//                var wallTask = RunWallsAsync(wallLeft, wallRight, successTcs, ct);
//                var qteTask = successTcs.Task.AttachExternalCancellation(ct);

//                int winner = await UniTask.WhenAny(wallTask, qteTask);
//                if (winner == 1)
//                    qteSuccess = await qteTask;
//            }
//            finally
//            {
//                if (interactObj != null) Object.Destroy(interactObj);
//            }

//            if (wallLeftGo != null) Object.Destroy(wallLeftGo);
//            if (wallRightGo != null) Object.Destroy(wallRightGo);

//            ct.ThrowIfCancellationRequested();

//            // ── 5. 성공/실패 VFX ──────────────────────────────────────────
//            if (qteSuccess)
//            {
//                Log("QTE Success!");
//                if (_successVfxPrefab != null)
//                {
//                    var vfx = Object.Instantiate(
//                        _successVfxPrefab, ctx.BossTransform.position, Quaternion.identity);
//                    vfx.Play();
//                    Object.Destroy(vfx.gameObject, vfx.main.duration);
//                }
//            }
//            else
//            {
//                Log("QTE Failed.");
//                if (_failVfxPrefab != null)
//                {
//                    var vfx = Object.Instantiate(
//                        _failVfxPrefab, ctx.BossTransform.position, Quaternion.identity);
//                    vfx.Play();
//                    Object.Destroy(vfx.gameObject, vfx.main.duration);
//                }
//            }

//            ctx.DamageHandler.BreakBarrier();
//            Log($"Barrier broken! Deal time {_dealTimeDuration}s.");

//            await UniTask.Delay(
//                System.TimeSpan.FromSeconds(_dealTimeDuration), cancellationToken: ct);

//            Log("NarrowingLaser pattern complete.");
//        }

//        // ── 헬퍼 ─────────────────────────────────────────────────────────

//        private (GameObject leftGo, LineRenderer left,
//                 GameObject rightGo, LineRenderer right) CreateWalls()
//        {
//            if (_laserWallPrefab == null)
//                return (null, null, null, null);

//            var leftGo = Object.Instantiate(_laserWallPrefab);
//            var left = leftGo.GetComponent<LineRenderer>();
//            var rightGo = Object.Instantiate(_laserWallPrefab);
//            var right = rightGo.GetComponent<LineRenderer>();

//            // 왼쪽 벽: 시작 -wallStartX
//            if (left != null)
//            {
//                left.startColor = left.endColor = _laserWallColor;
//                left.positionCount = 2;
//                left.SetPosition(0, new Vector3(-_wallStartX, -_wallHalfHeight, 0f));
//                left.SetPosition(1, new Vector3(-_wallStartX, _wallHalfHeight, 0f));
//                left.enabled = true;
//            }
//            // 오른쪽 벽: 시작 +wallStartX
//            if (right != null)
//            {
//                right.startColor = right.endColor = _laserWallColor;
//                right.positionCount = 2;
//                right.SetPosition(0, new Vector3(_wallStartX, -_wallHalfHeight, 0f));
//                right.SetPosition(1, new Vector3(_wallStartX, _wallHalfHeight, 0f));
//                right.enabled = true;
//            }

//            return (leftGo, left, rightGo, right);
//        }

//        private async UniTask RunWallsAsync(
//     LineRenderer wallLeft,
//     LineRenderer wallRight,
//     UniTaskCompletionSource<bool> successTcs,
//     CancellationToken ct)
//        {
//            // 시작 좌표 저장
//            float startLeftX = -_wallStartX;
//            float startRightX = _wallStartX + _wallStopX;

//            // 현재 좌표 초기화
//            float leftX = startLeftX;
//            float rightX = startRightX;

//            // 목표 좌표 통일
//            float meetPointX = 8f;
//            float leftTarget = meetPointX;
//            float rightTarget = meetPointX;

//            // --- [추가된 부분] 동시에 도착하도록 맞춤 속도 계산 ---
//            float leftDistance = Mathf.Abs(leftTarget - startLeftX);
//            float rightDistance = Mathf.Abs(rightTarget - startRightX);

//            // 더 멀리 있는 벽을 기준으로 도착하는 데 걸릴 총 시간(Duration) 계산
//            float maxDistance = Mathf.Max(leftDistance, rightDistance);
//            float moveDuration = maxDistance > 0 ? (maxDistance / _wallMoveSpeed) : 0.1f;

//            // 정해진 시간(moveDuration) 안에 도착하도록 각 벽의 개별 속도 설정
//            float leftSpeed = leftDistance / moveDuration;
//            float rightSpeed = rightDistance / moveDuration;
//            // --------------------------------------------------------

//            while (!ct.IsCancellationRequested)
//            {
//                if (successTcs.Task.Status != UniTaskStatus.Pending) return;

//                // 기존 _wallMoveSpeed 대신 계산된 개별 속도(leftSpeed, rightSpeed)를 사용
//                leftX = Mathf.MoveTowards(leftX, leftTarget, leftSpeed * Time.deltaTime);
//                rightX = Mathf.MoveTowards(rightX, rightTarget, rightSpeed * Time.deltaTime);

//                SetWallX(wallLeft, leftX);
//                SetWallX(wallRight, rightX);

//                // 벽 데미지
//                LaserUtility.BoxCastAndDamage(
//                    new Vector2(leftX, 0f), Vector2.right,
//                    _wallThickness, _wallCastLen, _playerLayer, _wallDPS * Time.deltaTime);
//                LaserUtility.BoxCastAndDamage(
//                    new Vector2(rightX, 0f), Vector2.left,
//                    _wallThickness, _wallCastLen, _playerLayer, _wallDPS * Time.deltaTime);

//                // 압사 판정: 두 벽이 목표(8)에 모두 도달했을 때
//                bool leftArrived = Mathf.Abs(leftX - leftTarget) < 0.01f;
//                bool rightArrived = Mathf.Abs(rightX - rightTarget) < 0.01f;

//                if (leftArrived && rightArrived)
//                {
//                    Log($"Walls reached target (left={leftX:F1}, right={rightX:F1}). Crushed!");
//                    successTcs.TrySetResult(false);
//                    return;
//                }

//                await UniTask.Yield(PlayerLoopTiming.Update, ct);
//            }
//        }

//        private static void SetWallX(LineRenderer lr, float x)
//        {
//            if (lr == null) return;
//            var p0 = lr.GetPosition(0); p0.x = x; lr.SetPosition(0, p0);
//            var p1 = lr.GetPosition(1); p1.x = x; lr.SetPosition(1, p1);
//        }

//        private async UniTaskVoid FailAfterTimeout(
//            UniTaskCompletionSource<bool> tcs, CancellationToken ct)
//        {
//            await UniTask.Delay(
//                System.TimeSpan.FromSeconds(_qteTimeLimit),
//                cancellationToken: ct).SuppressCancellationThrow();
//            tcs.TrySetResult(false);
//        }

//        public void SetQTEUI(QTEUIController qteUI)
//        {
//            _qteUI = qteUI;
//        }
//    }
//}
