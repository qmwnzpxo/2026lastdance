using System.Collections.Generic;
using UnityEngine;

public class EnemyBulletPool : MonoBehaviour
{
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
}
