using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using static UnityEngine.GraphicsBuffer;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;

using Random = UnityEngine.Random;

public class Panel : MonoBehaviour
{
    public GameObject[] panel = new GameObject[4];
    public Transform[] panelTransform;

    public float panelmoveSpeed = 1.0f;

    [SerializeField] EnemyBase enemyBase;

    bool AllonPanel = false;
    bool[] testbool;

    public Transform bossTransform;
    Vector2 currentPos;
    private int currentOffset = 0;

    private CancellationToken tk1;
    async UniTask Start()
    {
        tk1 = this.GetCancellationTokenOnDestroy(); // 추가
        testbool = new bool[panel.Length];


        foreach (GameObject p in panel)
            if (p != null) p.SetActive(false);

        updateLoop(tk1).Forget();



    }
    async UniTaskVoid updateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                await PanelSpawn();
            }
            await UniTask.Yield(); 
        }
    }
    async UniTask PanelSpawn()
    {
        for (int i = 0; i < panel.Length; i++)
        {
            if (bossTransform != null)
                panel[i].transform.position = bossTransform.position;

            panel[i].SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
        }

        await PanelMove(currentOffset);
        await AutoRotateLoop(tk1);
    }
    async UniTask AutoRotateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: token);
            currentOffset = (currentOffset + 1) % panel.Length;
            await PanelMove(currentOffset);
        }
    }

    async UniTask PanelMove(int offset)
    {
        bool[] arrived = new bool[panel.Length];

        while (!arrived.All(x => x))
        {
            for (int i = 0; i < panel.Length; i++)
            {
                if (arrived[i]) continue;

                Vector2 current = panel[i].transform.position;
                Vector2 target = panelTransform[(i + offset) % panel.Length].position;

                panel[i].transform.position = Vector2.MoveTowards(
                    current,
                    target,
                    panelmoveSpeed * Time.deltaTime
                );

                if (Vector2.Distance(current, target) < 0.01f)
                    arrived[i] = true;
            }
            await UniTask.Yield();
        }
    }
    //async UniTask PanelMove()
    //{
    //    bool[] arrived = new bool[panel.Length];

    //    while (!arrived.All(x => x))
    //    {
    //        for (int i = 0; i < panel.Length; i++)
    //        {
    //            if (arrived[i]) continue;

    //            Vector2 current = panel[i].transform.position;
    //            Vector2 target = panelTransform[i].position;

    //            panel[i].transform.position = Vector2.MoveTowards(
    //                current,
    //                target,
    //                panelmoveSpeed * Time.deltaTime
    //            );

    //            if (Vector2.Distance(current, target) < 0.01f)
    //                arrived[i] = true;
    //        }
    //        await UniTask.Yield(); 
    //    }
    //}
    //async UniTask PanelMove()
    //{


    //    //정해진 빈 오브젝트의 위치로 이동한뒤
    //    //광선 발사 
    //    //발사후 랜덤으로 자리가 비어있는 빈오브젝트의 위치로 이동
    //    //반복
    //    for (int i = 0; i < panel.Length; i++)
    //    { 
    //        Vector2 g = panel[i].transform.position;
    //        Vector2 p = panelTransform[i].localPosition;
    //        Vector2 target = g - p;
    //        currentPos = target;
    //    }
    //    if (AllonPanel)
    //    {
    //        Vector2 direction = (currentPos - (Vector2)transform.position);
    //    }
    //}
    void ChangeSet()
    {
        bool[] arrived = new bool[panel.Length];
        int offset = Random.Range(0, panel.Length);
        while (!arrived.All(x => x))
        {
            for (int i = 0; i < panel.Length; i++)
            {
                if (arrived[i]) continue;

                Vector2 current = panel[i].transform.position;
                Vector2 target = panelTransform[(i + offset) % panel.Length].position;

                panel[i].transform.position = Vector2.MoveTowards(
                    current,
                    target,
                    panelmoveSpeed * Time.deltaTime
                );

                if (Vector2.Distance(current, target) < 0.01f)
                    arrived[i] = true;
            }
            
        }
        
        
    }
}
