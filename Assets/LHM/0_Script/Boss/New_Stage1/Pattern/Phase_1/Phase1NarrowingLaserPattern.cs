using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    [CreateAssetMenu(fileName = "Pattern_Phase1_NarrowingLaser",
                     menuName = "BossSystem/Patterns/Phase1_NarrowingLaser")]
    public class Phase1NarrowingLaserPattern : BossPatternBase
    {
        [Header("Charge")]
        [SerializeField] private ParticleSystem _chargeVfxPrefab;
        [SerializeField] private float _chargeDuration = 2f;

        [Header("Laser Walls")]
        [SerializeField] private GameObject _laserWallPrefab;
        [SerializeField] private Color _laserWallColor = Color.red;
        [SerializeField] private float _wallStartX = 18f;
        [SerializeField] private float _wallHalfHeight = 15f;
        [SerializeField] private float _wallTargetX = 0f;
        [SerializeField] private float _wallMoveSpeed = 1.5f;
        [SerializeField] private float _crushDistance = 0.5f;

        [Header("Wall Damage")]
        [SerializeField] private float _wallDPS = 40f;
        [SerializeField] private float _wallThickness = 0.5f;
        [SerializeField] private float _wallCastLen = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Interact Object")]
        [SerializeField] private GameObject _interactObjPrefab;
        [SerializeField] private Vector2 _interactOffset = new Vector2(0f, -3f);

        [Header("QTE")]
        [SerializeField] private float _qteTimeLimit = 8f;

        [Header("Result VFX")]
        [SerializeField] private ParticleSystem _successVfxPrefab;
        [SerializeField] private ParticleSystem _failVfxPrefab;

        [Tooltip("파훼 후 딜타임 지속 시간(초).")]
        [SerializeField] private float _dealTimeDuration = 5f;

        [Header("UI")]
        [SerializeField] private QTEUIController _qteUI;
        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // ── 1. 차지 연출 ──────────────────────────────────────────────
            ParticleSystem chargeVfx = null;
            if (_chargeVfxPrefab != null)
            {
                chargeVfx = Object.Instantiate(
                    _chargeVfxPrefab,
                    ctx.BossTransform.position, Quaternion.identity,
                    ctx.BossTransform);
                chargeVfx.Play();
            }

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_chargeDuration), cancellationToken: ct);

            if (chargeVfx != null) Object.Destroy(chargeVfx.gameObject);
            ct.ThrowIfCancellationRequested();

            // ── 2. 레이저 벽 생성 ─────────────────────────────────────────
            var (wallLeftGo, wallLeft, wallRightGo, wallRight) = CreateWalls();

            // ── 3. 상호작용 오브젝트 배치 ────────────────────────────────
            GameObject interactObj = null;
            NarrowingQTETrigger qteTrigger = null;

            if (_interactObjPrefab != null)
            {
                Vector3 interactPos = ctx.BossTransform.position + (Vector3)_interactOffset;
                interactObj = Object.Instantiate(_interactObjPrefab, interactPos, Quaternion.identity);
                qteTrigger = interactObj.GetComponent<NarrowingQTETrigger>();
            }

            var successTcs = new UniTaskCompletionSource<bool>();

            if (qteTrigger != null)
            {
                qteTrigger.Initialize(ctx.PlayerTransform, _qteTimeLimit, _qteUI);
                qteTrigger.OnQTESuccess += () => successTcs.TrySetResult(true);
                qteTrigger.OnQTEFail += () => successTcs.TrySetResult(false);
            }
            else
            {
                FailAfterTimeout(successTcs, ct).Forget();
            }

            // ── 4. 벽 이동 + QTE 병렬 ────────────────────────────────────
            bool qteSuccess = false;

            try
            {
                var wallTask = RunWallsAsync(wallLeft, wallRight, successTcs, ct);
                var qteTask = successTcs.Task.AttachExternalCancellation(ct);

                int winner = await UniTask.WhenAny(wallTask, qteTask);
                if (winner == 1)
                    qteSuccess = await qteTask;
            }
            finally
            {
                if (interactObj != null) Object.Destroy(interactObj);
            }

            if (wallLeftGo != null) Object.Destroy(wallLeftGo);
            if (wallRightGo != null) Object.Destroy(wallRightGo);

            ct.ThrowIfCancellationRequested();

            // ── 5. 성공/실패 VFX — 모두 방어막 해제 후 딜타임 ────────────
            if (qteSuccess)
            {
                Log("QTE Success!");
                if (_successVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _successVfxPrefab, ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }
            }
            else
            {
                Log("QTE Failed.");
                if (_failVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _failVfxPrefab, ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }
            }

            ctx.DamageHandler.BreakBarrier();
            Log($"Barrier broken! Deal time {_dealTimeDuration}s.");

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_dealTimeDuration), cancellationToken: ct);

            Log("NarrowingLaser pattern complete.");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private (GameObject leftGo, LineRenderer left,
                 GameObject rightGo, LineRenderer right) CreateWalls()
        {
            if (_laserWallPrefab == null)
                return (null, null, null, null);

            var leftGo = Object.Instantiate(_laserWallPrefab);
            var left = leftGo.GetComponent<LineRenderer>();
            var rightGo = Object.Instantiate(_laserWallPrefab);
            var right = rightGo.GetComponent<LineRenderer>();

            if (left != null)
            {
                left.startColor = left.endColor = _laserWallColor;
                left.positionCount = 2;
                left.SetPosition(0, new Vector3(-_wallStartX, -_wallHalfHeight, 0f));
                left.SetPosition(1, new Vector3(-_wallStartX, _wallHalfHeight, 0f));
                left.enabled = true;
            }
            if (right != null)
            {
                right.startColor = right.endColor = _laserWallColor;
                right.positionCount = 2;
                right.SetPosition(0, new Vector3(_wallStartX, -_wallHalfHeight, 0f));
                right.SetPosition(1, new Vector3(_wallStartX, _wallHalfHeight, 0f));
                right.enabled = true;
            }

            return (leftGo, left, rightGo, right);
        }

        private async UniTask RunWallsAsync(
            LineRenderer wallLeft,
            LineRenderer wallRight,
            UniTaskCompletionSource<bool> successTcs,
            CancellationToken ct)
        {
            float leftX = -_wallStartX;
            float rightX = _wallStartX;

            while (!ct.IsCancellationRequested)
            {
                if (successTcs.Task.Status != UniTaskStatus.Pending) return;

                leftX = Mathf.MoveTowards(leftX, _wallTargetX, _wallMoveSpeed * Time.deltaTime);
                rightX = Mathf.MoveTowards(rightX, _wallTargetX, _wallMoveSpeed * Time.deltaTime);

                SetWallX(wallLeft, leftX);
                SetWallX(wallRight, rightX);

                LaserUtility.BoxCastAndDamage(
                    new Vector2(leftX, 0f), Vector2.right,
                    _wallThickness, _wallCastLen, _playerLayer, _wallDPS * Time.deltaTime);
                LaserUtility.BoxCastAndDamage(
                    new Vector2(rightX, 0f), Vector2.left,
                    _wallThickness, _wallCastLen, _playerLayer, _wallDPS * Time.deltaTime);

                if (rightX - leftX <= _crushDistance)
                {
                    Log("Walls crushed.");
                    successTcs.TrySetResult(false);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private static void SetWallX(LineRenderer lr, float x)
        {
            if (lr == null) return;
            var p0 = lr.GetPosition(0); p0.x = x; lr.SetPosition(0, p0);
            var p1 = lr.GetPosition(1); p1.x = x; lr.SetPosition(1, p1);
        }

        private async UniTaskVoid FailAfterTimeout(
            UniTaskCompletionSource<bool> tcs, CancellationToken ct)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_qteTimeLimit),
                cancellationToken: ct).SuppressCancellationThrow();
            tcs.TrySetResult(false);
        }
        public void SetQTEUI(QTEUIController qteUI)
        {
            _qteUI = qteUI;
        }
    }

}


