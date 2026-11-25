using System.Collections.Generic;
using UnityEngine;

public class EnemyBulletPool : PersistentSingleton<EnemyBulletPool>
{

    [System.Serializable]
    public class BulletEntry
    {
        public string key;
        public GameObject prefab;
        public int minPoolSize;
        public int maxPoolSize;

        [HideInInspector] public int currentTotal; // 현재까지 생성된 총 수
        [HideInInspector] public Transform parent;  // 타입별 부모 오브젝트
    }

    [SerializeField] private List<BulletEntry> bulletEntries;

    private Dictionary<string, Queue<GameObject>> pools;
    private Dictionary<string, BulletEntry> entryMap;

    // 현재 대여 중(활성)인 투사체 → 키
    private readonly Dictionary<GameObject, string> activeBullets = new();
    private readonly List<GameObject> returnBuffer = new();

    /// <summary>현재 활성(대여 중)인 투사체 수</summary>
    public int ActiveCount => activeBullets.Count;


    // ── 초기화 ──────────────────────────────────
    #region
    protected override void Awake()
    {
        base.Awake();
        InitPools();
    }

    private void InitPools()
    {
        pools = new Dictionary<string, Queue<GameObject>>();
        entryMap = new Dictionary<string, BulletEntry>();

        foreach (var entry in bulletEntries)
        {
            // 타입별 부모 오브젝트 생성 (매니저 하위)
            var parentObj = new GameObject($"{entry.key}_Pool");
            parentObj.transform.SetParent(transform);
            entry.parent = parentObj.transform;

            var queue = new Queue<GameObject>();

            // minPoolSize 만큼 미리 생성
            for (int i = 0; i < entry.minPoolSize; i++)
            {
                var obj = CreateBullet(entry);
                queue.Enqueue(obj);
            }

            pools[entry.key] = queue;
            entryMap[entry.key] = entry;
        }
    }
    #endregion

    #region// ── 대여 ────────────────────────────────────
    /// <summary>
    /// 풀에서 투사체를 꺼낸다.
    /// 풀이 비었으면 추가 생성, maxPoolSize 초과 시 null 반환.
    /// </summary>
    public GameObject Get(string key)
    {
        if (!pools.TryGetValue(key, out var queue) ||
            !entryMap.TryGetValue(key, out var entry))
        {
            Debug.LogWarning($"[EnemyBulletPool] 등록되지 않은 키: {key}");
            return null;
        }

        // 파괴된 오브젝트는 건너뜀
        while (queue.Count > 0)
        {
            var obj = queue.Dequeue();
            if (obj == null) { entry.currentTotal--; continue; }
            obj.SetActive(true);
            activeBullets[obj] = key;
            return obj;
        }

        // 풀이 비었을 때
        if (entry.currentTotal < entry.maxPoolSize)
        {
            // 최대치 미만이면 추가 생성
            var obj = CreateBullet(entry);
            obj.SetActive(true);
            activeBullets[obj] = key;
            return obj;
        }

        // 최대치 초과 → 공격 중지 신호
        return null;
    }

    // ── 반환 ────────────────────────────────────
    public void Return(string key, GameObject obj)
    {
        if (obj == null) return;

        // 중복 반환 방지: 대여 목록에 없고 이미 꺼져 있으면 이미 반환된 것
        if (!activeBullets.Remove(obj) && !obj.activeSelf) return;

        if (!pools.ContainsKey(key))
        {
            Debug.LogWarning($"[EnemyBulletPool] 등록되지 않은 키: {key}");
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(entryMap[key].parent);
        pools[key].Enqueue(obj);
    }

    /// <summary>활성 중인 투사체를 전부 풀로 반환한다. (레드대쉬 되감기 등)</summary>
    public void ReturnAll()
    {
        returnBuffer.Clear();
        foreach (var obj in activeBullets.Keys) returnBuffer.Add(obj);

        foreach (var obj in returnBuffer)
        {
            if (obj == null) { activeBullets.Remove(obj); continue; }
            if (activeBullets.TryGetValue(obj, out var key))
            {
                var rb = obj.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                Return(key, obj);
            }
        }
        returnBuffer.Clear();
    }

    // ── 내부 생성 ────────────────────────────────
    private GameObject CreateBullet(BulletEntry entry)
    {
        var obj = Instantiate(entry.prefab, entry.parent);
        obj.SetActive(false);
        entry.currentTotal++;
        return obj;
    }
    #endregion

    #region 26/04/15구버전
    /*
    static EnemyBulletPool instance;
    Queue<GameObject> bulletPool;
    public int PoolSize;
    NewEnemyCombat enemyCombat;
    public Transform bulletParent;
    void Awake()
    {
        enemyCombat = GetComponent<NewEnemyCombat>();
        if(instance == null)
        {
            instance = this;
        }
    }

    private void Start()
    {
        bulletPool = new Queue<GameObject>();
        if (bulletParent != null)
        {
            CreatePool();
        }
    }

    void CreatePool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            bulletPool.Enqueue(Instantiate(enemyCombat.bullet,bulletParent));
        }
    }

    public GameObject GetPool()
    {
        if (bulletPool == null) return null;

        var pool = bulletPool.Dequeue();
        return pool;
    }
    public void ReturnPool(GameObject pool)
    {
        if (bulletPool == null) return;

        bulletPool.Enqueue(pool);
    }
    */
    #endregion
}
