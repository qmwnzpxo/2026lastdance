using UnityEngine;
using System.Collections;

/// <summary>
/// 드론 상태 열거형
/// </summary>
public enum DroneState
{
    Spawning,  // 소환 중 - 콜라이더 비활성, 상승 완료 대기
    Active,    // 활성 - 플레이어 추적 + 공격
    Inactive,  // 비활성 - StealthSniper 전용, 반투명 상태
    Dead       // 사망 - 사망 이펙트 후 오브젝트 비활성화
}

/// <summary>
/// 보스 드론 AI 스크립트
/// 보스로부터 소환되어 독립적으로 플레이어를 추적·공격
/// </summary>
public class BossDrone : MonoBehaviour
{
    [Header("드론 스탯")]
    [SerializeField] private float droneHP = 50f;
    [SerializeField] private float droneMoveSpeed = 3f;
    [SerializeField] private float droneAttackInterval = 2f;
    [SerializeField] private float droneDamage = 2f;

    [Header("참조 컴포넌트")]
    private Transform player;
    private BossController bossController;
    private Rigidbody2D rb2d;
    private Collider2D droneCollider;

    // 현재 상태
    private DroneState currentState = DroneState.Spawning;
    private float currentHP;
    private Coroutine attackCoroutine;

    // 드론 패턴 관련
    private DronePattern assignedPattern = DronePattern.LaserBomber;
    private int droneCount;
    private float duration1;
    private float duration2;
    private float patternDamage;
    private float attackInterval;
    private int maxCycles;
    private int currentCycle = 0;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        droneCollider = GetComponent<Collider2D>();
        currentHP = droneHP;
    }

    /// <summary>
    /// 드론 초기화 (기본)
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller)
    {
        Initialize(playerTransform, controller, DronePattern.LaserBomber, new DronePatternParameters());
    }

    /// <summary>
    /// 드론 초기화 (패턴 지정)
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller, DronePattern pattern, DronePatternParameters parameters)
    {
        player = playerTransform;
        bossController = controller;
        assignedPattern = pattern;

        // 파라미터 설정
        droneCount = parameters.count;
        duration1 = parameters.duration1;
        duration2 = parameters.duration2;
        patternDamage = parameters.damage;
        attackInterval = parameters.interval;
        maxCycles = parameters.maxCycles;
        currentCycle = 0;

        // 소환 상태로 시작
        currentState = DroneState.Spawning;
        currentHP = droneHP;

        // 콜라이더 비활성
        if (droneCollider != null)
        {
            droneCollider.enabled = false;
        }

        // 상승 코루틴 시작
        StartCoroutine(RiseSequence());
    }

    /// <summary>
    /// 드론 패턴 파라미터 구조체
    /// </summary>
    public struct DronePatternParameters
    {
        public int count;
        public float duration1;
        public float duration2;
        public float damage;
        public float interval;
        public int maxCycles;
    }

    /// <summary>
    /// 드론 상승 시퀀스
    /// </summary>
    private IEnumerator RiseSequence()
    {
        Vector3 targetPosition = bossController.GetDroneRiseTarget();
        float riseSpeed = bossController.GetDroneRiseSpeed();

        // 상승 중
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, riseSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;

        // 등장 완료, Active 상태로 전환
        currentState = DroneState.Active;

        // 콜라이더 활성화
        if (droneCollider != null)
        {
            droneCollider.enabled = true;
        }

        // 패턴별 공격 코루틴 시작
        switch (assignedPattern)
        {
            case DronePattern.LaserBomber:
                attackCoroutine = StartCoroutine(LaserBomberPattern());
                break;
            case DronePattern.GroundTracker:
                attackCoroutine = StartCoroutine(GroundTrackerPattern());
                break;
            case DronePattern.StealthSniper:
                attackCoroutine = StartCoroutine(StealthSniperPattern());
                break;
        }
    }

    /// <summary>
    /// 패턴 A - 맵 전체 레이저 폭격 드론 (LaserBomber)
    /// </summary>
    private IEnumerator LaserBomberPattern()
    {
        // 랜덤 좌표로 이동
        Vector3 randomPosition = GetRandomMapPosition();
        while (Vector3.Distance(transform.position, randomPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, randomPosition, droneMoveSpeed * Time.deltaTime);
            yield return null;
        }

        // 조준 전조
        yield return new WaitForSeconds(duration1);

        // 레이저 발사
        FireLaser();

        // 레이저 유지
        yield return new WaitForSeconds(duration2);

        // 즉시 소멸
        Die();
    }

    /// <summary>
    /// 패턴 B - 지상 추적 폭격 드론 (GroundTracker)
    /// </summary>
    private IEnumerator GroundTrackerPattern()
    {
        // 3초 동안 플레이어 추적
        float elapsedTime = 0f;
        while (elapsedTime < duration1)
        {
            if (player != null)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                if (rb2d != null)
                {
                    rb2d.linearVelocity = direction * droneMoveSpeed;
                }
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 플레이어 좌표 저장
        Vector3 targetPos = player != null ? player.position : transform.position;

        // 폭격 요청
        RequestStrike(targetPos);

        // 즉시 소멸
        Die();
    }

    /// <summary>
    /// 패턴 C - 은신 저격 드론 (StealthSniper)
    /// </summary>
    private IEnumerator StealthSniperPattern()
    {
        while (currentState == DroneState.Active && currentCycle < maxCycles)
        {
            // Active 상태 - 총알 발사
            float activeElapsedTime = 0f;
            while (activeElapsedTime < duration1)
            {
                FireBullet();
                yield return new WaitForSeconds(attackInterval);
                activeElapsedTime += attackInterval;
            }

            // Inactive 상태로 전환
            currentState = DroneState.Inactive;

            // 반투명 이펙트 (필요시 추가)
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f);
            }

            // 비활성 시간 대기
            yield return new WaitForSeconds(duration2);

            // 새로운 랜덤 좌표로 순간이동
            Vector3 randomPosition = GetRandomMapPosition();
            transform.position = randomPosition;

            // Active 상태로 전환
            currentState = DroneState.Active;

            // 불투명 복귀
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            }

            currentCycle++;
        }

        // 사이클 완료 후 소멸
        Die();
    }

    /// <summary>
    /// 맵 전체 랜덤 좌표 반환
    /// </summary>
    private Vector3 GetRandomMapPosition()
    {
        // 맵 범위 내 랜덤 좌표 (필요시 맵 경계에 따라 조정)
        float randomX = Random.Range(-20f, 20f);
        float randomY = Random.Range(5f, 15f);
        return new Vector3(randomX, randomY, 0f);
    }

    /// <summary>
    /// 레이저 발사
    /// </summary>
    private void FireLaser()
    {
        // LineRenderer 또는 트레일 이펙트 기반 레이저 시각화
        // 여기서는 간단히 Raycast로 데미지 적용
        if (player == null)
            return;

        Vector3 direction = (player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, 100f);

        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            hit.collider.GetComponent<PlayerHealth>()?.TakeDamage(patternDamage);
        }

        // 레이저 시각화 (필요시 LineRenderer 추가)
    }

    /// <summary>
    /// 폭격 요청
    /// </summary>
    private void RequestStrike(Vector3 targetPos)
    {
        // 경고 마커 표시
        GameObject warningMarker = bossController.GetWarningMarkerFromPool();
        if (warningMarker != null)
        {
            warningMarker.transform.position = targetPos;
            StartCoroutine(ExecuteStrikeAfterDelay(targetPos, warningMarker, duration2));
        }
    }

    /// <summary>
    /// 지연 후 폭격 실행
    /// </summary>
    private IEnumerator ExecuteStrikeAfterDelay(Vector3 targetPos, GameObject warningMarker, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 경고 마커 반환
        if (warningMarker != null)
        {
            bossController.ReturnWarningMarkerToPool(warningMarker);
        }

        // 폭발 데미지 적용
        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(targetPos, 3f);
        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<PlayerHealth>()?.TakeDamage(patternDamage);
            }
        }

        // 폭발 이펙트 (필요시 추가)
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
            projectile.Initialize(direction, droneDamage, bossController);
        }
    }

    private void Update()
    {
        if (currentState == DroneState.Active && player != null)
        {
            // 플레이어 방향으로 이동
            Vector3 direction = (player.position - transform.position).normalized;
            
            if (rb2d != null)
            {
                rb2d.linearVelocity = direction * droneMoveSpeed;
            }
        }
    }

    /// <summary>
    /// 피격 처리
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (currentState == DroneState.Dead)
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
        currentState = DroneState.Dead;

        // 공격 코루틴 중지
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        // 보스 컨트롤러에서 드론 제거
        if (bossController != null)
        {
            bossController.UnregisterDrone(gameObject);
        }

        // 사망 이펙트 (필요시 추가)
        // 오브젝트 비활성화
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 공격 코루틴 중지
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }
    }
}
