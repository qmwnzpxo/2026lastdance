using UnityEngine;
using System.Collections;

public class EnemyState : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 10f;
    public float currentHp;

    public float deathDelay = 2.0f; // 2초 뒤 오브젝트 삭제되도록 변수 추가

    protected EnemyBTController BTController;
    protected EnemyCombat Combat;

    // LockdownRoom이 구독하는 사망 이벤트
    public event System.Action<EnemyState> OnDeath;

    protected virtual void Awake()
    {
        currentHp = maxHp;
        BTController = GetComponent<EnemyBTController>();
        Combat = GetComponent<EnemyCombat>();
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
        /*BTController.enabled = false; // BT 비활성화
        BTController.requireInit = true;
        var col = GetComponent<Collider2D>().enabled = false; // 충돌체 비활성화
        var rb = GetComponent<Rigidbody2D>().simulated = false; // 물리 시뮬레이션 비활성화

        OnDeath?.Invoke(this);

        gameObject.SetActive(false);*/

        if (BTController != null)
        {
            BTController.enabled = false;
            BTController.requireInit = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        // 2. 이벤트를 발생시켜 방(Room) 등 시스템에 사망 사실을 즉시 알립니다.
        OnDeath?.Invoke(this);

        // 3. 바로 꺼버리지 않고 2초(deathDelay)를 기다리는 코루틴을 실행합니다.
        StartCoroutine(DeactivateAfterDelay());
    }

    protected virtual IEnumerator DeactivateAfterDelay()
    {
        yield return new WaitForSeconds(deathDelay); // 2초 대기
        gameObject.SetActive(false);                 // 오브젝트 비활성화 (삭제)
    }
}
