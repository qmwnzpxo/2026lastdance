using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

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

    private void LateUpdate()
    {
        if (isfirstZooming == false)
        {isfirstZooming = true;
            startWhenZoom().Forget();
        }
    }

    private void Start()
    {
        cmZoom.gameObject.SetActive(false);
        cmGamePlay.gameObject.SetActive(true);

    }
    async UniTask startWhenZoom()
    {
        await UniTask.Delay(50);
        Debug.Log("zoomStart");
        ZoomCamera(fromOrtho, toOrtho, duration);
        await UniTask.WaitForSeconds(2f);

        return;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)&&!isZooming)
            ZoomCamera(fromOrtho, toOrtho, duration);
    }

    public void Zoom(float targetOrtho, float zoomDuration)
    {
        if(isZooming) return; // 이미 줌 중이면 무시
        ZoomCamera(cmZoom.Lens.OrthographicSize, targetOrtho, zoomDuration);
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
        cmBrain.DefaultBlend.Time = defaultBlendTime;
        cmZoom.gameObject.SetActive(false);
        isZooming = false;
    }

    public void SetCameraDumping(float value)
    {
        cmZoom.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping = new Vector3(value, value, 1);
        cmGamePlay.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping = new Vector3(value, value, 1);

    }

    private void OnDestroy()
    {
        zoom_cts?.Cancel();
        zoom_cts?.Dispose();
    }
}