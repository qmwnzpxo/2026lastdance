using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

public class BossShield : MonoBehaviour
{
    public GameObject Shield;
    public GameObject bossTransformObj;

    [SerializeField] LazerPatten lazerPatten;
    Transform bossTransform;

    public float alphaValue = 1.0f;
    private Material mat;

    private CancellationToken tk;

    private void Start()
    {
        mat = Shield.GetComponent<Renderer>().material;
        updateLoop(tk);
    }
 
    async UniTaskVoid updateLoop(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            bossShield();

            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
        }
    }
    private async UniTask bossShield()
    {
        Color color = mat.color;
        color.a = alphaValue;


        if(lazerPatten.allDestory == true)
        {
            for (int k = 0; k < 4; k++)
            {
                alphaValue = Mathf.Max(alphaValue - 0.2f, 0.2f);
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                mat.color =new Color (mat.color.r, mat.color.g, mat.color.b, alphaValue);
            }
        }
        else
        {
            for (int k = 0; k < 4; k++)
            {
                alphaValue = Mathf.Min(alphaValue + 0.2f, 0.6f);
                mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, alphaValue);
            }
        }

        //for (int i = 0; i<lazerPatten.imDestory.Length; i++)
        //{
        //    Debug.Log("1");
        //    if(lazerPatten.imDestory[i] == false)
        //    {
        //        Debug.Log("2");
        //        for (int k = 0; k < i; k++)
        //        {
        //            color.a -= 0.2f;
        //            Debug.Log("3");
        //        }
        //    }
        //    else
        //    {
        //        Debug.Log("4");
        //        for (int k = 0; k < i; k++)
        //        {
        //            Debug.Log("5");
        //            color.a += 0.2f;
        //        }
        //    }
        //}
    }
        
        

    
    
}
