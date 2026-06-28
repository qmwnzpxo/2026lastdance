using UnityEngine;
using System.Collections;

/// <summary>
/// 불도저 드론 - 돌진 및 내리찍기 공격
/// 플레이어와의 X 거리에 따라 공격 패턴 변경
/// </summary>
public class BulldozerDrone : MonoBehaviour, IDamageable
{
    [Header("드론 스탯")]
    [SerializeField] private float droneHP = 80f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float chargeSpeed = 25f;
    [SerializeField] private float chargeDistance = 30f;
    [SerializeField] private float slamDamage = 15f;
    [SerializeField] private float chargeDamage = 20f;

    [Header("공격 설정")]
    [SerializeField] private float bladeSlamInterval = 0.3f;
    [SerializeField] private float slamApproachRange = 3f;
    [SerializeField] private float xDistanceThreshold = 10f;

    [Header("참조 컴포넌트")]
    [SerializeField] private Transform player;
    [SerializeField] private BossController bossController;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private Collider2D droneCollider;
    [SerializeField] private GameObject bladeEffect;

    private float currentHP;
    private bool isAttacking = false;
    private bool isDead = false;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        droneCollider = GetComponent<Collider2D>();
        currentHP = droneHP;
        transform.localScale = Vector3.one * 5f; // 스케일 5로 고정

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

    public void Initialize(Transform playerTransform, BossController controller)
    {
        if (playerTransform != null) player = playerTransform;
        if (controller != null) bossController = controller;
        currentHP = droneHP;
        isDead = false;
        isAttacking = false;
        transform.localScale = Vector3.one * 5f; // 스케일 5로 고정

        // 콜라이더 활성화
        if (droneCollider != null)
        {
            droneCollider.enabled = true;
        }

        // 공격 패턴 시작
        StartCoroutine(AttackPatternLoop());
    }

    private void LateUpdate()
    {
        // 애니메이터나 부모 스케일의 영향을 강제로 무시하고 5로 고정
        transform.localScale = Vector3.one * 5f;
    }

    /// <summary>
    /// 공격 패턴 루프
    /// </summary>
    private IEnumerator AttackPatternLoop()
    {
        while (!isDead)
        {
            if (player == null)
            {
                yield return null;
                continue;
            }

            float xDistance = Mathf.Abs(transform.position.x - player.position.x);

            if (xDistance > xDistanceThreshold)
            {
                // 돌진 공격
                yield return StartCoroutine(ChargeAttack());
            }
            else
            {
                // 내리찍기 공격
                yield return StartCoroutine(SlamAttack());
            }

            // 공격 후 대기
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// 돌진 공격 (플레이어가 X10 이상 벗어났을 때)
    /// </summary>
    private IEnumerator ChargeAttack()
    {
        isAttacking = true;

        // 블레이드 두 번 내리찍기
        for (int i = 0; i < 2; i++)
        {
            if (bladeEffect != null)
            {
                bladeEffect.SetActive(true);
            }
            yield return new WaitForSeconds(bladeSlamInterval);
            if (bladeEffect != null)
            {
                bladeEffect.SetActive(false);
            }
            yield return new WaitForSeconds(bladeSlamInterval);
        }

        // 플레이어 방향으로 X 좌표 30만큼 빠르게 돌진
        Vector3 chargeDirection = player.position.x > transform.position.x ? Vector3.right : Vector3.left;
        Vector3 targetPosition = transform.position + chargeDirection * chargeDistance;

        float chargeTime = 0f;
        float chargeDuration = chargeDistance / chargeSpeed;

        while (chargeTime < chargeDuration && !isDead)
        {
            if (rb2d != null)
            {
                rb2d.linearVelocity = chargeDirection * chargeSpeed;
            }
            chargeTime += Time.deltaTime;
            yield return null;
        }

        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        isAttacking = false;
    }

    /// <summary>
    /// 내리찍기 공격 (플레이어가 X10 이하 벗어났을 때)
    /// </summary>
    private IEnumerator SlamAttack()
    {
        isAttacking = true;

        // 플레이어의 현재 좌표값 저장
        Vector3 targetPosition = player.position;

        // X축 범위 3까지 접근
        Vector3 approachDirection = (targetPosition - transform.position).normalized;
        Vector3 approachTarget = transform.position + approachDirection * slamApproachRange;

        while (Vector2.Distance(transform.position, approachTarget) > 0.5f && !isDead)
        {
            if (rb2d != null)
            {
                rb2d.linearVelocity = approachDirection * moveSpeed;
            }
            yield return null;
        }

        // 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 내리찍기 공격 (범위 데미지)
        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(transform.position, 3f);
        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<PlayerHealth>()?.TakeDamage(slamDamage);
            }
        }

        // 내리찍기 이펙트 (필요시 추가)

        isAttacking = false;
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
        isAttacking = false;

        // 보스 컨트롤러에서 드론 제거 + 풀로 반환
        if (bossController != null)
        {
            bossController.UnregisterDrone(gameObject);
            bossController.ReturnBulldozerDroneToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 돌진 중 플레이어 충돌 시
        if (isAttacking && other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealth>()?.TakeDamage(chargeDamage);
        }
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
