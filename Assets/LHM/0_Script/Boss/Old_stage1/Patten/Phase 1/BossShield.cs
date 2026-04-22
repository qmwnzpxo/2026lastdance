//using UnityEngine;
//using Cysharp.Threading.Tasks;
//using System;
//using System.Threading;


//public class BossShield : MonoBehaviour
//{
//    [Header("References")]
//    public GameObject Shield;
//    [SerializeField] private LazerPatten lazerPatten;

//    [Header("Alpha Settings")]
//    [SerializeField] private float alphaMax = 0.6f;
//    [SerializeField] private float alphaMin = 0.2f;
//    [SerializeField] private float alphaStep = 0.2f;
//    [SerializeField] private float stepInterval = 0.2f;

//    private Material _mat;
//    private float _alphaValue = 1.0f;
//    private bool _shieldActive = true;

//    // 오브젝트 파괴 시 자동 취소
//    public CancellationToken _lifeTk;

//    private void Start()
//    {
//        _lifeTk = this.GetCancellationTokenOnDestroy();

//        if (Shield != null)
//            _mat = Shield.GetComponent<Renderer>().material;

//        UpdateLoop(_lifeTk).Forget();
//    }
//    public void SetShieldActive(bool active)
//    {
//        _shieldActive = active;
//        if (Shield != null)
//            Shield.SetActive(active);
//    }
//    private async UniTaskVoid UpdateLoop(CancellationToken token)
//    {
//        while (!token.IsCancellationRequested)
//        {
//            if (_mat != null && _shieldActive)
//                await UpdateShieldAlpha(token);

//            await UniTask.Delay(
//                TimeSpan.FromSeconds(stepInterval),
//                cancellationToken: token
//            );
//        }
//    }

//    private async UniTask UpdateShieldAlpha(CancellationToken token)
//    {
//        bool shouldFadeOut = lazerPatten != null && lazerPatten.allDestory;

//        for (int k = 0; k < 4; k++)
//        {
//            if (token.IsCancellationRequested) return;

//            _alphaValue = shouldFadeOut
//                ? Mathf.Max(_alphaValue - alphaStep, alphaMin)
//                : Mathf.Min(_alphaValue + alphaStep, alphaMax);

//            if (_mat != null)
//                _mat.color = new Color(_mat.color.r, _mat.color.g, _mat.color.b, _alphaValue);

//            await UniTask.Delay(
//                TimeSpan.FromSeconds(stepInterval),
//                cancellationToken: token
//            );
//        }
//    }
//}

////public class BossShield : MonoBehaviour
////{
////    public GameObject Shield;
////    public GameObject bossTransformObj;

////    [SerializeField] LazerPatten lazerPatten;
////    Transform bossTransform;

////    public float alphaValue = 1.0f;
////    private Material mat;

////    private CancellationToken tk;

////    private void Start()
////    {
////        mat = Shield.GetComponent<Renderer>().material;
////        updateLoop(tk);
////    }
 
////    async UniTaskVoid updateLoop(CancellationToken token)
////    {
////        while(!token.IsCancellationRequested)
////        {
////            bossShield();

////            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
////        }
////    }
////    private async UniTask bossShield()
////    {
////        Color color = mat.color;
////        color.a = alphaValue;


////        if(lazerPatten.allDestory == true)
////        {
////            for (int k = 0; k < 4; k++)
////            {
////                alphaValue = Mathf.Max(alphaValue - 0.2f, 0.2f);
////                await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
////                mat.color =new Color (mat.color.r, mat.color.g, mat.color.b, alphaValue);
////            }
////        }
////        else
////        {
////            for (int k = 0; k < 4; k++)
////            {
////                alphaValue = Mathf.Min(alphaValue + 0.2f, 0.6f);
////                mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, alphaValue);
////            }
////        }

//        //for (int i = 0; i<lazerPatten.imDestory.Length; i++)
//        //{
//        //    Debug.Log("1");
//        //    if(lazerPatten.imDestory[i] == false)
//        //    {
//        //        Debug.Log("2");
//        //        for (int k = 0; k < i; k++)
//        //        {
//        //            color.a -= 0.2f;
//        //            Debug.Log("3");
//        //        }
//        //    }
//        //    else
//        //    {
//        //        Debug.Log("4");
//        //        for (int k = 0; k < i; k++)
//        //        {
//        //            Debug.Log("5");
//        //            color.a += 0.2f;
//        //        }
//        //    }
//        //}
    
        
        

    
    

