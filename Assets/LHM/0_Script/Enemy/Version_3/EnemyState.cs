using Unity.VisualScripting;
using UnityEngine;

public class EnemyState : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 10f;
    public float currentHp;

    // LockdownRoom이 구독하는 사망 이벤트
    public event System.Action<EnemyState> OnDeath;

    protected virtual void Awake()
    {
        currentHp = maxHp;
    }

    public virtual void Init()
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
        var BTcon = GetComponent<EnemyBTController>();
        BTcon.enabled = false; // BT 비활성화
        BTcon.requireInit = true;
        var col = GetComponent<Collider2D>().enabled = false; // 충돌체 비활성화
        var rb = GetComponent<Rigidbody2D>().simulated = false; // 물리 시뮬레이션 비활성화

        OnDeath?.Invoke(this);

        gameObject.SetActive(false);
    }
}
