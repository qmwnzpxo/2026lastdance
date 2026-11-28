using UnityEngine;

[RequireComponent(typeof(Rigidbody2D),typeof(TimeScaleReceiver))]
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
    protected TimeScaleReceiver timeScaleReceiver;

    // ── 초기화 ──────────────────────────────────
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        timeScaleReceiver = GetComponent<TimeScaleReceiver>();
        rb.gravityScale = 0f; // 기본: 중력 없음
    }
    protected void Start()
    {
        customTimeManager = CustomTimeManager.Instance;
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

        // 풀에서 처음 꺼낸 탄은 Start(customTimeManager 할당)보다 Init이 먼저 불릴 수 있어 Instance를 직접 사용
        var tmg = CustomTimeManager.Instance;
        rb.linearVelocity = direction * speed * (tmg != null ? tmg.EnemyTimeScale : 1f);
    }

    // ── 이동 / 거리 체크 ─────────────────────────
    protected virtual void FixedUpdate()
    {
        // 적 배율에 맞춰 매 물리 프레임 속도 갱신 (발사 때 한 번만 정하면 슬로우 중에도 원래 속도로 날아가고,
        // 사거리는 배율 시간으로 계산돼 슬로우 중 훨씬 멀리 날아감)
        rb.linearVelocity = direction * speed * customTimeManager.EnemyTimeScale;
        traveledDistance += speed * customTimeManager.EnemyFixedDeltaTime;

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
