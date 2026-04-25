using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 패턴 3. 아재 패턴 + 좁아지는 레이저 (Focus & QTE) [Sequential]
    //
    // 흐름:
    //   1. 차지 연출 (에너지 모음)
    //   2. 화면 양 끝에서 레이저 벽 2개가 중앙으로 천천히 이동
    //   3. 보스 아래에 상호작용 오브젝트가 나타남
    //      → 플레이어가 근처에서 G키 입력 → UI에 커맨드 입력창 활성화
    //      → QTEManager.RequestQTE() 이벤트 발행
    //   4a. QTE 성공: 레이저 정지 + VFX + 파훼 완료 → ctx 다음 패턴
    //   4b. QTE 실패 또는 레이저가 겹침: 전체 화면 광역 데미지 → 다음 패턴
    //
    // UI / QTE 연동:
    //   · QTEManager (씬 싱글턴) — OnQTESuccess / OnQTEFail 이벤트
    //   · 실제 커맨드 UI는 QTEManager가 담당, 패턴은 결과 이벤트만 구독
    //
    // 의존 컴포넌트:
    //   · _laserWallPrefab      : LineRenderer (또는 Sprite) 레이저 벽
    //   · _interactObjPrefab    : G키 트리거 오브젝트 (NarrowingQTETrigger 필요)
    //   · QTEManager            : 씬에 존재해야 함
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_NarrowingLaser",
                     menuName = "BossSystem/Patterns/Phase1_NarrowingLaser")]
    public class Phase1NarrowingLaserPattern : BossPatternBase
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Charge")]
        [SerializeField] private ParticleSystem _chargeVfxPrefab;
        [SerializeField] private float          _chargeDuration = 2f;

        [Header("Laser Walls")]
        [Tooltip("레이저 벽 LineRenderer 프리팹")]
        [SerializeField] private LineRenderer _laserWallPrefab;
        [SerializeField] private Color        _laserWallColor = Color.red;
        [Tooltip("양 끝 시작 X 좌표 (절댓값). 왼쪽은 -값, 오른쪽은 +값으로 자동 처리")]
        [SerializeField] private float _wallStartX    = 18f;
        [Tooltip("레이저 벽의 Y 반폭 (위아래로 이 값의 2배 크기)")]
        [SerializeField] private float _wallHalfHeight = 15f;
        [Tooltip("벽이 이동할 중앙 X 수렴 좌표")]
        [SerializeField] private float _wallTargetX   = 0f;
        [Tooltip("초당 이동 속도")]
        [SerializeField] private float _wallMoveSpeed = 1.5f;
        [Tooltip("이 거리 이하로 좁혀지면 '겹침' 판정 → 광역 피해")]
        [SerializeField] private float _crushDistance = 0.5f;

        [Header("Wall Damage (지속 판정)")]
        [SerializeField] private float     _wallDPS       = 40f;
        [SerializeField] private float     _wallThickness = 0.5f;
        [SerializeField] private float     _wallCastLen   = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Interact Object")]
        [Tooltip("G키 트리거 오브젝트 프리팹 (NarrowingQTETrigger 컴포넌트 필요)")]
        [SerializeField] private GameObject _interactObjPrefab;
        [Tooltip("보스 기준 오프셋 (아래쪽 배치)")]
        [SerializeField] private Vector2    _interactOffset = new Vector2(0f, -3f);

        [Header("QTE")]
        [Tooltip("커맨드 입력 제한 시간 (초)")]
        [SerializeField] private float _qteTimeLimit = 8f;

        [Header("Result VFX")]
        [SerializeField] private ParticleSystem _successVfxPrefab;
        [SerializeField] private ParticleSystem _failVfxPrefab;
        [Tooltip("성공/실패 광역 데미지")]
        [SerializeField] private float _failGlobalDamage   = 40f;
        [SerializeField] private float _successHoldTime    = 1f;

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
            var (wallLeft, wallRight) = CreateWalls(ctx);

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
                qteTrigger.OnQTEFail    += () => successTcs.TrySetResult(false);
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

                // WhenAny 결과:
                //   winner == 0: 벽 이동 루프가 먼저 끝남 (겹침 or ct)
                //   winner == 1: QTE 결과가 먼저 나옴
                if (winner == 1)
                    qteSuccess = await qteTask;
            }
            finally
            {
                if (interactObj != null) Object.Destroy(interactObj);
            }

            // ── 6. 벽 정리 ───────────────────────────────────────────────
            if (wallLeft  != null) Object.Destroy(wallLeft.gameObject);
            if (wallRight != null) Object.Destroy(wallRight.gameObject);

            // ── 7. 결과 처리 ─────────────────────────────────────────────
            ct.ThrowIfCancellationRequested();

            if (qteSuccess)
            {
                Log("QTE Success! Boss barrier broken.");

                if (_successVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _successVfxPrefab,
                        ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }

                // 파훼 처리
                ctx.DamageHandler.BreakBarrier();

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_successHoldTime), cancellationToken: ct);
            }
            else
            {
                Log("QTE Failed — global damage applied!");

                if (_failVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(
                        _failVfxPrefab,
                        ctx.BossTransform.position, Quaternion.identity);
                    vfx.Play();
                    Object.Destroy(vfx.gameObject, vfx.main.duration);
                }

                // 광역 데미지
                ApplyGlobalDamage(ctx, _failGlobalDamage);

                await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
            }

            Log("NarrowingLaser pattern complete.");
        }

        // ─────────────────────────────────────────────────────────────────
        // 벽 생성
        // ─────────────────────────────────────────────────────────────────

        private (LineRenderer left, LineRenderer right) CreateWalls(BossContext ctx)
        {
            LineRenderer left  = null;
            LineRenderer right = null;

            if (_laserWallPrefab == null) return (left, right);

            left              = Object.Instantiate(_laserWallPrefab);
            left.startColor   = _laserWallColor;
            left.endColor     = _laserWallColor;
            left.positionCount = 2;
            left.SetPosition(0, new Vector3(-_wallStartX, -_wallHalfHeight, 0f));
            left.SetPosition(1, new Vector3(-_wallStartX,  _wallHalfHeight, 0f));
            left.enabled = true;

            right             = Object.Instantiate(_laserWallPrefab);
            right.startColor  = _laserWallColor;
            right.endColor    = _laserWallColor;
            right.positionCount = 2;
            right.SetPosition(0, new Vector3(_wallStartX, -_wallHalfHeight, 0f));
            right.SetPosition(1, new Vector3(_wallStartX,  _wallHalfHeight, 0f));
            right.enabled = true;

            return (left, right);
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

                leftX  = Mathf.MoveTowards(leftX,   _wallTargetX, _wallMoveSpeed * Time.deltaTime);
                rightX = Mathf.MoveTowards(rightX,  _wallTargetX, _wallMoveSpeed * Time.deltaTime);

                // 벽 위치 갱신
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
            // OverlapCircleAll로 플레이어 레이어 모두 히트
            var hits = Physics2D.OverlapCircleAll(
                ctx.BossTransform.position, 50f);

            foreach (var col in hits)
            {
               // var health = col.GetComponent<Boss.Phase1.IPlayerHealth>();
                //health?.TakeDamage(damage);
            }
        }
    }

    // =========================================================================
    // NarrowingQTETrigger
    // =========================================================================
    // 씬에 배치된 상호작용 오브젝트에 붙이는 컴포넌트.
    // 플레이어가 근처에서 G키 입력 → QTEManager에 커맨드 시퀀스 요청.
    // =========================================================================
    [RequireComponent(typeof(Collider2D))]
    public class NarrowingQTETrigger : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Interact")]
        [Tooltip("G키 반응 거리")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Header("QTE Commands")]
        [Tooltip("순서대로 입력해야 할 키 코드 목록")]
        [SerializeField] private KeyCode[] _commandSequence = {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow,
            KeyCode.RightArrow, KeyCode.Space
        };
        [Tooltip("QTE 전체 제한 시간 (초). 패턴에서도 동일값 사용 권장")]
        [SerializeField] private float _timeLimit = 8f;

        // ── 이벤트 ────────────────────────────────────────────────────────
        public event System.Action OnQTESuccess;
        public event System.Action OnQTEFail;

        // ── 런타임 ────────────────────────────────────────────────────────
        private Transform _playerTransform;
        private bool      _qteActive;
        private bool      _qteTriggered;

        // ─────────────────────────────────────────────────────────────────

        public void Initialize(Transform player, float timeLimit)
        {
            _playerTransform = player;
            _timeLimit       = timeLimit;
        }

        private void Update()
        {
            if (_qteTriggered) return;
            if (_playerTransform == null) return;

            // G키 입력 감지
            if (!Input.GetKeyDown(KeyCode.G)) return;
            float dist = Vector2.Distance(
                transform.position, _playerTransform.position);
            if (dist > _interactRadius) return;

            _qteTriggered = true;
            RunQTEAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // ─────────────────────────────────────────────────────────────────
        // QTE 입력 루프
        // ─────────────────────────────────────────────────────────────────

        private async UniTaskVoid RunQTEAsync(CancellationToken ct)
        {
            _qteActive = true;

            // TODO: QTEManager.ShowUI(_commandSequence) 호출 연결 지점
            // 실제 프로젝트에서 QTEManager 싱글턴이 있다면 여기서 호출
            Debug.Log("[NarrowingQTE] QTE Started! Input sequence: "
                      + string.Join(", ", _commandSequence));

            float elapsed  = 0f;
            int   step     = 0;
            bool  success  = false;

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

            _qteActive = false;
            // TODO: QTEManager.HideUI() 호출 연결 지점

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
