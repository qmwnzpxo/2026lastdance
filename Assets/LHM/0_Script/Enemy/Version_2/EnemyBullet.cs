using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour, IDamageable
{
    // ── 설정 ────────────────────────────────────
    [Header("투사체 기본")]
    public string poolKey;
    public float speed = 10f;
    public float maxTravelDistance = 15f;

    [Header("레이어")]
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask groundLayer;

    // ── 런타임 ──────────────────────────────────
    protected float damage;
    protected Vector2 direction;
    protected float traveledDistance;
    protected Rigidbody2D rb;
    protected CustomTimeManager customTimeManager;

    // ── 초기화 ──────────────────────────────────
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        customTimeManager = CustomTimeManager.Instance;
        rb.gravityScale = 0f; // 기본: 중력 없음
    }

    /// <summary>
    /// 풀에서 꺼낼 때 호출. 방향/데미지 주입.
    /// </summary>
    public virtual void Init(Vector2 fireDirection, float dmg)
    {
        Debug.Log($"x{fireDirection.x}  y.{fireDirection.y}");
        direction = fireDirection.normalized;
        damage = dmg;
        traveledDistance = 0f;

        rb.linearVelocity = direction * speed;
    }

    // ── 이동 / 거리 체크 ─────────────────────────
    protected virtual void FixedUpdate()
    {
        traveledDistance += speed * Time.fixedDeltaTime;

        if (traveledDistance >= maxTravelDistance)
            ReturnToPool();
    }

    // ── 충돌 ────────────────────────────────────
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Trigger 진입: {other.gameObject.name}, layer={other.gameObject.layer}");

        // 플레이어 레이어
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            OnHitPlayer(other);            
            return;
        }

        // 지형 레이어
        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            OnHitGround(other);
        }
    }

    /// <summary>플레이어 적중 시 처리. 서브클래스에서 override 가능.</summary>
    protected virtual void OnHitPlayer(Collider2D other)
    {
        other.gameObject.GetComponent<IDamageable>()?.TakeDamage(damage);
        HitstopManager.Instance.TriggerLight();
        ReturnToPool();
    }

    /// <summary>지형 적중 시 처리. 서브클래스에서 override 가능.</summary>
    protected virtual void OnHitGround(Collider2D other)
    {
        // 기본: 그냥 반환. 서브클래스에서 파열 등 추가 가능.
        ReturnToPool();
    }

    // ── 풀 반환 ──────────────────────────────────
    protected virtual void ReturnToPool()
    {
        rb.linearVelocity = Vector2.zero;
        traveledDistance = 0f;
        EnemyBulletPool.Instance.Return(poolKey, gameObject);
    }
    public virtual void TakeDamage(float damage)
    {
        Debug.Log($"enemy bullet take damage");
        ReturnToPool();
    }
}
