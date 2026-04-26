using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 패턴 3. 아재 패턴 + 좁아지는 레이저 (Focus & QTE) [Sequential]
    //
    // 파훼 조건: QTE 성공 or 벽 겹침(시간 초과 포함)
    //
    // [성공] QTE 성공
    //   → ctx.DamageHandler.BreakBarrier()
    //   → DamageHandler.OnBarrierBroken 이벤트 발생
    //   → Phase1ShieldPattern.onBarrierBroken 콜백
    //   → 방어막 5초 해제
    //
    // [실패] 벽 겹침 or QTE 실패
    //   → 광역 데미지만 적용
    //   → 방어막은 유지 (BreakBarrier 호출 없음)
    //   → 다음 패턴으로 진행 (방어막 유지 상태)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_NarrowingLaser",
                     menuName = "BossSystem/Patterns/Phase1_NarrowingLaser")]
    public class Phase1NarrowingLaserPattern : BossPatternBase
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Charge")]
        [SerializeField] private ParticleSystem _chargeVfxPrefab;
        [SerializeField] private float _chargeDuration = 2f;

        [Header("Laser Walls")]
        [Tooltip("레이저 벽 프리팹 (LineRenderer 컴포넌트가 붙어있어야 함)")]
        [SerializeField] private GameObject _laserWallPrefab;
        [SerializeField] private Color _laserWallColor = Color.red;
        [Tooltip("양 끝 시작 X 좌표 (절댓값). 왼쪽은 -값, 오른쪽은 +값으로 자동 처리")]
        [SerializeField] private float _wallStartX = 18f;
        [Tooltip("레이저 벽의 Y 반폭 (위아래로 이 값의 2배 크기)")]
        [SerializeField] private float _wallHalfHeight = 15f;
        [Tooltip("벽이 이동할 중앙 X 수렴 좌표")]
        [SerializeField] private float _wallTargetX = 0f;
        [Tooltip("초당 이동 속도")]
        [SerializeField] private float _wallMoveSpeed = 1.5f;
        [Tooltip("이 거리 이하로 좁혀지면 '겹침' 판정 → 광역 피해")]
        [SerializeField] private float _crushDistance = 0.5f;

        [Header("Wall Damage (지속 판정)")]
        [SerializeField] private float _wallDPS = 40f;
        [SerializeField] private float _wallThickness = 0.5f;
        [SerializeField] private float _wallCastLen = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Interact Object")]
        [Tooltip("G키 트리거 오브젝트 프리팹 (NarrowingQTETrigger 컴포넌트 필요)")]
        [SerializeField] private GameObject _interactObjPrefab;
        [Tooltip("보스 기준 오프셋 (아래쪽 배치)")]
        [SerializeField] private Vector2 _interactOffset = new Vector2(0f, -3f);

        [Header("QTE")]
        [Tooltip("커맨드 입력 제한 시간 (초)")]
        [SerializeField] private float _qteTimeLimit = 8f;

        [Header("Result VFX")]
        [SerializeField] private ParticleSystem _successVfxPrefab;
        [SerializeField] private ParticleSystem _failVfxPrefab;
        [Tooltip("실패 시 광역 데미지")]
        [SerializeField] private float _failGlobalDamage = 40f;
        [SerializeField] private float _successHoldTime = 1f;

        // ─────────────────────────────────────────────────────────────────
        // OnExecuteAsync
        // ─────────────────────────────────────────────────────────────────

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // ── 1. 차지 연출 ──────────────────────────────────────────────
            Log("Charging energy...");
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
                Vector3 interactPos = ctx.BossTransform.position
                                      + (Vector3)_interactOffset;
                interactObj = Object.Instantiate(_interactObjPrefab, interactPos,
                                                  Quaternion.identity);
                qteTrigger  = interactObj.GetComponent<NarrowingQTETrigger>();
            }

            // ── 4. QTE 완료 소스 ─────────────────────────────────────────
            var successTcs = new UniTaskCompletionSource<bool>();

            if (qteTrigger != null)
            {
                qteTrigger.Initialize(ctx.PlayerTransform, _qteTimeLimit);
                qteTrigger.OnQTESuccess += () => successTcs.TrySetResult(true);
                qteTrigger.OnQTEFail   += () => successTcs.TrySetResult(false);
            }
            else
            {
                // 트리거 없으면 시간 초과로 처리
                FailAfterCrush(successTcs, ct).Forget();
            }

            // ── 5. 벽 이동 루프 + QTE 대기 병렬 실행 ────────────────────
            bool qteSuccess = false;

            try
            {
                var wallTask = RunWallsAsync(wallLeft, wallRight, ctx, successTcs, ct);
                var qteTask  = successTcs.Task.AttachExternalCancellation(ct);

                int winner = await UniTask.WhenAny(wallTask, qteTask);

                // winner == 0: 벽 이동 루프가 먼저 끝남 (겹침 or ct)
                // winner == 1: QTE 결과가 먼저 나옴
                if (winner == 1)
                    qteSuccess = await qteTask;
            }
            finally
            {
                if (interactObj != null) Object.Destroy(interactObj);
            }

            // ── 6. 벽 정리 ───────────────────────────────────────────────
            if (wallLeftGo  != null) Object.Destroy(wallLeftGo);
            if (wallRightGo != null) Object.Destroy(wallRightGo);

            ct.ThrowIfCancellationRequested();

            // ── 7. 결과 처리 ─────────────────────────────────────────────
            if (qteSuccess)
            {
                // ★ QTE 성공 → 방어막 파훼
                // BreakBarrier()
                //   → DamageHandler.OnBarrierBroken 이벤트 발생
                //   → Phase1ShieldPattern.onBarrierBroken 콜백
                //   → 방어막 5초 해제
                Log("QTE Success! Boss barrier broken.");

                if (_successVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _successVfxPrefab,
                        ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }

                ctx.DamageHandler.BreakBarrier();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_successHoldTime), cancellationToken: ct);
            }
            else
            {
                // ★ QTE 실패 → 광역 데미지만 적용, 방어막은 유지
                // BreakBarrier()를 호출하지 않으므로 방어막이 계속 활성화된 상태로
                // 다음 Sequential 패턴으로 넘어감
                Log("QTE Failed — global damage applied! Barrier remains active.");

                if (_failVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _failVfxPrefab,
                        ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }

                ApplyGlobalDamage(ctx, _failGlobalDamage);

                await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
            }

            Log("NarrowingLaser pattern complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 벽 생성
        // ─────────────────────────────────────────────────────────────────

        private (GameObject leftGo, LineRenderer left,
                 GameObject rightGo, LineRenderer right) CreateWalls()
        {
            if (_laserWallPrefab == null)
                return (null, null, null, null);

            var leftGo  = Object.Instantiate(_laserWallPrefab);
            var left    = leftGo.GetComponent<LineRenderer>();

            var rightGo = Object.Instantiate(_laserWallPrefab);
            var right   = rightGo.GetComponent<LineRenderer>();

            if (left != null)
            {
                left.startColor     = _laserWallColor;
                left.endColor       = _laserWallColor;
                left.positionCount  = 2;
                left.SetPosition(0, new Vector3(-_wallStartX, -_wallHalfHeight, 0f));
                left.SetPosition(1, new Vector3(-_wallStartX,  _wallHalfHeight, 0f));
                left.enabled        = true;
            }

            if (right != null)
            {
                right.startColor    = _laserWallColor;
                right.endColor      = _laserWallColor;
                right.positionCount = 2;
                right.SetPosition(0, new Vector3(_wallStartX, -_wallHalfHeight, 0f));
                right.SetPosition(1, new Vector3(_wallStartX,  _wallHalfHeight, 0f));
                right.enabled       = true;
            }

            return (leftGo, left, rightGo, right);
        }

        // ─────────────────────────────────────────────────────────────────
        // 벽 이동 루프
        // ─────────────────────────────────────────────────────────────────

        private async UniTask RunWallsAsync(
            LineRenderer wallLeft,
            LineRenderer wallRight,
            BossContext ctx,
            UniTaskCompletionSource<bool> successTcs,
            CancellationToken ct)
        {
            float leftX  = -_wallStartX;
            float rightX =  _wallStartX;

            while (!ct.IsCancellationRequested)
            {
                // QTE 이미 완료 → 루프 종료
                if (successTcs.Task.Status != UniTaskStatus.Pending) return;

                leftX  = Mathf.MoveTowards(leftX,  _wallTargetX, _wallMoveSpeed * Time.deltaTime);
                rightX = Mathf.MoveTowards(rightX, _wallTargetX, _wallMoveSpeed * Time.deltaTime);

                SetWallX(wallLeft,  leftX);
                SetWallX(wallRight, rightX);

                // 벽 접촉 데미지 (매 프레임 판정)
                if (wallLeft != null)
                    LaserUtility.BoxCastAndDamage(
                        new Vector2(leftX, 0f), Vector2.right,
                        _wallThickness, _wallCastLen, _playerLayer,
                        _wallDPS * Time.deltaTime);

                if (wallRight != null)
                    LaserUtility.BoxCastAndDamage(
                        new Vector2(rightX, 0f), Vector2.left,
                        _wallThickness, _wallCastLen, _playerLayer,
                        _wallDPS * Time.deltaTime);

                // 겹침 판정 → QTE 자동 실패
                if (rightX - leftX <= _crushDistance)
                {
                    Log("Walls crushed! QTE auto-fail.");
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

        // ─────────────────────────────────────────────────────────────────
        // 트리거 없을 때 폴백: 일정 시간 후 강제 실패
        // ─────────────────────────────────────────────────────────────────

        private async UniTaskVoid FailAfterCrush(
            UniTaskCompletionSource<bool> tcs, CancellationToken ct)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_qteTimeLimit),
                cancellationToken: ct).SuppressCancellationThrow();
            tcs.TrySetResult(false);
        }

        // ─────────────────────────────────────────────────────────────────
        // 광역 데미지
        // ─────────────────────────────────────────────────────────────────

        private static void ApplyGlobalDamage(BossContext ctx, float damage)
        {
            var hits = Physics2D.OverlapCircleAll(ctx.BossTransform.position, 50f);
            foreach (var col in hits)
            {
                // var health = col.GetComponent<IPlayerHealth>();
                // health?.TakeDamage(damage);
            }
        }
    }

    // =========================================================================
    // NarrowingQTETrigger
    // =========================================================================
    [RequireComponent(typeof(Collider2D))]
    public class NarrowingQTETrigger : MonoBehaviour
    {
        [Header("Interact")]
        [Tooltip("G키 반응 거리")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Header("QTE Commands")]
        [Tooltip("순서대로 입력해야 할 키 코드 목록")]
        [SerializeField]
        private KeyCode[] _commandSequence = {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow,
            KeyCode.RightArrow, KeyCode.Space
        };
        [Tooltip("QTE 전체 제한 시간 (초). 패턴에서도 동일값 사용 권장")]
        [SerializeField] private float _timeLimit = 8f;

        public event System.Action OnQTESuccess;
        public event System.Action OnQTEFail;

        private Transform _playerTransform;
        private bool _qteTriggered;

        public void Initialize(Transform player, float timeLimit)
        {
            _playerTransform = player;
            _timeLimit       = timeLimit;
        }

        private void Update()
        {
            if (_qteTriggered) return;
            if (_playerTransform == null) return;
            if (!Input.GetKeyDown(KeyCode.G)) return;

            float dist = Vector2.Distance(
                transform.position, _playerTransform.position);
            if (dist > _interactRadius) return;

            _qteTriggered = true;
            RunQTEAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid RunQTEAsync(CancellationToken ct)
        {
            Debug.Log("[NarrowingQTE] QTE Started! Input sequence: "
                      + string.Join(", ", _commandSequence));

            float elapsed = 0f;
            int   step    = 0;
            bool  success = false;

            while (!ct.IsCancellationRequested && elapsed < _timeLimit)
            {
                elapsed += Time.deltaTime;

                if (step < _commandSequence.Length
                    && Input.GetKeyDown(_commandSequence[step]))
                {
                    step++;
                    Debug.Log($"[NarrowingQTE] Step {step}/{_commandSequence.Length}");

                    if (step >= _commandSequence.Length)
                    {
                        success = true;
                        break;
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (success)
            {
                Debug.Log("[NarrowingQTE] QTE SUCCESS.");
                OnQTESuccess?.Invoke();
            }
            else
            {
                Debug.Log("[NarrowingQTE] QTE FAILED.");
                OnQTEFail?.Invoke();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
#endif
    }
}
