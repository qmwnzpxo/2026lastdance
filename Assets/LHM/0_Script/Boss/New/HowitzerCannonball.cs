using UnityEngine;
using System.Collections;

/// <summary>
/// 곡사포 포탄 상태 열거형
/// </summary>
public enum HowitzerState
{
    Idle,        // 대기
    Rising,      // 수직 상승 중
    Waiting,     // 낙하 신호 대기 중
    Falling,     // 낙하 중
    AirBursting, // 공중 폭발 중 (AirBurst 전용)
    Exploded     // 폭발 완료
}

/// <summary>
/// 곡사포 포탄 전용 상태 머신
/// 5단계 곡사포 사양을 위한 포탄 제어
/// </summary>
public class HowitzerCannonball : MonoBehaviour
{
    [Header("폭발 설정")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private float explosionRange = 3f;

    private HowitzerState currentState = HowitzerState.Idle;
    private Rigidbody2D rb2d;
    private BossController bossController;

    private Vector3 targetHeight;
    private float riseSpeed;
    private Vector3 fallTarget;
    private float fallSpeed;
    private float damage;
    private Coroutine currentCoroutine;

    // 탄종 관련
    private HowitzerAmmoType ammoType = HowitzerAmmoType.Normal;

    // AirBurst 파라미터
    private float airBurstHeight;
    private int airBurstPelletCount;
    private float airBurstPelletSpeed;
    private float airBurstPelletRange;
    private float airBurstDamage;
    private GameObject airBurstPelletPrefab;

    // BounceBlast 파라미터
    private int bounceCount;
    private float bounceMinAngle;
    private float bounceMaxAngle;
    private float bounceSpeed;
    private float bounceBlastRange;
    private float bounceBlastDamage;
    private GameObject bouncePelletPrefab;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 수직 상승 시작
    /// </summary>
    public void StartRising(Vector3 target, float speed)
    {
        targetHeight = target;
        riseSpeed = speed;
        currentState = HowitzerState.Rising;
        
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(RisingCoroutine());
    }

    /// <summary>
    /// 수직 상승 코루틴
    /// </summary>
    private IEnumerator RisingCoroutine()
    {
        while (Vector3.Distance(transform.position, targetHeight) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetHeight, riseSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetHeight;
        currentState = HowitzerState.Waiting;
    }

    /// <summary>
    /// 낙하 시작
    /// </summary>
    public void StartFalling(Vector3 target, float speed, float dmg, float range)
    {
        fallTarget = target;
        fallSpeed = speed;
        damage = dmg;
        explosionRange = range;
        currentState = HowitzerState.Falling;
        
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(FallingCoroutine());
    }

    /// <summary>
    /// 낙하 코루틴 - 포물선 궤적
    /// </summary>
    private IEnumerator FallingCoroutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = fallTarget;
        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / fallSpeed;
        float elapsedTime = 0f;

        if (ammoType == HowitzerAmmoType.AirBurst)
        {
            // AirBurst: 낙하 중 airBurstHeight 고도에서 폭발
            float startY = transform.position.y;
            float targetY = fallTarget.y;
            float burstY = startY - airBurstHeight;

            while (transform.position.y > burstY && elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                yield return null;
            }

            // 공중 폭발
            currentState = HowitzerState.AirBursting;
            ExecuteAirBurst();
        }
        else if (ammoType == HowitzerAmmoType.BounceBlast)
        {
            // BounceBlast: 지면 충돌까지 낙하
            while (Vector3.Distance(transform.position, fallTarget) > 0.1f && elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                yield return null;
            }

            transform.position = fallTarget;
            currentState = HowitzerState.Exploded;

            // 지면 충돌 후 바운스 파편 생성
            ExecuteBounceBlast();
        }
        else
        {
            // Normal: 기존 로직
            while (Vector3.Distance(transform.position, fallTarget) > 0.1f && elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                yield return null;
            }

            transform.position = fallTarget;
            currentState = HowitzerState.Exploded;

            // 폭발 실행
            Explode();
        }
    }

    /// <summary>
    /// 폭발
    /// </summary>
    private void Explode()
    {
        // 폭발 이펙트
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(explosion, 1f);
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

        // 오브젝트 풀 반환
        ReturnToPool();
    }

    /// <summary>
    /// 오브젝트 풀에 반환
    /// </summary>
    private void ReturnToPool()
    {
        // BossController 참조가 없으면 그냥 비활성화
        if (bossController == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bossController.ReturnCannonballToPool(gameObject);
    }

    /// <summary>
    /// AirBurst 폭발 실행 - 공중에서 파편 다방향 산탄
    /// </summary>
    private void ExecuteAirBurst()
    {
        // 폭발 이펙트
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(explosion, 1f);
        }

        // 파편 생성 및 발사
        if (airBurstPelletPrefab != null)
        {
            float angleStep = 360f / airBurstPelletCount;
            for (int i = 0; i < airBurstPelletCount; i++)
            {
                GameObject pellet = Instantiate(airBurstPelletPrefab, transform.position, Quaternion.identity);
                Rigidbody2D pelletRb = pellet.GetComponent<Rigidbody2D>();
                if (pelletRb != null)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    pelletRb.linearVelocity = direction * airBurstPelletSpeed;

                    // 파편에 데미지 정보 전달 (BossProjectile 컴포넌트가 있다면)
                    BossProjectile projectile = pellet.GetComponent<BossProjectile>();
                    if (projectile != null)
                    {
                        projectile.Initialize(direction, airBurstDamage, bossController);
                    }

                    // 파편 사거리 제한 (일정 시간 후 파괴)
                    StartCoroutine(DestroyPelletAfterDelay(pellet, airBurstPelletRange / airBurstPelletSpeed));
                }
            }
        }

        // 오브젝트 풀 반환
        ReturnToPool();
    }

    /// <summary>
    /// BounceBlast 폭발 실행 - 지면 충돌 후 바운스 파편 생성
    /// </summary>
    private void ExecuteBounceBlast()
    {
        // 폭발 이펙트
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(explosion, 1f);
        }

        // 바운스 파편 생성
        if (bouncePelletPrefab != null)
        {
            for (int i = 0; i < bounceCount; i++)
            {
                GameObject pellet = Instantiate(bouncePelletPrefab, transform.position, Quaternion.identity);
                Rigidbody2D pelletRb = pellet.GetComponent<Rigidbody2D>();
                if (pelletRb != null)
                {
                    // 랜덤 각도로 위로 솟구침
                    float angle = Random.Range(bounceMinAngle, bounceMaxAngle) * Mathf.Deg2Rad;
                    // 왼쪽 또는 오른쪽 랜덤 방향
                    float directionX = Random.Range(0, 2) == 0 ? -1f : 1f;
                    Vector2 direction = new Vector2(directionX * Mathf.Cos(angle), Mathf.Sin(angle));
                    pelletRb.linearVelocity = direction * bounceSpeed;

                    // 중력 영향 받도록 설정
                    pelletRb.gravityScale = 1f;

                    // 파편에 2차 폭발 정보 전달 (BossProjectile 컴포넌트가 있다면)
                    BossProjectile projectile = pellet.GetComponent<BossProjectile>();
                    if (projectile != null)
                    {
                        projectile.InitializeForBounceBlast(bounceBlastDamage, bounceBlastRange, bossController);
                    }
                }
            }
        }

        // 오브젝트 풀 반환
        ReturnToPool();
    }

    /// <summary>
    /// 파편 지연 파괴
    /// </summary>
    private IEnumerator DestroyPelletAfterDelay(GameObject pellet, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pellet != null)
        {
            Destroy(pellet);
        }
    }

    /// <summary>
    /// BossController 참조 설정
    /// </summary>
    public void SetBossController(BossController controller)
    {
        bossController = controller;
    }

    /// <summary>
    /// 탄종 설정
    /// </summary>
    public void SetAmmoType(HowitzerAmmoType type)
    {
        ammoType = type;
    }

    /// <summary>
    /// AirBurst 파라미터 설정
    /// </summary>
    public void SetAirBurstParameters(float height, int pelletCount, float pelletSpeed, float pelletRange, float pelletDamage, GameObject pelletPrefab)
    {
        airBurstHeight = height;
        airBurstPelletCount = pelletCount;
        airBurstPelletSpeed = pelletSpeed;
        airBurstPelletRange = pelletRange;
        airBurstDamage = pelletDamage;
        airBurstPelletPrefab = pelletPrefab;
    }

    /// <summary>
    /// BounceBlast 파라미터 설정
    /// </summary>
    public void SetBounceBlastParameters(int count, float minAngle, float maxAngle, float speed, float blastRange, float blastDamage, GameObject pelletPrefab)
    {
        bounceCount = count;
        bounceMinAngle = minAngle;
        bounceMaxAngle = maxAngle;
        bounceSpeed = speed;
        bounceBlastRange = blastRange;
        bounceBlastDamage = blastDamage;
        bouncePelletPrefab = pelletPrefab;
    }

    private void OnDisable()
    {
        // 상태 초기화
        currentState = HowitzerState.Idle;
        
        // 코루틴 중지
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }
    }
}
