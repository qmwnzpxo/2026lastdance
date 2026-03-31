using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.U2D;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private PlayerMove player;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);
    [SerializeField] UnityEngine.U2D.PixelPerfectCamera perfectCamera;
    private CancellationTokenSource _cts;
    //[SerializeField] UnityEditor.Rendering.Universal.PixelPerfectCamera pixelPerfectCamera;
    bool rescaling;

    private void Awake()
    {
        if (target != null)
            player = target.GetComponent<PlayerMove>();
        else if (player == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            player = target.GetComponent<PlayerMove>();
        }

        if(perfectCamera == null)
        {
            perfectCamera = GetComponent<PixelPerfectCamera>();
        }
    }

    private void Start()
    {
        TriggerHitstop();
    }
    public void TriggerHitstop(float duration = 0.08f, float timeScale = 0.05f)
    {
        _cts?.Cancel();
        _cts?.Dispose();

        _cts = new CancellationTokenSource();

        CameraSizeRescale(_cts.Token).Forget();
    }


    public async UniTaskVoid CameraSizeRescale(CancellationToken _ct)
    {
        float timer = 0;
        try
        {
            await UniTask.WaitForSeconds(1.25f,ignoreTimeScale:true,cancellationToken:_ct);
        }
        catch (Exception) { }
        finally
        {
            timer = Mathf.Min(timer + Time.fixedDeltaTime, 1);
            
            Debug.Log("perfect.X" + perfectCamera.refResolutionX);
            perfectCamera.refResolutionX = (int)Mathf.Lerp(1600f, 1920f, timer);

            Debug.Log("perfect.Y" + perfectCamera.refResolutionY);
            perfectCamera.refResolutionY = (int)Mathf.Lerp(900, 1080, timer);

            Debug.Log("perfect.assetsPPU" + perfectCamera.assetsPPU);
            perfectCamera.assetsPPU = (int)Mathf.Lerp(128, 64, timer);
        }

        Debug.Log("CameraSizeRescale() end");
    }

    void LateUpdate()
    {
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}