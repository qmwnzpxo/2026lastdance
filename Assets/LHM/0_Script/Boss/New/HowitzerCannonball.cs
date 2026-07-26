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
    [Tooltip("착탄 폭발 시 재생할 사운드")]
    [SerializeField] private AudioClip explosionSound;


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
        // 탄두가 위를 향하도록 회전 (Z=-90° → 스프라이트 -X방향(탄두)이 위를 가리탔)
        transform.rotation = Quaternion.Euler(0f, 0f, -90f);

        while (Vector2.Distance(transform.position, targetHeight) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetHeight, riseSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetHeight;
        currentState = HowitzerState.Waiting;

        // 안전장치: Waiting 상태에서 5초 이상 대기하면 자동 풀 반환
        // (페이즈 전환/코루틴 중단 등으로 StartFalling이 호출되지 않는 경우 대비)
        float waitedTime = 0f;
        while (currentState == HowitzerState.Waiting)
        {
            waitedTime += Time.deltaTime;
            if (waitedTime > 5f)
            {
                Debug.LogWarning("[HowitzerCannonball] Waiting 타임아웃 - 풀로 자동 반환");
                ReturnToPool();
                yield break;
            }
            yield return null;
        }
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
        float distance = Vector2.Distance(startPos, endPos);
        float duration = distance / fallSpeed;
        float elapsedTime = 0f;

        if (ammoType == HowitzerAmmoType.AirBurst)
        {
            // AirBurst: 낙하 중 낙하 목표 Y + airBurstHeight 고도에서 폭발
            // (burstY = 지면에서 airBurstHeight만큼 위에서 폭발 발동)
            float burstY = fallTarget.y + airBurstHeight;

            while (transform.position.y > burstY && elapsedTime < duration)
            {
                Vector3 prevPos = transform.position;
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                UpdateFallingRotation(prevPos);  // 탄두가 warning marker 방향을 향하도록 회전
                yield return null;
            }

            // 공중 폭발
            currentState = HowitzerState.AirBursting;
            ExecuteAirBurst();
        }
        else if (ammoType == HowitzerAmmoType.BounceBlast)
        {
            // BounceBlast: 지면 충돌까지 낙하
            while (Vector2.Distance(transform.position, fallTarget) > 0.1f && elapsedTime < duration)
            {
                Vector3 prevPos = transform.position;
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                UpdateFallingRotation(prevPos);  // 탄두가 warning marker 방향을 향하도록 회전
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
            while (Vector2.Distance(transform.position, fallTarget) > 0.1f && elapsedTime < duration)
            {
                Vector3 prevPos = transform.position;
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // 포물선 궤적 계산
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                // 포물선 높이 보정
                float arcHeight = Mathf.Sin(t * Mathf.PI) * (startPos.y - endPos.y) * 0.5f;
                currentPos.y += arcHeight;

                transform.position = currentPos;
                UpdateFallingRotation(prevPos);  // 탄두가 warning marker 방향을 향하도록 회전
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
            explosion.SetActive(true);
            Destroy(explosion, 1f);
        }

        // 폭발 사운드 재생
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }


        // 범위 내 플레이어 데미지 적용
        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(transform.position, explosionRange);
        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<IDamageable>()?.TakeDamage(damage);
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
            explosion.SetActive(true);
            Destroy(explosion, 1f);
        }

        // 폭발 사운드 재생
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
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

                    // velocity 먼저 설정 (초기화보다 앞에 해야 velocity가 덮이지 않음)
                    pelletRb.gravityScale = 0f;
                    pelletRb.linearVelocity = direction * airBurstPelletSpeed;

                    // 파편에 데미지 정보 전달 — InitializeAirBurstPellet 사용(velocity 바체 없음)
                    BossProjectile projectile = pellet.GetComponent<BossProjectile>();
                    if (projectile != null)
                    {
                        projectile.InitializeAirBurstPellet(airBurstDamage, bossController);
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
            explosion.SetActive(true);
            Destroy(explosion, 1f);
        }

        // 폭발 사운드 재생
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
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
    /// 생성 시 1회만 플레이어 방향으로 좌우 방향 전환 (이후 고정)
    /// localScale.x의 부호를 flip해 스프라이트 방향 결정
    /// </summary>
    public void SetFacingToPlayer(Transform player)
    {
        // 곡사포탄은 Atan2 물리 회전 각도로 비행 방향을 표현하므로, 
        // scale.x를 뒤집을 경우 회전 행렬이 꼬여 탄두가 반대로 날아가는 버그가 발생합니다.
        // 따라서 항상 절대값(양수) 상태를 유지하여 정방향 회전이 적용되도록 처리합니다.
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
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

    /// <summary>
    /// 풀 재사용 전 상태 완전 초기화 (풀에서 꺼낼 때 호출)
    /// </summary>
    public void ResetForPool()
    {
        currentState = HowitzerState.Idle;
        ammoType = HowitzerAmmoType.Normal;
        targetHeight = Vector3.zero;
        fallTarget = Vector3.zero;
        riseSpeed = 0f;
        fallSpeed = 0f;
        damage = 0f;

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 회전 초기화
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// 낙하 중 매 프레임 이동 방향으로 탄두 회전 갱신 (warning marker를 향하도록)
    /// 탄두가 스프라이트 -X 방향에 있으므로 Atan2 + 180° 보정.
    /// </summary>
    private void UpdateFallingRotation(Vector3 prevPos)
    {
        Vector3 moveDelta = transform.position - prevPos;
        if (moveDelta.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(moveDelta.y, moveDelta.x) * Mathf.Rad2Deg + 180f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void OnDisable()
    {
        // 상태 초기화 (풀 반환 시)
        currentState = HowitzerState.Idle;

        // 코루틴 중지
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 회전 초기화
        transform.rotation = Quaternion.identity;
    }
}
