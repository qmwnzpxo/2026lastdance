using Unity.VisualScripting;
using UnityEngine;

public class EnemyState : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 10f;
    public float currentHp;


    public void Awake()
    {
        currentHp = maxHp;
    }
    public virtual void TakeDamage(float damage)
    {
        currentHp -= damage;
        if (currentHp <= 0)
        {
            Die();
        }
    }

    public virtual void Die()
    {
        GetComponent<EnemyBTController>().enabled = false; // BT 비활성화
        GetComponent<Collider2D>().enabled = false; // 충돌체 비활성화
        GetComponent<Rigidbody2D>().simulated = false; // 물리 시뮬레이션 비활성화
        gameObject.SetActive(false);
    }
}
