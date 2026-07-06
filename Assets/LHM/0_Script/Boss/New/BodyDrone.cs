using UnityEngine;
using System.Collections;

/// <summary>
/// 몸체 드론 - 지렁이 드론의 몸통 부분
/// 지휘자 드론을 따라가며 일정 거리를 유지
/// 지휘자 드론 파괴 시 흩어지며 랜덤 이동 후 총알 발사
/// </summary>
public class BodyDrone : MonoBehaviour, IDamageable
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
    private int totalBodyCount;
    private Coroutine fireCoroutine;
    private Coroutine scatterCoroutine;
    private Coroutine waveFireCoroutine;
    private bool isInitialized = false;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        droneCollider = GetComponent<Collider2D>();
        currentHP = droneHP;

        // 자동으로 Player 및 BossController 연결
        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }
        if (bossController == null)
        {
            bossController = Object.FindAnyObjectByType<BossController>();
        }
    }

    /// <summary>
    /// 드론 초기화 (기본, totalBodyCount 미지정)
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller, ConductorDrone conductor, int index)
    {
        Initialize(playerTransform, controller, conductor, index, 0);
    }

    /// <summary>
    /// 드론 초기화 (파도 발사용 totalBodyCount 포함)
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller, ConductorDrone conductor, int index, int totalCount)
    {
        if (isInitialized) return;
        isInitialized = true;

        if (playerTransform != null) player = playerTransform;
        if (controller != null) bossController = controller;
        conductorDrone = conductor;
        bodyIndex = index;
        totalBodyCount = totalCount;
        currentHP = droneHP;
        isDead = false;
        isScattered = false;

        // 시작 시 머리 드론(Conductor)의 위치(보스 좌표)로 순간이동하여 같은 자리에서 출발하게 함
        if (conductorDrone != null)
        {
            transform.position = conductorDrone.transform.position;
        }

        // 콜라이더 활성화

        if (droneCollider != null)
        {
            droneCollider.enabled = true;
        }

        // 지휘자 드론 따라가기 + 파도 발사 동시 시작
        StartCoroutine(FollowConductor());
        if (totalBodyCount > 0)
        {
            waveFireCoroutine = StartCoroutine(WaveFireCoroutine());
        }
    }

    /// <summary>
    /// 지휘자 드론 따라가기
    /// </summary>
    private IEnumerator FollowConductor()
    {
        // 머리로부터의 추적 인덱스 간격 (각 세그먼트 간의 포인트 간격)
        // bodyIndex가 1부터 시작하므로, 첫 번째는 8번째 이전 포인트, 두 번째는 16번째...
        int pointIndex = bodyIndex * 8; 

        while (!isDead && !isScattered && conductorDrone != null)
        {
            var history = conductorDrone.GetPathHistory();

            if (history != null && history.Count > pointIndex)
            {
                Vector3 targetPosition = history[pointIndex];

                // 헤드의 속도보다 너무 느리면 꼬리가 점점 뒤처지므로, 헤드 속도보다 항상 충분히 빠르게 따라가도록 보정
                float speedLimit = Mathf.Max(followSpeed, conductorDrone.GetMoveSpeed() * 1.5f);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speedLimit * Time.deltaTime);
            }
            else
            {
                // 기록이 아직 없을 때는 지휘자 드론의 현재 위치를 바로 추적
                Vector3 targetPosition = conductorDrone.GetCurrentPosition();
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }

            yield return null;
        }
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

        // 파도 발사 코루틴 중지
        if (waveFireCoroutine != null)
        {
            StopCoroutine(waveFireCoroutine);
            waveFireCoroutine = null;
        }

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
            // Y축 최소 -3f ~ 최대 9f 범위 Clamp 제한
            scatterTarget.y = Mathf.Clamp(scatterTarget.y, -3f, 9f);



            // 랜덤 위치로 이동
            while (Vector2.Distance(transform.position, scatterTarget) > 0.5f && !isDead)
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
                // Y축 최소 -3f ~ 최대 9f 범위 Clamp 제한
                newTarget.y = Mathf.Clamp(newTarget.y, -3f, 9f);



                while (Vector2.Distance(transform.position, newTarget) > 0.5f && !isDead)
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
    /// 파도 총알 발사 (지휘자 추적 중 순차 발사 — 스케일 성장 없음)
    /// </summary>
    private void FireWaveBullet()
    {
        if (bossController == null || player == null)
            return;

        GameObject bullet = bossController.GetBulletFromPool();
        if (bullet == null)
            return;

        bullet.transform.position = transform.position;
        Vector3 direction = (player.position - transform.position).normalized;

        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            // 스케일 성장 없이 일반 발사
            projectile.Initialize(direction, bulletDamage, bossController);
        }
    }

    /// <summary>
    /// 파도 발사 코루틴 — bodyIndex 순서(낮을수록 머리에 가까움)에 따라
    /// 0.5초씩 초기 딜레이를 두고 totalBodyCount * 0.5초 주기로 반복 발사
    /// </summary>
    private IEnumerator WaveFireCoroutine()
    {
        // 머리와의 거리 순서 기반 초기 딜레이 (bodyIndex 1 = 0초, 2 = 0.5초, ...)
        float initialDelay = (bodyIndex - 1) * 0.5f;
        yield return new WaitForSeconds(initialDelay);

        float period = Mathf.Max(totalBodyCount * 0.5f, 0.5f);

        while (!isDead && !isScattered)
        {
            FireWaveBullet();
            yield return new WaitForSeconds(period);
        }
    }

    /// <summary>
    /// 총알 발사 (흩어진 후 사용 — 스케일 성장)
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

        // 총알 스크립트 설정 (플레이어 방향 + 스케일 성장, 기본 배율 적용)
        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            projectile.InitializeGrowing(direction, bulletDamage, bossController);
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

        // 파도 발사 코루틴 개별 중지
        if (waveFireCoroutine != null)
        {
            StopCoroutine(waveFireCoroutine);
            waveFireCoroutine = null;
        }

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
        isInitialized = false; // 풀 재사용을 위해 초기화 플래그 리셋
    }

    private void Update()
    {
        if (isScattered)
        {
            // 플레이어를 바라보게 만듦 (좌우 스케일 반전)
            if (player != null)
            {
                float dirX = player.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.1f)
                {
                    Vector3 scale = transform.localScale;
                    float absX = Mathf.Abs(scale.x);
                    scale.x = dirX > 0f ? absX : -absX;
                    transform.localScale = scale;
                }
            }

            // Y축 최소값 -3f 및 최대값 9f 한계 설정 (관성 뚫고 나감 방지)
            float clampedY = Mathf.Clamp(transform.position.y, -3f, 9f);
            if (transform.position.y != clampedY)
            {
                transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);
                if (rb2d != null)
                {
                    if (transform.position.y <= -3f && rb2d.linearVelocity.y < 0f)
                    {
                        rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
                    }
                    else if (transform.position.y >= 9f && rb2d.linearVelocity.y > 0f)
                    {
                        rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
                    }
                }
            }
        }
    }


    /// <summary>
    /// 흩어진 상태에서 "Wall" 또는 "Ground" 태그 오브젝트를 관통하지 못하도록 제어
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        if (isScattered && !isDead && (other.CompareTag("Wall") || other.CompareTag("Ground")))
        {
            // 가장 가까운 콜라이더 경계선 좌표 계산
            Vector2 closestPoint = other.ClosestPoint(transform.position);
            Vector2 pushDir = ((Vector2)transform.position - closestPoint);

            // 완전히 겹쳐서 방향을 잡기 힘든 경우 위로 밀어냄
            if (pushDir.sqrMagnitude < 0.001f)
            {
                pushDir = Vector2.up;
            }
            else
            {
                pushDir.Normalize();
            }

            // 콜라이더 바깥 방향으로 약간 밀어내기 (관통 방지)
            transform.position += (Vector3)(pushDir * 0.1f);

            // 벽/바닥을 뚫고 들어가려는 속도 성분 상쇄 (슬라이딩 이동 구현)
            if (rb2d != null)
            {
                float dot = Vector2.Dot(rb2d.linearVelocity, pushDir);
                if (dot < 0f)
                {
                    rb2d.linearVelocity -= dot * pushDir;
                }
            }
        }
    }
}


