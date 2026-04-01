using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;
//using UnityEngine.U2D;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private PlayerMove player;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);
    private CancellationTokenSource _cts;
    private CancellationTokenSource start_cts;
    [SerializeField] PixelPerfectCamera pixelPerfectCamera;
    bool rescaling;

    private int SnapToEven(int value) => value & ~1;
    private void Awake()
    {
        if (target != null)
            player = target.GetComponent<PlayerMove>();
        else if (player == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            player = target.GetComponent<PlayerMove>();
        }

        if(pixelPerfectCamera == null)
        {
            pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
        }

    }

    private void Start()
    {
        TriggerHitstop();

        start_cts?.Cancel();
        start_cts?.Dispose();
        start_cts = new CancellationTokenSource();
        CameraSizeRescale(start_cts.Token).Forget();
    }

    public void TriggerHitstop(float duration = 0.08f, float timeScale = 0.05f)
    {
        _cts?.Cancel();
        _cts?.Dispose();

        _cts = new CancellationTokenSource();
    }


    public async UniTaskVoid CameraSizeRescale(CancellationToken _ct)
    {
        float timer = 0;
        float duration = 3f;
    float fromPPU = 128f;
    float toPPU = 64f;
        pixelPerfectCamera.assetsPPU = 64;
        pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.Letterbox;
        while (!_ct.IsCancellationRequested)
        {
            await UniTask.WaitForFixedUpdate(cancellationToken: _ct).SuppressCancellationThrow();

            if (_ct.IsCancellationRequested) break;

            timer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ease-in-out

            // refResolution: 짝수 스냅
            pixelPerfectCamera.refResolutionX = SnapToEven(Mathf.RoundToInt(Mathf.Lerp(1024f, 1920f, Mathf.Min(smoothT + 0.1f, 1f))));
            pixelPerfectCamera.refResolutionY = SnapToEven(Mathf.RoundToInt(Mathf.Lerp(192f, 1080f, smoothT)));
            offset.y = Mathf.Min(t * 2f, 2);

            // PPU: float 보간 후 마지막에만 정수 적용
            float ppu = Mathf.Lerp(fromPPU, toPPU, smoothT);
            pixelPerfectCamera.assetsPPU = Mathf.RoundToInt(ppu);

            if (t >= 1f) break;
        }
        pixelPerfectCamera.refResolutionX = 1920;
        pixelPerfectCamera.refResolutionY = 1080;
        offset.y = 2f;
    }

    void LateUpdate()
    {
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}