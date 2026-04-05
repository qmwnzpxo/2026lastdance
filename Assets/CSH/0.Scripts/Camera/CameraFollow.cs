using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;

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

    private void Start()
    {
        cmZoom.gameObject.SetActive(true);
        cmGamePlay.gameObject.SetActive(true);
        ZoomCamera(fromOrtho, toOrtho, duration);
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

        cmBrain.DefaultBlend.Time = defaultBlendTime;
        cmZoom.gameObject.SetActive(false);
        isZooming = false;
    }

    private void OnDestroy()
    {
        zoom_cts?.Cancel();
        zoom_cts?.Dispose();
    }
}