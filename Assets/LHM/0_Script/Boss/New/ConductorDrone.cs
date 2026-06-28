using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 지휘자 드론 - 지렁이 드론의 머리 부분
/// ∞ 루트로 움직이며 3초마다 에너지탄 발사
/// 몸체 드론들의 이동 방향을 결정
/// </summary>
public class ConductorDrone : MonoBehaviour, IDamageable
{
    [Header("드론 스탯")]
    [SerializeField] private float droneHP = 100f;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float infinityPathScale = 5f;
    [SerializeField] private float infinityPathSpeed = 2f;

    [Header("공격 설정")]
    [SerializeField] private float energyBulletInterval = 3f;
    [SerializeField] private float energyBulletDamage = 10f;
    [SerializeField] private GameObject energyBulletPrefab;

    [Header("참조 컴포넌트")]
    [SerializeField] private Transform player;
    [SerializeField] private BossController bossController;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private Collider2D droneCollider;

    [Header("몸체 드론 관리")]
    [SerializeField] private BodyDrone[] bodyDrones;

    private float currentHP;
    private bool isDead = false;
    private float pathTime = 0f;
    private Vector3 centerPosition;
    private Coroutine attackCoroutine;
    private bool isInitialized = false;

    // 위치 기록 관련 (지렁이 기차 움직임용)
    private List<Vector3> pathHistory = new List<Vector3>();
    private const float minDistanceBetweenPoints = 0.15f;

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

    private void Start()
    {
        if (!isInitialized)
        {
            Initialize(player, bossController, transform.position);
        }
    }

    /// <summary>
    /// 드론 초기화
    /// </summary>
    public void Initialize(Transform playerTransform, BossController controller, Vector3 spawnPosition)
    {
        if (isInitialized) return;
        isInitialized = true;

        if (playerTransform != null) player = playerTransform;
        if (controller != null) bossController = controller;
        centerPosition = spawnPosition;
        currentHP = droneHP;
        isDead = false;
        pathTime = 0f;

        // 콜라이더 활성화
        if (droneCollider != null)
        {
            droneCollider.enabled = true;
        }

        // 몸체 드론 초기화
        InitializeBodyDrones();

        // 이동 및 공격 시작
        StartCoroutine(MovementLoop());
        attackCoroutine = StartCoroutine(EnergyBulletAttack());
    }

    /// <summary>
    /// 몸체 드론 초기화
    /// </summary>
    private void InitializeBodyDrones()
    {
        if (bodyDrones == null || bodyDrones.Length == 0)
            return;

        for (int i = 0; i < bodyDrones.Length; i++)
        {
            if (bodyDrones[i] != null)
            {
                bodyDrones[i].Initialize(player, bossController, this, i + 1, bodyDrones.Length);
            }
        }
    }

    /// <summary>
    /// ∞ 루트 이동 루프
    /// </summary>
    private IEnumerator MovementLoop()
    {
        while (!isDead)
        {
            pathTime += Time.deltaTime * infinityPathSpeed;

            // ∞ 루트 (Lemniscate) 경로 계산
            float x = centerPosition.x + infinityPathScale * Mathf.Cos(pathTime);
            float y = centerPosition.y + infinityPathScale * Mathf.Sin(2f * pathTime) / 2f;

            Vector3 targetPosition = new Vector3(x, y, 0f);

            // 부드러운 이동
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // 위치 기록 갱신
            UpdatePathHistory();

            yield return null;
        }
    }

    /// <summary>
    /// 에너지탄 발사 공격
    /// </summary>
    private IEnumerator EnergyBulletAttack()
    {
        while (!isDead)
        {
            if (player != null)
            {
                FireEnergyBullet();
            }

            yield return new WaitForSeconds(energyBulletInterval);
        }
    }

    /// <summary>
    /// 에너지탄 발사
    /// </summary>
    private void FireEnergyBullet()
    {
        if (energyBulletPrefab == null || bossController == null || player == null)
            return;

        // 에너지탄 생성
        GameObject bullet = Instantiate(energyBulletPrefab, transform.position, Quaternion.identity);
        
        // 플레이어 방향 계산
        Vector3 direction = (player.position - transform.position).normalized;

        // 에너지탄 스크립트 설정
        EnergyBullet energyBullet = bullet.GetComponent<EnergyBullet>();
        if (energyBullet != null)
        {
            energyBullet.Initialize(direction, energyBulletDamage);
        }
    }

    /// <summary>
    /// 현재 위치 getter (몸체 드론이 추적용)
    /// </summary>
    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    /// <summary>
    /// 위치 기록 Getter
    /// </summary>
    public List<Vector3> GetPathHistory()
    {
        return pathHistory;
    }

    /// <summary>
    /// 속도 Getter
    /// </summary>
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    /// <summary>
    /// 위치 기록 갱신 (지정된 최소 거리만큼 이동 시 좌표 추가)
    /// </summary>
    private void UpdatePathHistory()
    {
        if (pathHistory.Count == 0 || Vector2.Distance(transform.position, pathHistory[0]) > minDistanceBetweenPoints)
        {
            pathHistory.Insert(0, transform.position);

            // 메모리 절약을 위해 필요한 개수만큼만 기록 유지
            int maxHistory = (bodyDrones != null ? bodyDrones.Length : 8) * 10 + 10;
            if (pathHistory.Count > maxHistory)
            {
                pathHistory.RemoveAt(pathHistory.Count - 1);
            }
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

        // 공격 코루틴 중지
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        // 몸체 드론들에게 사망 통보 (흩어지기)
        NotifyBodyDronesOfDeath();

        // 보스 컨트롤러에서 드론 제거
        if (bossController != null)
        {
            bossController.UnregisterDrone(gameObject);
        }

        // 사망 이펙트 (필요시 추가)
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 몸체 드론들에게 사망 통보
    /// </summary>
    private void NotifyBodyDronesOfDeath()
    {
        if (bodyDrones == null)
            return;

        foreach (BodyDrone bodyDrone in bodyDrones)
        {
            if (bodyDrone != null)
            {
                bodyDrone.OnConductorDeath();
            }
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
