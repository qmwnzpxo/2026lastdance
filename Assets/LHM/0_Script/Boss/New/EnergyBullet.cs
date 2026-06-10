using UnityEngine;

/// <summary>
/// 에너지탄 - 지휘자 드론이 발사하는 특수 투사체
/// 매우 느린 속도로 이동하며 시간이 지날수록 크기가 점점 커짐
/// </summary>
public class EnergyBullet : MonoBehaviour
{
    [Header("투사체 설정")]
    [SerializeField] private float bulletSpeed = 2f;
    [SerializeField] private float initialSize = 0.5f;
    [SerializeField] private float maxSize = 3f;
    [SerializeField] private float growthRate = 0.5f;
    [SerializeField] private float lifetime = 10f;

    [Header("데미지 설정")]
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float damageMultiplier = 1.5f;

    private Rigidbody2D rb2d;
    private Vector3 moveDirection;
    private float currentSize;
    private float currentLifetime;
    private float currentDamage;
    private bool isInitialized = false;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        currentLifetime = lifetime;
        currentSize = initialSize;
        currentDamage = baseDamage;
        isInitialized = false;
    }

    /// <summary>
    /// 에너지탄 초기화
    /// </summary>
    public void Initialize(Vector3 direction, float damage)
    {
        moveDirection = direction.normalized;
        currentDamage = damage;
        baseDamage = damage;
        currentSize = initialSize;
        isInitialized = true;

        transform.localScale = Vector3.one * currentSize;

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.linearVelocity = moveDirection * bulletSpeed;
        }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        // 수명 감소
        currentLifetime -= Time.deltaTime;
        if (currentLifetime <= 0f)
        {
            DestroyBullet();
            return;
        }

        // 크기 증가
        if (currentSize < maxSize)
        {
            currentSize += growthRate * Time.deltaTime;
            currentSize = Mathf.Min(currentSize, maxSize);
            transform.localScale = Vector3.one * currentSize;

            // 크기에 비례하여 데미지 증가
            currentDamage = baseDamage + (currentSize - initialSize) * damageMultiplier;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 충돌 시
        if (other.CompareTag("Player"))
        {
            // 현재 크기에 따른 데미지 적용
            other.GetComponent<PlayerHealth>()?.TakeDamage(currentDamage);
            DestroyBullet();
        }
    }

    /// <summary>
    /// 에너지탄 파괴
    /// </summary>
    private void DestroyBullet()
    {
        // 파괴 이펙트 (필요시 추가)
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 속도 및 크기 초기화
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }
        transform.localScale = Vector3.one * initialSize;
        isInitialized = false;
    }
}
