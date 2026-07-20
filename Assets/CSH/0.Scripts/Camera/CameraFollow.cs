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

    public Transform target;
    bool isZooming = false;
    bool isfirstZooming = false;
    CinemachineFollow cinemachineFollow;

    private CancellationTokenSource zoom_cts;

    private CancellationTokenSource _lockdownCts;
    private float _savedOrthoSize;
    private bool _isLockdown = false;

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
        try
        {
            float timer = 0f;
            cmZoom.gameObject.SetActive(true);

            var lens = cmZoom.Lens;
            lens.OrthographicSize = from;
            cmZoom.Lens = lens;
            cmBrain.DefaultBlend.Time = 0f;
            SetCameraDumping(0f);
            while (!ct.IsCancellationRequested)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();
                if (ct.IsCancellationRequested) break;

                timer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(timer / zoomDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                lens = cmZoom.Lens;
                lens.OrthographicSize = Mathf.Lerp(from, to, smoothT);
                cmZoom.Lens = lens;
                cinemachineFollow.FollowOffset.y = Mathf.Lerp(0f, 1f, smoothT);

                if (t >= 1f) break;
            }
            SetCameraDumping(1f);
        }
        finally
        {
            cmBrain.DefaultBlend.Time = defaultBlendTime;
            cmZoom.gameObject.SetActive(false);
            isZooming = false;
        }
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

    private async UniTaskVoid LockdownEnterAsync(
        Transform roomCenter, float targetOrtho, float duration, CancellationToken ct)
    {
        // 현재 ortho 저장, 타겟을 방 중앙으로 교체
        _savedOrthoSize = cmGamePlay.Lens.OrthographicSize;
        cmZoom.Follow = roomCenter;

        float from = cmGamePlay.Lens.OrthographicSize;
        float timer = 0f;

        cmBrain.DefaultBlend.Time = 0f;
        SetCameraDumping(0.5f);
        cmZoom.gameObject.SetActive(true);

        var lens = cmZoom.Lens;
        lens.OrthographicSize = from;
        cmZoom.Lens = lens;

        while (!ct.IsCancellationRequested)
        {
            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) break;

            timer += Time.fixedDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));

            lens = cmZoom.Lens;
            lens.OrthographicSize = Mathf.Lerp(from, targetOrtho, t);
            cmZoom.Lens = lens;

            if (timer >= duration) break;
        }
    }

    private async UniTaskVoid LockdownExitAsync(float duration, CancellationToken ct)
    {
        cmZoom.Follow = target;

        float from = cmZoom.Lens.OrthographicSize;
        float to = _savedOrthoSize;
        float timer = 0f;

        while (!ct.IsCancellationRequested)
        {
            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) break;

            timer += Time.fixedDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));

            var lens = cmZoom.Lens;
            lens.OrthographicSize = Mathf.Lerp(from, to, t);
            cmZoom.Lens = lens;

            if (timer >= duration) break;
        }

        SetCameraDumping(1f);
        cmBrain.DefaultBlend.Time = defaultBlendTime;
        cmZoom.gameObject.SetActive(false);
        isZooming = false;
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
        shakeCts?.Cancel();
        shakeCts?.Dispose();
    }
}