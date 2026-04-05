using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.GraphicsBuffer;
using Random = UnityEngine.Random;

public class Panel : MonoBehaviour
{
    public GameObject[] panel = new GameObject[4];
    public Transform[] panelTransform;

    public float panelmoveSpeed = 1.0f;
    float projectileSpeed = 5f;

    [SerializeField] Color lineColor = Color.red;
    [SerializeField] float lineWidth = 0.05f;

    bool NextPatten = false;

    [SerializeField] GameObject projectilePrefab;
    [SerializeField] EnemyBase enemyBase;

    private LineRenderer[] lineRenderers;

    private Transform playerTransform;

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

        // 패널 먼저 활성화 상태로 LineRenderer 추가 후 비활성화
        lineRenderers = new LineRenderer[panel.Length];
        for (int i = 0; i < panel.Length; i++)
        {
            if (panel[i] == null) continue;

            // SetActive(false) 전에 먼저 활성화해서 AddComponent 가능하게
            panel[i].SetActive(true);

            lineRenderers[i] = panel[i].GetComponent<LineRenderer>();
            if (lineRenderers[i] == null)
                lineRenderers[i] = panel[i].AddComponent<LineRenderer>();

            lineRenderers[i].positionCount = 2;
            lineRenderers[i].startWidth = lineWidth;
            lineRenderers[i].endWidth = lineWidth;
            lineRenderers[i].material = new Material(Shader.Find("Sprites/Default"));
            lineRenderers[i].startColor = lineColor;
            lineRenderers[i].endColor = lineColor;
            lineRenderers[i].enabled = false;

            // LineRenderer 추가 완료 후 비활성화
            panel[i].SetActive(false);
        }

        // EnemyBase.Start()가 먼저 실행될 때까지 1프레임 대기
        await UniTask.Yield();

        // EnemyBase에서 playerTransform 참조 (1프레임 후 안전하게 접근)
        if (enemyBase != null)
            playerTransform = enemyBase.playerTransform;

       

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
        currentOffset = 0;

        for (int i = 0; i < panel.Length; i++)
        {
            if (bossTransform != null)
                panel[i].transform.position = bossTransform.position;

            panel[i].SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: tk1);
        }

 
        await PanelMove(currentOffset);
        await AttackLoop(tk1);
    }
    //async UniTask AutoRotateLoop(CancellationToken token)
    //{
    //    while (!token.IsCancellationRequested)
    //    {
    //        await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: token);
    //        currentOffset = (currentOffset + 1) % panel.Length;
    //        await PanelMove(currentOffset);
    //    }
    //}

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
                {
                    panel[i].transform.position = target;
                    arrived[i] = true;
                }
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
    async UniTask AttackLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (playerTransform == null && enemyBase != null)
                playerTransform = enemyBase.playerTransform;

            if (playerTransform != null)
            {
                for (int i = 0; i < panel.Length; i++)
                {
                    if (lineRenderers[i] != null)
                        lineRenderers[i].enabled = true;
                }
                float aimDuration = 1.0f;
                float elapsed = 0f;
                while (elapsed < aimDuration)
                {
                    for (int i = 0; i < panel.Length; i++)
                    {
                        if (lineRenderers[i] != null && lineRenderers[i].enabled)
                        {
                            lineRenderers[i].SetPosition(0, panel[i].transform.position);
                            lineRenderers[i].SetPosition(1, playerTransform.position);
                        }
                    }
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(token);
                }
                for (int i = 0; i < panel.Length; i++)
                {
                    if (lineRenderers[i] != null)
                        lineRenderers[i].enabled = false;
                }
                Vector3 targetPos = playerTransform.position;
                for (int i = 0; i < panel.Length; i++)
                {
                    FireProjectile(panel[i].transform.position, targetPos);
                }
            }
            currentOffset = (currentOffset + 1) % panel.Length;
            await PanelMove(currentOffset);
        }
    }
    void FireProjectile(Vector3 from, Vector3 to)
    {
        if (projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab, from, Quaternion.identity);

        // 생성 시점의 플레이어 위치만 넘기고 이후 직진
        PanelProjectile projectile = proj.GetComponent<PanelProjectile>();
        if (projectile != null)
            projectile.Init((Vector2)to, projectileSpeed);

        Destroy(proj, 5f);
    }
    //void ChangeSet()
    //{
    //    bool[] arrived = new bool[panel.Length];
    //    int offset = Random.Range(0, panel.Length);
    //    while (!arrived.All(x => x))
    //    {
    //        for (int i = 0; i < panel.Length; i++)
    //        {
    //            if (arrived[i]) continue;

    //            Vector2 current = panel[i].transform.position;
    //            Vector2 target = panelTransform[(i + offset) % panel.Length].position;

    //            panel[i].transform.position = Vector2.MoveTowards(
    //                current,
    //                target,
    //                panelmoveSpeed * Time.deltaTime
    //            );

    //            if (Vector2.Distance(current, target) < 0.01f)
    //                arrived[i] = true;
    //        }

    //    }
    //    LazerShoot();

    //}
    //async UniTask LazerShoot()
    //{
    //    while(NextPatten)
    //    {

    //    }
    //}
}

