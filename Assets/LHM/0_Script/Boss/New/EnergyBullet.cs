using UnityEngine;

/// <summary>
/// 에너지탄 - 드론이 발사하는 투사체
/// 플레이어 방향으로 직선 이동하며 시간이 지날수록 크기가 점점 커짐
/// rb2d 유무와 관계없이 항상 이동 (transform 폴백 지원)
/// </summary>
public class EnergyBullet : MonoBehaviour
{
    [Header("투사체 설정")]
    [SerializeField] private float bulletSpeed = 8f;
    [SerializeField] private float lifetime = 10f;

    [Header("스케일 성장 설정")]
    [SerializeField] private float initialSize = 0.3f;
    [SerializeField] private float maxSize = 2.5f;
    [SerializeField] private float growthRate = 0.4f;

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
    /// 에너지탄 초기화 - 발사 방향과 데미지 설정
    /// </summary>
    public void Initialize(Vector3 direction, float damage)
    {
        moveDirection = direction.normalized;
        currentDamage = damage;
        baseDamage = damage;
        currentSize = initialSize;
        currentLifetime = lifetime;
        isInitialized = true;

        // 초기 스케일 설정
        transform.localScale = Vector3.one * currentSize;

        // rb2d가 있으면 velocity로 이동
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

        // rb2d가 없거나 Kinematic인 경우 transform으로 직접 이동 (폴백)
        if (rb2d == null || rb2d.isKinematic)
        {
            transform.position += moveDirection * bulletSpeed * Time.deltaTime;
        }

        // 스케일 성장
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
        if (!isInitialized)
            return;

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
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 속도 및 크기 초기화
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.gravityScale = 0f;
        }
        transform.localScale = Vector3.one * initialSize;
        moveDirection = Vector3.zero;
        isInitialized = false;
    }
}
