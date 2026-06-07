using UnityEngine;

/// <summary>
/// 보스 투사체 스크립트 - 총알 및 대포탄 오브젝트 공통 스크립트
/// 오브젝트 풀 반환 기능 포함
/// </summary>
public class BossProjectile : MonoBehaviour
{
    [Header("투사체 설정")]
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float cannonballSpeed = 10f;
    [SerializeField] private float lifetime = 5f;

    [Header("충격파 설정")]
    [SerializeField] private GameObject shockwaveEffect;
    [SerializeField] private float shockwaveRange = 3f;

    private Rigidbody2D rb2d;
    private BossController bossController;
    private float damage;
    private float explosionRange;
    private bool isCannonball = false;
    private bool isBounceBlastPellet = false;
    private float currentLifetime;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        currentLifetime = lifetime;
    }

    private void Update()
    {
        // 수명 감소
        currentLifetime -= Time.deltaTime;
        if (currentLifetime <= 0f)
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// 일반 총알 초기화
    /// </summary>
    public void Initialize(Vector3 direction, float projectileDamage, BossController controller)
    {
        isCannonball = false;
        damage = projectileDamage;
        bossController = controller;

        if (rb2d != null)
        {
            // 총알은 중력 영향을 받지 않음
            rb2d.gravityScale = 0f;
            rb2d.linearVelocity = direction * bulletSpeed;
        }

        currentLifetime = lifetime;
    }

    /// <summary>
    /// 대포탄 초기화 (포물선 탄도)
    /// </summary>
    public void InitializeCannonball(Vector3 direction, float projectileDamage, float range, BossController controller)
    {
        isCannonball = true;
        damage = projectileDamage;
        explosionRange = range;
        bossController = controller;

        if (rb2d != null)
        {
            // 중력 적용
            rb2d.gravityScale = 1f;
            
            // 포물선 탄도를 위한 초기 속도 계산
            Vector2 velocity = CalculateParabolicVelocity(direction, cannonballSpeed);
            rb2d.linearVelocity = velocity;
        }

        currentLifetime = lifetime;
        Debug.Log($"[BossProjectile] 대포탄 발사: 방향={direction}, 속도={cannonballSpeed}");
    }

    /// <summary>
    /// 포물선 탄도 속도 계산
    /// </summary>
    private Vector2 CalculateParabolicVelocity(Vector3 direction, float speed)
    {
        // 수평 속도: 방향의 x 성분
        float horizontalSpeed = direction.x * speed;

        // 수직 속도: 위로 발사하여 포물선 형성
        // 중력에 의해 자연스럽게 떨어지도록 충분한 상향 속도 부여
        float verticalSpeed = speed * 0.8f;

        return new Vector2(horizontalSpeed, verticalSpeed);
    }

    /// <summary>
    /// BounceBlast 파편 초기화 (2차 폭발용)
    /// </summary>
    public void InitializeForBounceBlast(float projectileDamage, float range, BossController controller)
    {
        isBounceBlastPellet = true;
        damage = projectileDamage;
        explosionRange = range;
        bossController = controller;

        // 중력은 이미 HowitzerCannonball에서 설정됨
        // 수명은 기본값 사용

        currentLifetime = lifetime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 충돌 시
        if (other.CompareTag("Player"))
        {
            if (isCannonball)
            {
                // 대포탄은 충격파 발생 후 사라짐
                Explode();
            }
            else if (isBounceBlastPellet)
            {
                // BounceBlast 파편은 2차 폭발 후 사라짐
                Explode();
            }
            else
            {
                // 총알은 직접 데미지 적용
                other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
                ReturnToPool();
            }
        }
        // 지면 충돌 시 (대포탄 또는 BounceBlast 파편)
        else if (other.CompareTag("Ground") && (isCannonball || isBounceBlastPellet))
        {
            Explode();
        }
    }

    /// <summary>
    /// 대포탄 폭발
    /// </summary>
    private void Explode()
    {
        // 충격파 이펙트
        if (shockwaveEffect != null)
        {
            GameObject shockwave = Instantiate(shockwaveEffect, transform.position, Quaternion.identity);
            Destroy(shockwave, 1f);
        }

        // 범위 내 플레이어 데미지 적용
        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(transform.position, explosionRange);
        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            }
        }

        ReturnToPool();
    }

    /// <summary>
    /// 오브젝트 풀에 반환
    /// </summary>
    private void ReturnToPool()
    {
        if (bossController != null)
        {
            if (isCannonball)
            {
                bossController.ReturnCannonballToPool(gameObject);
            }
            else
            {
                bossController.ReturnBulletToPool(gameObject);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 속도 및 중력 초기화
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.gravityScale = 0f;
        }
    }
}
