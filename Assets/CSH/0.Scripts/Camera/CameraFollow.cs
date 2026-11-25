using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
//using static UnityEngine.Rendering.DebugUI;

public class CameraFollow : MonoBehaviour
{
    [Header("메인 카메라")]
    [SerializeField] private Camera mainCamera;
    private CinemachineBrain cmBrain;
    float defaultBlendTime;

    [Header("버추얼 카메라")]
    [SerializeField] private CinemachineCamera cmZoom;
    [SerializeField] private CinemachineCamera cmGamePlay;

    [Header("줌 설정")]
    [SerializeField] private float duration = 3f;
    [SerializeField] private float fromOrtho = 1.5f;
    [SerializeField] private float toOrtho = 8.4375f;
    [Tooltip("인트로 줌 최종 크기(toOrtho)가 게임플레이 카메라 크기와 다를 때, 전환 전에 맞추는 시간")]
    [SerializeField] private float introSettleDuration = 0.5f;

    public Transform target;
    bool isZooming = false;
    bool isfirstZooming = false;
    CinemachineFollow cinemachineFollow;

    private CancellationTokenSource zoom_cts;

    private CancellationTokenSource _lockdownCts;
    private float _savedOrthoSize;
    private bool _isLockdown = false;
    private bool _lockdownExiting = false; // 락다운 해제 줌아웃 진행 중
    private Transform _lockdownPivot;

    // 카메라 흔들림(지진) 기능용 CancellationTokenSource
    private CancellationTokenSource shakeCts;

    public static bool ZoomRequest { get; set; } = false;



    // ────────────────────────────────────────
    #region 초기화 
    private void Awake()
    {
        if (target == null)
            target = FindFirstObjectByType<PlayerMove>().transform;

        cmZoom.Follow = target;
        cmGamePlay.Follow = target;

        cmBrain = mainCamera.GetComponent<CinemachineBrain>();
        defaultBlendTime = cmBrain.DefaultBlend.Time;
        try
        {
            cinemachineFollow = cmZoom.GetComponent<CinemachineFollow>();
        }
        catch (System.Exception e)
        {
            Debug.LogError("CinemachineFollow 컴포넌트를 찾을 수 없습니다: " + e.Message);
        }

    }

    private void Start()
    {
        cmZoom.gameObject.SetActive(false);
        cmGamePlay.gameObject.SetActive(true);
        SnapToTarget(); // 씬 시작 시 배치 위치에서 플레이어까지 댐핑으로 끌려오는 것 방지
#if UNITY_EDITOR
        if(ZoomRequest)
#endif
            startWhenZoom().Forget();

    }
    #endregion

    // ──────────────────────────────────────
    #region 생명주기
    private void LateUpdate()
    {
        if (isfirstZooming == false)
        {
            isfirstZooming = true;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
    if (Input.GetKeyDown(KeyCode.R)&&!isZooming)
            ZoomCamera(fromOrtho, toOrtho, duration);
#endif
  }
    #endregion

    // ────────────────────────────────────
    #region 줌 
    public void Zoom(float targetOrtho, float zoomDuration)
    {
        if(isZooming) return; // 이미 줌 중이면 무시
        ZoomCamera(cmZoom.Lens.OrthographicSize, targetOrtho, zoomDuration);
    }

    async UniTask startWhenZoom()
    {
        await UniTask.Delay(50, cancellationToken:this.destroyCancellationToken);
        Debug.Log("zoomStart");
        ZoomCamera(fromOrtho, toOrtho, duration);

        return;
    }

    private void ZoomCamera(float from, float to, float zoomDuration)
    {
        cmBrain.DefaultBlend.Time = 0f;
        // 기존 줌 취소 후 새 줌 시작 → 동시 실행 없음
        zoom_cts?.Cancel();
        zoom_cts?.Dispose();
        zoom_cts = new CancellationTokenSource();
        ZoomAsync(from, to, zoomDuration, zoom_cts.Token).Forget();
    }

    private async UniTaskVoid ZoomAsync(float from, float to, float zoomDuration, CancellationToken ct)
    {
        isZooming = true;

        cmBrain.DefaultBlend.Time = 0f;
        SetCameraDumping(0f);
        SetZoomOrtho(from);
        cmZoom.gameObject.SetActive(true);

        // 렌더 프레임마다 갱신 (FixedUpdate 주기 계단 현상 방지)
        float timer = 0f;
        while (timer < zoomDuration)
        {
            bool canceled = await UniTask
                .Yield(PlayerLoopTiming.Update, ct)
                .SuppressCancellationThrow();
            // 취소 = 새 줌이 cmZoom을 이어받았거나 파괴됨 → 정리하지 않는다
            if (canceled) return;

            timer += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / zoomDuration));
            SetZoomOrtho(Mathf.Lerp(from, to, smoothT));
            cinemachineFollow.FollowOffset.y = Mathf.Lerp(0f, 1f, smoothT);
        }
        SetZoomOrtho(to);
        cinemachineFollow.FollowOffset.y = 1f;

        // 최종 크기가 게임플레이 카메라와 다르면 컷 전에 부드럽게 맞춘다 (안 맞추면 전환 순간 크기가 튐)
        float gameplayOrtho = cmGamePlay.Lens.OrthographicSize;
        if (!Mathf.Approximately(to, gameplayOrtho))
        {
            if (await AnimateZoomOrtho(to, gameplayOrtho, introSettleDuration, ct)) return;
        }

        SetCameraDumping(1f);
        isZooming = false;
        await CutToGameplayAsync(ct);
    }

    /// <summary>
    /// cmZoom → cmGamePlay 전환.
    /// 브레인 기본 블렌드(Custom 0→0 커브)를 타면 1초간 멈췄다가 한 번에 튀므로,
    /// 게임플레이 카메라를 줌 카메라의 현재 위치로 맞춘 뒤 컷 전환한다.
    /// 이후 게임플레이 카메라의 댐핑으로 타겟까지 부드럽게 따라간다.
    /// 반환값: 취소 여부
    /// </summary>
    private async UniTask<bool> CutToGameplayAsync(CancellationToken ct)
    {
        var zoomState = cmZoom.State;
        cmGamePlay.ForceCameraPosition(zoomState.GetFinalPosition(), zoomState.GetFinalOrientation());

        cmBrain.DefaultBlend.Time = 0f;
        cmZoom.gameObject.SetActive(false);

        // 브레인이 컷을 처리한 뒤(LateUpdate 이후)에 블렌드 시간 복구
        bool canceled = await UniTask
            .Yield(PlayerLoopTiming.PostLateUpdate, ct)
            .SuppressCancellationThrow();
        if (canceled) return true;

        cmBrain.DefaultBlend.Time = defaultBlendTime;
        return false;
    }

    /// <summary>
    /// 다음 프레임에 댐핑 없이 타겟 위치로 즉시 이동시킨다.
    /// 플레이어 순간이동(컷씬 스킵·텔레포트) 직후 호출 → 빈 배경이 보이며 끌려가는 현상 방지.
    /// </summary>
    public void SnapToTarget()
    {
        if (cmGamePlay != null) cmGamePlay.PreviousStateIsValid = false;
        if (cmZoom != null) cmZoom.PreviousStateIsValid = false;
    }

    /// <summary>씬의 CameraFollow를 찾아 SnapToTarget 호출 (외부 스크립트용)</summary>
    public static void SnapMainToTarget()
    {
        var cf = FindFirstObjectByType<CameraFollow>();
        if (cf != null) cf.SnapToTarget();
    }

    public void SetCameraDumping(float value)
    {
        cmZoom.gameObject.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping = new Vector3(value, value, 1);
        cmGamePlay.gameObject.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping = new Vector3(value, value, 1);

    }
#endregion

    // ────────────────────────────────────
    #region 락다운 카메라

    /// <summary>
    /// 락다운 시작
    /// roomCenter - 카메라가 바라볼 위치, targetOrtho - 락다운 시 카메라 크기, duration - 전환 시간
    /// </summary>
    public void StartLockdown(Transform roomCenter, float targetOrtho, float duration)
    {
        if (_isLockdown) return;
        _isLockdown = true;

        _lockdownCts?.Cancel();
        _lockdownCts?.Dispose();
        _lockdownCts = new CancellationTokenSource();

        LockdownEnterAsync(roomCenter, targetOrtho, duration, _lockdownCts.Token).Forget();
    }

    // 락다운 해제
    public void EndLockdown(float duration = 1f)
    {
        if (!_isLockdown) return;
        _isLockdown = false;

        _lockdownCts?.Cancel();
        _lockdownCts?.Dispose();
        _lockdownCts = new CancellationTokenSource();

        LockdownExitAsync(duration, _lockdownCts.Token).Forget();
    }

    // 락다운 중 cmZoom이 따라가는 피벗.
    // 위치 이동과 줌을 같은 곡선으로 직접 움직여서, 순간이동이나 둘의 박자 차이가 없게 한다.
    private Transform LockdownPivot
    {
        get
        {
            if (_lockdownPivot == null)
                _lockdownPivot = new GameObject("LockdownCameraPivot").transform;
            return _lockdownPivot;
        }
    }

    private async UniTaskVoid LockdownEnterAsync(
        Transform roomCenter, float targetOrtho, float duration, CancellationToken ct)
    {
        // 해제 연출 도중 재진입(레드대쉬 재생 등)이면 현재 상태에서 이어서 시작
        bool resuming = _lockdownExiting;
        _lockdownExiting = false;

        if (!resuming)
            _savedOrthoSize = cmGamePlay.Lens.OrthographicSize;

        float fromOrthoSize = resuming ? cmZoom.Lens.OrthographicSize : cmGamePlay.Lens.OrthographicSize;
        // 시작점 = 지금 화면이 보고 있는 위치 → 진입 순간 카메라가 튀지 않음
        Vector3 fromPos = resuming ? LockdownPivot.position : CurrentZoomPivotPosition();

        var pivot = LockdownPivot;
        pivot.position = fromPos;

        cmZoom.Follow = pivot;
        SetZoomDamping(0f); // 피벗이 이미 부드럽게 움직이므로 댐핑을 겹치지 않는다
        cmBrain.DefaultBlend.Time = 0f;
        SetZoomOrtho(fromOrthoSize);
        if (!cmZoom.gameObject.activeSelf)
        {
            cmZoom.PreviousStateIsValid = false;
            cmZoom.gameObject.SetActive(true);
        }

        await AnimateLockdownAsync(fromPos, () => roomCenter.position, fromOrthoSize, targetOrtho, duration, ct);
    }

    private async UniTaskVoid LockdownExitAsync(float duration, CancellationToken ct)
    {
        _lockdownExiting = true;

        Vector3 fromPos = LockdownPivot.position;
        float fromOrthoSize = cmZoom.Lens.OrthographicSize;

        // 끝점 = 게임플레이 카메라가 실제로 있을 위치 (플레이어가 움직여도 매 프레임 갱신)
        bool canceled = await AnimateLockdownAsync(
            fromPos, GameplayEquivalentPivotPosition, fromOrthoSize, _savedOrthoSize, duration, ct);

        // 취소됐다면 다른 전환(재진입)이 카메라를 이어받은 상태 → 정리하지 않는다
        if (canceled) return;

        _lockdownExiting = false;
        SetCameraDumping(1f);
        isZooming = false;
        await CutToGameplayAsync(ct);
        if (cmZoom != null && !_isLockdown) cmZoom.Follow = target;
    }

    // 피벗 위치와 렌즈 크기를 같은 SmoothStep 곡선으로 함께 보간. 반환값: 취소 여부
    private async UniTask<bool> AnimateLockdownAsync(
        Vector3 fromPos, System.Func<Vector3> toPos,
        float fromOrthoSize, float toOrthoSize, float duration, CancellationToken ct)
    {
        var pivot = LockdownPivot;
        float timer = 0f;

        while (timer < duration)
        {
            bool canceled = await UniTask
                .Yield(PlayerLoopTiming.Update, ct)
                .SuppressCancellationThrow();
            if (canceled) return true;

            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
            pivot.position = Vector3.Lerp(fromPos, toPos(), t);
            SetZoomOrtho(Mathf.Lerp(fromOrthoSize, toOrthoSize, t));
        }

        pivot.position = toPos();
        SetZoomOrtho(toOrthoSize);
        return ct.IsCancellationRequested;
    }

    // 현재 화면 위치를 cmZoom이 그대로 재현하려면 피벗이 있어야 할 위치
    private Vector3 CurrentZoomPivotPosition()
    {
        Vector3 cam = mainCamera.transform.position;
        Vector3 offset = cinemachineFollow != null ? cinemachineFollow.FollowOffset : Vector3.zero;
        return new Vector3(cam.x - offset.x, cam.y - offset.y, target.position.z);
    }

    // 게임플레이 카메라가 보는 위치를 cmZoom이 재현하려면 피벗이 있어야 할 위치
    private Vector3 GameplayEquivalentPivotPosition()
    {
        var gameFollow = cmGamePlay.GetComponent<CinemachineFollow>();
        Vector3 gameOffset = gameFollow != null ? gameFollow.FollowOffset : Vector3.zero;
        Vector3 zoomOffset = cinemachineFollow != null ? cinemachineFollow.FollowOffset : Vector3.zero;
        Vector3 p = target.position;
        return new Vector3(p.x + gameOffset.x - zoomOffset.x, p.y + gameOffset.y - zoomOffset.y, p.z);
    }

    private void SetZoomDamping(float value)
    {
        if (cinemachineFollow != null)
            cinemachineFollow.TrackerSettings.PositionDamping = new Vector3(value, value, 1);
    }

    // 렌더 프레임마다(Update) 렌즈를 갱신해 FixedUpdate 주기로 인한 계단 현상을 없앤다.
    // 반환값: 취소 여부
    private async UniTask<bool> AnimateZoomOrtho(float from, float to, float duration, CancellationToken ct)
    {
        if (duration <= 0f)
        {
            SetZoomOrtho(to);
            return ct.IsCancellationRequested;
        }

        float timer = 0f;
        while (timer < duration)
        {
            bool canceled = await UniTask
                .Yield(PlayerLoopTiming.Update, ct)
                .SuppressCancellationThrow();
            if (canceled) return true;

            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
            SetZoomOrtho(Mathf.Lerp(from, to, t));
        }
        return false;
    }

    private void SetZoomOrtho(float size)
    {
        var lens = cmZoom.Lens;
        lens.OrthographicSize = size;
        cmZoom.Lens = lens;
    }

    #endregion

    // ────────────────────────────────────
    #region 카메라 흔들림 (지진 효과)
    /// <summary>
    /// 지정된 강도와 시간동안 카메라를 흔듭니다.
    /// </summary>
    public void ShakeCamera(float duration, float magnitude)
    {
        shakeCts?.Cancel();
        shakeCts?.Dispose();
        shakeCts = new CancellationTokenSource();
        ShakeAsync(duration, magnitude, shakeCts.Token).Forget();
    }

    private async UniTaskVoid ShakeAsync(float duration, float magnitude, CancellationToken ct)
    {
        float elapsed = 0f;
        
        var followZoom = cmZoom.GetComponent<CinemachineFollow>();
        var followGame = cmGamePlay.GetComponent<CinemachineFollow>();

        Vector3 originalOffsetZoom = followZoom != null ? followZoom.FollowOffset : Vector3.zero;
        Vector3 originalOffsetGame = followGame != null ? followGame.FollowOffset : Vector3.zero;

        while (elapsed < duration && !ct.IsCancellationRequested)
        {
            float xOffset = Random.Range(-1f, 1f) * magnitude;
            float yOffset = Random.Range(-1f, 1f) * magnitude;

            if (followZoom != null)
            {
                followZoom.FollowOffset = new Vector3(originalOffsetZoom.x + xOffset, originalOffsetZoom.y + yOffset, originalOffsetZoom.z);
            }
            if (followGame != null)
            {
                followGame.FollowOffset = new Vector3(originalOffsetGame.x + xOffset, originalOffsetGame.y + yOffset, originalOffsetGame.z);
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 오프셋 원상 복구
        if (followZoom != null) followZoom.FollowOffset = originalOffsetZoom;
        if (followGame != null) followGame.FollowOffset = originalOffsetGame;
    }
    #endregion

    private void OnDestroy()
    {
        zoom_cts?.Cancel();
        zoom_cts?.Dispose();
        _lockdownCts?.Cancel();
        _lockdownCts?.Dispose();
        if (_lockdownPivot != null) Destroy(_lockdownPivot.gameObject);
        shakeCts?.Cancel();
        shakeCts?.Dispose();
    }
}