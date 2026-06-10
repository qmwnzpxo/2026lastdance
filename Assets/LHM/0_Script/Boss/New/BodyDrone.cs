using UnityEngine;
using System.Collections;

/// <summary>
/// 몸체 드론 - 지렁이 드론의 몸통 부분
/// 지휘자 드론을 따라가며 일정 거리를 유지
/// 지휘자 드론 파괴 시 흩어지며 랜덤 이동 후 총알 발사
/// </summary>
public class BodyDrone : MonoBehaviour
{
    [Header("드론 스탯")]
    [SerializeField] private float droneHP = 30f;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float scatterSpeed = 3f;
    [SerializeField] private float followDistance = 1.5f;

    [Header("공격 설정")]
    [SerializeField] private float bulletDamage = 5f;
    [SerializeField] private float minFireInterval = 1f;
    [SerializeField] private float maxFireInterval = 3f;
    [SerializeField] private float scatterRange = 10f;

    [Header("참조 컴포넌트")]
    [SerializeField] private Transform player;
    [SerializeField] private BossController bossController;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private Collider2D droneCollider;

    private float currentHP;
    private bool isDead = false;
    private bool isScattered = false;
    private ConductorDrone conductorDrone;
    private int bodyIndex;
    private Coroutine fireCoroutine;
    private Coroutine scatterCoroutine;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        droneCollider = GetComponent<Collider2D>();
        currentHP = droneHP;
    }

    /// <summary>
    /// 드론 초기화
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller, ConductorDrone conductor, int index)
    {
        player = playerTransform;
        bossController = controller;
        conductorDrone = conductor;
        bodyIndex = index;
        currentHP = droneHP;
        isDead = false;
        isScattered = false;

        // 콜라이더 활성화
        if (droneCollider != null)
        {
            droneCollider.enabled = true;
        }

        // 지휘자 드론 따라가기 시작
        StartCoroutine(FollowConductor());
    }

    /// <summary>
    /// 지휘자 드론 따라가기
    /// </summary>
    private IEnumerator FollowConductor()
    {
        while (!isDead && !isScattered && conductorDrone != null)
        {
            // 지휘자 드론의 현재 위치
            Vector3 conductorPos = conductorDrone.GetCurrentPosition();

            // 몸체 인덱스에 따른 목표 위치 계산
            // 각 몸체 드론은 일정 거리를 두고 지휘자 드론을 따라감
            Vector3 targetPosition = CalculateTargetPosition(conductorPos);

            // 부드럽게 이동
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, followSpeed * Time.deltaTime);

            yield return null;
        }
    }

    /// <summary>
    /// 목표 위치 계산
    /// </summary>
    private Vector3 CalculateTargetPosition(Vector3 conductorPos)
    {
        // 지휘자 드론의 이동 방향을 역추적하여 몸체 위치 계산
        // 간단하게 지휘자 드론 뒤쪽에 배치
        Vector3 direction = (conductorPos - transform.position).normalized;
        
        if (direction == Vector3.zero)
        {
            direction = Vector3.left;
        }

        // 인덱스에 따른 거리 계산
        float distanceOffset = bodyIndex * followDistance;
        Vector3 targetPos = conductorPos - direction * distanceOffset;

        return targetPos;
    }

    /// <summary>
    /// 지휘자 드론 사망 시 호출
    /// </summary>
    public void OnConductorDeath()
    {
        if (isScattered || isDead)
            return;

        isScattered = true;
        conductorDrone = null;

        // 따라가기 코루틴 중지
        StopAllCoroutines();

        // 흩어지기 동작 시작
        scatterCoroutine = StartCoroutine(ScatterBehavior());
    }

    /// <summary>
    /// 흩어지기 동작
    /// </summary>
    private IEnumerator ScatterBehavior()
    {
        // 플레이어 주변 랜덤 위치로 이동
        if (player != null)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-scatterRange, scatterRange),
                Random.Range(-scatterRange, scatterRange),
                0f
            );
            Vector3 scatterTarget = player.position + randomOffset;

            // 랜덤 위치로 이동
            while (Vector3.Distance(transform.position, scatterTarget) > 0.5f && !isDead)
            {
                Vector3 direction = (scatterTarget - transform.position).normalized;
                if (rb2d != null)
                {
                    rb2d.linearVelocity = direction * scatterSpeed;
                }
                yield return null;
            }

            // 속도 정지
            if (rb2d != null)
            {
                rb2d.linearVelocity = Vector2.zero;
            }
        }

        // 총알 발사 후 이동 반복
        fireCoroutine = StartCoroutine(ScatterFireAndMove());
    }

    /// <summary>
    /// 흩어진 상태에서 총알 발사 후 이동
    /// </summary>
    private IEnumerator ScatterFireAndMove()
    {
        while (!isDead)
        {
            // 총알 발사
            FireBullet();

            // 랜덤 대기 시간 (1~3초)
            float waitTime = Random.Range(minFireInterval, maxFireInterval);
            yield return new WaitForSeconds(waitTime);

            // 새로운 랜덤 위치로 이동
            if (player != null)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-scatterRange, scatterRange),
                    Random.Range(-scatterRange, scatterRange),
                    0f
                );
                Vector3 newTarget = player.position + randomOffset;

                while (Vector3.Distance(transform.position, newTarget) > 0.5f && !isDead)
                {
                    Vector3 direction = (newTarget - transform.position).normalized;
                    if (rb2d != null)
                    {
                        rb2d.linearVelocity = direction * scatterSpeed;
                    }
                    yield return null;
                }

                // 속도 정지
                if (rb2d != null)
                {
                    rb2d.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    /// <summary>
    /// 총알 발사
    /// </summary>
    private void FireBullet()
    {
        if (bossController == null || player == null)
            return;

        // 총알 오브젝트 풀에서 가져오기
        GameObject bullet = bossController.GetBulletFromPool();
        if (bullet == null)
            return;

        // 발사 위치 설정
        bullet.transform.position = transform.position;

        // 플레이어 방향 계산
        Vector3 direction = (player.position - transform.position).normalized;

        // 총알 스크립트 설정
        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(direction, bulletDamage, bossController);
        }
    }

    /// <summary>
    /// 피격 처리
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHP -= damage;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 사망 처리
    /// </summary>
    private void Die()
    {
        isDead = true;

        // 코루틴 중지
        StopAllCoroutines();

        // 보스 컨트롤러에서 드론 제거
        if (bossController != null)
        {
            bossController.UnregisterDrone(gameObject);
        }

        // 사망 이펙트 (필요시 추가)
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        StopAllCoroutines();
    }
}
