using System.Collections.Generic;
using UnityEngine;

public class EnemyBulletPool : MonoBehaviour
{
    Queue<GameObject> bulletPool;
    public int PoolSize;
    NewEnemyCombat enemyCombat;
    void Awake()
    {
        enemyCombat = GetComponent<NewEnemyCombat>();
    }

    private void Start()
    {
        bulletPool = new Queue<GameObject>();
        CreatePool();
    }

    void CreatePool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            bulletPool.Enqueue(Instantiate(enemyCombat.bullet));
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
