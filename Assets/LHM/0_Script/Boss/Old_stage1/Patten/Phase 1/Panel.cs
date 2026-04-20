using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

public class Panel : MonoBehaviour
{
    //ObjectHp objectHp;
    public static Panel Instance;

    public GameObject[] panel = new GameObject[7];
    public Transform[] panelTransform;

    public float panelmoveSpeed = 1.0f;
    float projectileSpeed = 5f;

    public float currentHp = 1f;
    public bool objisDead = false;

    [SerializeField] Color lineColor = Color.red;
    [SerializeField] float lineWidth = 0.05f;

    //bool NextPatten = false;

    [SerializeField] GameObject projectilePrefab;
    [SerializeField] EnemyBase enemyBase;

    private LineRenderer[] lineRenderers;

    private Transform playerTransform;

    bool AllonPanel = false;
    bool[] ispaneldeadbool = new bool[7];
    public bool[] imDestorypanel = new bool[7];
    public bool IsPanelCleared { get; private set; } = false;
    public bool panelDis = false;

    public Transform bossTransform;
    Vector2 currentPos;
    private int currentOffset = 0;

    private CancellationToken _lifeTk;

    public bool destorypanel1 = false;
    public bool destorypanel2 = false;
    public bool destorypanel3 = false;
    public bool destorypanel4 = false;
    public bool destorypanel5 = false;
    public bool destorypanel6 = false;
    public bool destorypanel7 = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        imDestorypanel = new bool[7];
    }
    //async UniTask Start()
    //{
    //    _lifeTk = this.GetCancellationTokenOnDestroy();
    //    testbool = new bool[panel.Length];

    //    lineRenderers = new LineRenderer[panel.Length];
    //    for (int i = 0; i < panel.Length; i++)
    //    {
    //        if (panel[i] == null) continue;

    //        panel[i].SetActive(true);

    //        lineRenderers[i] = panel[i].GetComponent<LineRenderer>();
    //        if (lineRenderers[i] == null)
    //            lineRenderers[i] = panel[i].AddComponent<LineRenderer>();

    //        lineRenderers[i].positionCount = 2;
    //        lineRenderers[i].startWidth = lineWidth;
    //        lineRenderers[i].endWidth = lineWidth;
    //        lineRenderers[i].material = new Material(Shader.Find("Sprites/Default"));
    //        lineRenderers[i].startColor = lineColor;
    //        lineRenderers[i].endColor = lineColor;
    //        lineRenderers[i].enabled = false;
    //        panel[i].SetActive(false);
    //    }

    //    await UniTask.Yield();


    //    if (enemyBase != null)
    //        playerTransform = enemyBase.playerTransform;



    //    foreach (GameObject p in panel)
    //        if (p != null) p.SetActive(false);

    //    updateLoop(tk1).Forget();



    //}
    private async UniTask Start()
    {
        _lifeTk = this.GetCancellationTokenOnDestroy();

        lineRenderers = new LineRenderer[panel.Length];
        for (int i = 0; i < panel.Length; i++)
        {
            if (panel[i] == null) continue;

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
            panel[i].SetActive(false);
        }

        await UniTask.Yield();

        if (enemyBase != null)
            playerTransform = enemyBase.playerTransform;
    }



    public void OnPanelDestroyed(GameObject destroyedPanel)
    {
        int index = System.Array.IndexOf(panel, destroyedPanel);
        if (index < 0) return;

        imDestorypanel[index] = true;
        Debug.Log($"파괴된 판넬 인덱스: {index}번");


        if (imDestorypanel.All(x => x))
        {
            IsPanelCleared = true;
            Debug.Log("판넬 전부 파괴 — IsPanelCleared = true");
        }
    }

    public async UniTask StartPatternAsync(CancellationToken token)
    {
        currentOffset = 0;
        IsPanelCleared = false;

        await PanelSpawn(token);
    }
    public void ResetPanel()
    {
        IsPanelCleared = false;

        for (int i = 0; i < imDestorypanel.Length; i++)
            imDestorypanel[i] = false;

        foreach (GameObject p in panel)
            if (p != null) p.SetActive(false);
    }
    public void NotifyPanelDestroyed()
    {
        bool[] currentpanel = {destorypanel1, destorypanel2 , destorypanel3 ,
        destorypanel4,destorypanel5,destorypanel6,destorypanel7};

        for(int i =0; i<panel.Length;i++)
        {
            if (panel[i] == null) continue;
            if (currentpanel[i] && !ispaneldeadbool[i])
            {
                panel[i].SetActive(false);
            }
            if (!currentpanel[i] && ispaneldeadbool[i])
            {
                panel[i].SetActive(true);   
            }

            ispaneldeadbool[i] = currentpanel[i];

            Debug.Log(imDestorypanel[i]);
            for (int j = 0; j < currentpanel.Length; j++)
            {
                {
                    imDestorypanel[j] = currentpanel[j];

                }
            }

            bool allTrue = imDestorypanel.All(x => x);
            if (allTrue)
            {
                AllonPanel = true;
            }

        }
        
    }
    private async UniTask PanelSpawn(CancellationToken token)
    {
        for (int i = 0; i < panel.Length; i++)
        {
            if (panel[i] == null) continue;
            if (imDestorypanel[i]) continue; 

            panel[i].transform.position = transform.position;
            panel[i].SetActive(true);

            await UniTask.Delay(
                TimeSpan.FromSeconds(0.2f),
                cancellationToken: token
            );
        }
        await PanelMove(currentOffset, token);
        await AttackLoop(token);
    }
    private async UniTask PanelMove(int offset, CancellationToken token)
    {
        bool[] arrived = new bool[panel.Length];

        while (!arrived.All(x => x))
        {
            if (token.IsCancellationRequested) return;

            for (int i = 0; i < panel.Length; i++)
            {
                if (arrived[i] || panel[i] == null) continue;
                if (imDestorypanel[i])
                {
                    arrived[i] = true;
                    continue;
                }

                Vector2 current = panel[i].transform.position;
                Vector2 target = panelTransform[(i + offset) % panel.Length].position;

                panel[i].transform.position = Vector2.MoveTowards(
                    current, target, panelmoveSpeed * Time.deltaTime
                );

                if (Vector2.Distance(current, target) < 0.01f)
                {
                    panel[i].transform.position = target;
                    arrived[i] = true;
                }
            }

            await UniTask.Yield(token);
        }
    }

    private async UniTask AttackLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (IsPanelCleared) break;

            if (playerTransform == null && enemyBase != null)
                playerTransform = enemyBase.playerTransform;

            if (playerTransform != null)
            {

                SetLineRenderers(true);

                float aimDuration = 2.0f;
                float elapsed = 0f;

                while (elapsed < aimDuration && !token.IsCancellationRequested)
                {
                    for (int i = 0; i < panel.Length; i++)
                    {
                        if (lineRenderers[i] == null || !lineRenderers[i].enabled) continue;

                        
                        if (imDestorypanel[i])
                        {
                            lineRenderers[i].enabled = false;
                            continue;
                        }

                        lineRenderers[i].SetPosition(0, panel[i].transform.position);
                        lineRenderers[i].SetPosition(1, playerTransform.position);
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(token);
                }
                SetLineRenderers(false);
                Vector3 targetPos = playerTransform.position;
                for (int i = 0; i < panel.Length; i++)
                {
                    if (panel[i] == null) continue;

                    if (imDestorypanel[i]) continue;

                    FireProjectile(panel[i].transform.position, targetPos);
                }
            }
            currentOffset = (currentOffset + 1) % panel.Length;
            await PanelMove(currentOffset, token);
        }
        CleanUp();
    }
    private void SetLineRenderers(bool enabled)
    {
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            if (lineRenderers[i] == null) continue;

   
            if (enabled && imDestorypanel[i]) continue;

            lineRenderers[i].enabled = enabled;
        }
    }

    private void FireProjectile(Vector3 from, Vector3 to)
    {
        if (projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab, from, Quaternion.identity);
        PanelProjectile projectile = proj.GetComponent<PanelProjectile>();

        if (projectile != null)
            projectile.Init((Vector2)to, projectileSpeed);

        Destroy(proj, 5f);
    }

    private void CleanUp()
    {
        SetLineRenderers(false);
        foreach (GameObject p in panel)
            if (p != null) p.SetActive(false);
    }


    //void FireProjectile(Vector3 from, Vector3 to)
    //{
    //    if (projectilePrefab == null) return;

    //    GameObject proj = Instantiate(projectilePrefab, from, Quaternion.identity);

    //    // 생성 시점의 플레이어 위치만 넘기고 이후 직진
    //    PanelProjectile projectile = proj.GetComponent<PanelProjectile>();
    //    if (projectile != null)
    //        projectile.Init((Vector2)to, projectileSpeed);

    //    Destroy(proj, 5f);
    //}
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

    //async UniTaskVoid updateLoop(CancellationToken token)
    //{
    //    while (!token.IsCancellationRequested)
    //    {
    //        if (Input.GetKeyDown(KeyCode.V))
    //        {
    //            await PanelSpawn();
    //        }
    //        await UniTask.Yield(); 
    //    }
    //}
    //async UniTask PanelSpawn()
    //{
    //    currentOffset = 0;

    //    for (int i = 0; i < panel.Length; i++)
    //    {
    //        if (bossTransform != null)
    //            panel[i].transform.position = bossTransform.position;

    //        panel[i].SetActive(true);
    //        await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: tk1);
    //    }


    //    await PanelMove(currentOffset);
    //    await AttackLoop(tk1);
    //}
    //async UniTask AutoRotateLoop(CancellationToken token)
    //{
    //    while (!token.IsCancellationRequested)
    //    {
    //        await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: token);
    //        currentOffset = (currentOffset + 1) % panel.Length;
    //        await PanelMove(currentOffset);
    //    }
    //}

    //async UniTask PanelMove(int offset)
    //{
    //    bool[] arrived = new bool[panel.Length];

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
    //            {
    //                panel[i].transform.position = target;
    //                arrived[i] = true;
    //            }
    //        }
    //        await UniTask.Yield();
    //    }
    //}
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
}

