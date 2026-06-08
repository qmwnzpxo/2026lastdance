using UnityEngine;

/// <summary>
/// 플레이어 체력 관리 스크립트
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("플레이어 체력 설정")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP;

    [Header("무적 시간")]
    [SerializeField] private float invincibilityTime = 0.5f;
    private float invincibilityTimer = 0f;

    private bool isDead = false;

    private void Awake()
    {
        currentHP = maxHP;
    }

    /// <summary>
    /// 데미지 받기
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        // 무적 시간 체크
        if (invincibilityTimer > 0f)
            return;

        currentHP -= damage;

        // 무적 시간 시작
        invincibilityTimer = invincibilityTime;

        // 사망 체크
        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Die();
        }

        Debug.Log($"Player took {damage} damage. Current HP: {currentHP}/{maxHP}");
    }

    private void Update()
    {
        // 무적 시간 감소
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 사망 처리
    /// </summary>
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("Player died!");

        // 사망 처리 로직 (게임 오버 등)
        // 여기에 추가 구현 필요
    }

    /// <summary>
    /// 체력 회복
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead)
            return;

        currentHP = Mathf.Min(currentHP + amount, maxHP);
        Debug.Log($"Player healed {amount}. Current HP: {currentHP}/{maxHP}");
    }

    /// <summary>
    /// 현재 체력 getter
    /// </summary>
    public float GetCurrentHP()
    {
        return currentHP;
    }

    /// <summary>
    /// 최대 체력 getter
    /// </summary>
    public float GetMaxHP()
    {
        return maxHP;
    }

    /// <summary>
    /// 사망 상태 getter
    /// </summary>
    public bool IsDead()
    {
        return isDead;
    }
}
