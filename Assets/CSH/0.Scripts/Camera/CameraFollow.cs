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
    private CancellationTokenSource start_cts;

    [SerializeField] private Camera camera1;
    [SerializeField] private Camera camera2;
    [SerializeField] PixelPerfectCamera pixelPerfectCamera1;
    [SerializeField] PixelPerfectCamera pixelPerfectCamera2;


    private void Awake()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            player = target.GetComponent<PlayerMove>();
        }
        else 
        { 
            player = target.GetComponent<PlayerMove>();
        }

    }

    private void Start()
    {        
        doscale();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.R))
        doscale(); 
    }

    public async UniTaskVoid CameraSizeRescale(CancellationToken _ct)
    {
        float timer = 0;
        float duration = 3f;

        //pixelPerfectCamera1.assetsPPU = 64;
        //pixelPerfectCamera1.cropFrame = PixelPerfectCamera.CropFrame.Letterbox;

        float fromOrtho = 192f / 2f / 64f;  // 1.5
        float toOrtho = 1080f / 2f / 64f; // 8.4375
        camera2.orthographicSize = fromOrtho;

        #region appu 변경 방식
        /*
        while (!_ct.IsCancellationRequested)
        {
            await UniTask.WaitForFixedUpdate(cancellationToken: _ct).SuppressCancellationThrow();

            if (_ct.IsCancellationRequested) break;

            timer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ease-in-out

            // refResolution: 짝수 스냅
            pixelPerfectCamera1.refResolutionX = SnapToEven(Mathf.RoundToInt(Mathf.Lerp(1024f, 1920f, Mathf.Min(smoothT + 0.1f, 1f))));
            pixelPerfectCamera1.refResolutionY = SnapToEven(Mathf.RoundToInt(Mathf.Lerp(192f, 1080f, smoothT)));
            offset.y = Mathf.Min(t * 2f, 2);

            // PPU: float 보간 후 마지막에만 정수 적용
            float ppu = Mathf.Lerp(fromPPU, toPPU, smoothT);
            pixelPerfectCamera1.assetsPPU = Mathf.RoundToInt(ppu);

            if (t >= 1f) break;
        }*/
        #endregion

        while (!_ct.IsCancellationRequested)
        {
            await UniTask.WaitForFixedUpdate(cancellationToken: _ct).SuppressCancellationThrow();
            if (_ct.IsCancellationRequested) break;

            timer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // orthographicSize 보간 → 부드러운 줌아웃
            camera2.orthographicSize = Mathf.Lerp(fromOrtho, toOrtho, smoothT);
            offset.y = Mathf.Min(t * 2f, 2f);

            if (t >= 1f) break;
        }
        camera1.enabled=(true);
        camera2.enabled=(false);

        pixelPerfectCamera1.enabled = true;
        pixelPerfectCamera1.refResolutionX = 1920;
        pixelPerfectCamera1.refResolutionY = 1080;
        pixelPerfectCamera1.assetsPPU = 64;
        pixelPerfectCamera1.cropFrame = PixelPerfectCamera.CropFrame.None;
        offset.y = 2f;
    }

    private void doscale()
    {
        start_cts?.Cancel();
        start_cts?.Dispose();
        start_cts = new CancellationTokenSource();

        camera1.enabled = (false);
        camera2.enabled = (true);
        pixelPerfectCamera2.enabled = false;

        CameraSizeRescale(start_cts.Token).Forget();
    }

    void LateUpdate()
    {
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
    private void OnDestroy()
    {
        start_cts?.Cancel();
        start_cts?.Dispose();
    }
}