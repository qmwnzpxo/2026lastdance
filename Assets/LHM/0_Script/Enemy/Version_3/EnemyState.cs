using Unity.VisualScripting;
using UnityEngine;

public class EnemyState : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 10f;
    public float currentHp;

    public virtual void TakeDamage(float damage)
    {
        currentHp -= damage;
        if (currentHp <= 0)
        {
            // TODO: 사망 처리 꼭 할것!
        }
    }
}
