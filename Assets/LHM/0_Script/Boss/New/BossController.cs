using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BossBT;

/// <summary>
/// 보스 상태 열거형
/// </summary>
public enum BossState
{
    Idle,        // 기본 대기
    Appearing,   // 등장 연출 (무적 판정)
    Attacking,   // 공격/콤보 진행 중
    Resting,     // 콤보 후 2초 휴식
    BoosterJump, // 부스터 점프 이동 중
    Hit,         // 피격
    Dead         // 사망
}

/// <summary>
/// 공격 패턴 열거형
/// </summary>
public enum AttackPattern
{
    Stomp,
    MachinegunBurst,
    MachinegunRapid,
    Howitzer,
    Cannon,
    Charge,
    DroneSpawn,    // 드론 소환 (보스 내부 → 머리 위 상승 등장)
    Wait3Sec       // 돌진-대기-돌진 콤보 내 3초 대기 전용
}


/// <summary>
/// 보스 컨트롤러 - FSM 상태 머신 + Behavior Tree 하이브리드 구조
/// FSM: 보스 생애주기와 페이즈 전환 관리
/// BT: Attacking 상태 내부에서 공격 패턴 결정
/// </summary>
public class BossController : MonoBehaviour, IDamageable
{
    [Header("보스 기본 스탯")]
    [SerializeField] private float maxHP = 500f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float boosterJumpSpeed = 10f;
    [SerializeField] private float attackPower = 3f;

    [Header("참조 컴포넌트")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private BossHealthBar healthBar;
    [SerializeField] private BossAttackPatterns attackPatterns;

    [Header("오브젝트 풀")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private GameObject warningMarkerPrefab;  // 곡사포 경고 마커
    [SerializeField] private GameObject airBurstPelletPrefab; // AirBurst 파편 프리팹
    [SerializeField] private GameObject bouncePelletPrefab;   // BounceBlast 파편 프리팹
    [SerializeField] private int poolSize = 50;

    [Header("이펙트")]
    [SerializeField] private GameObject phaseTransitionEffect;
    [SerializeField] private GameObject deathEffect;

    [Header("드론 설정")]
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private Transform droneSpawnOrigin;
    [SerializeField] private Vector3 droneRiseTargetOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float droneRiseSpeed = 5f;
    [SerializeField] private float droneSpawnAnticipationTime = 0.4f;
    [SerializeField] private int maxDroneCount = 2;

    // 현재 상태
    private BossState currentState = BossState.Appearing;
    private float currentHP;
    private int currentPhase = 1;

    // 오브젝트 풀
    private Queue<GameObject> bulletPool = new Queue<GameObject>();
    private Queue<GameObject> cannonballPool = new Queue<GameObject>();
    private Queue<GameObject> warningMarkerPool = new Queue<GameObject>();
    private Queue<GameObject> airBurstPelletPool = new Queue<GameObject>();
    private Queue<GameObject> bouncePelletPool = new Queue<GameObject>();

    // Behavior Tree
    private BossBT.BTNode _phase1BT;
    private BossBT.BTNode _phase2BT;
    private BossBT.BTNode _activeBT;

    // 드론 관리
    private List<GameObject> activeDrones = new List<GameObject>();

    // 페이즈 전환 추적
    private bool hasPhaseTransitioned = false;

    private void Awake()
    {
        currentHP = maxHP;
        InitializeObjectPools();
    }

    private void Start()
    {
        if (healthBar != null)
        {
            healthBar.Initialize(maxHP);
        }

        // Behavior Tree 빌드
        BuildBehaviorTrees();

        // 등장 연출 시작
        StartCoroutine(AppearingSequence());
    }

    /// <summary>
    /// 오브젝트 풀 초기화
    /// </summary>
    private void InitializeObjectPools()
    {
        if (bulletPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject bullet = Instantiate(bulletPrefab);
                bullet.SetActive(false);
                bulletPool.Enqueue(bullet);
            }
        }

        if (cannonballPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject cannonball = Instantiate(cannonballPrefab);
                cannonball.SetActive(false);
                cannonballPool.Enqueue(cannonball);
            }
        }

        if (warningMarkerPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject marker = Instantiate(warningMarkerPrefab);
                marker.SetActive(false);
                warningMarkerPool.Enqueue(marker);
            }
        }

        if (airBurstPelletPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject pellet = Instantiate(airBurstPelletPrefab);
                pellet.SetActive(false);
                airBurstPelletPool.Enqueue(pellet);
            }
        }

        if (bouncePelletPrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject pellet = Instantiate(bouncePelletPrefab);
                pellet.SetActive(false);
                bouncePelletPool.Enqueue(pellet);
            }
        }
    }

    /// <summary>
    /// 총알 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetBulletFromPool()
    {
        if (bulletPool.Count > 0)
        {
            GameObject bullet = bulletPool.Dequeue();
            bullet.SetActive(true);
            return bullet;
        }
        else if (bulletPrefab != null)
        {
            return Instantiate(bulletPrefab);
        }
        return null;
    }

    /// <summary>
    /// 총알 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnBulletToPool(GameObject bullet)
    {
        if (bullet != null)
        {
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }

    /// <summary>
    /// 대포탄 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetCannonballFromPool()
    {
        if (cannonballPool.Count > 0)
        {
            GameObject cannonball = cannonballPool.Dequeue();
            cannonball.SetActive(true);
            return cannonball;
        }
        else if (cannonballPrefab != null)
        {
            return Instantiate(cannonballPrefab);
        }
        return null;
    }

    /// <summary>
    /// 대포탄 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnCannonballToPool(GameObject cannonball)
    {
        if (cannonball != null)
        {
            cannonball.SetActive(false);
            cannonballPool.Enqueue(cannonball);
        }
    }

    /// <summary>
    /// 경고 마커 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetWarningMarkerFromPool()
    {
        if (warningMarkerPool.Count > 0)
        {
            GameObject marker = warningMarkerPool.Dequeue();
            marker.SetActive(true);
            return marker;
        }
        else if (warningMarkerPrefab != null)
        {
            return Instantiate(warningMarkerPrefab);
        }
        return null;
    }

    /// <summary>
    /// 경고 마커 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnWarningMarkerToPool(GameObject marker)
    {
        if (marker != null)
        {
            marker.SetActive(false);
            warningMarkerPool.Enqueue(marker);
        }
    }

    /// <summary>
    /// AirBurst 파편 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetAirBurstPelletFromPool()
    {
        if (airBurstPelletPool.Count > 0)
        {
            GameObject pellet = airBurstPelletPool.Dequeue();
            pellet.SetActive(true);
            return pellet;
        }
        else if (airBurstPelletPrefab != null)
        {
            return Instantiate(airBurstPelletPrefab);
        }
        return null;
    }

    /// <summary>
    /// AirBurst 파편 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnAirBurstPelletToPool(GameObject pellet)
    {
        if (pellet != null)
        {
            pellet.SetActive(false);
            airBurstPelletPool.Enqueue(pellet);
        }
    }

    /// <summary>
    /// BounceBlast 파편 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetBouncePelletFromPool()
    {
        if (bouncePelletPool.Count > 0)
        {
            GameObject pellet = bouncePelletPool.Dequeue();
            pellet.SetActive(true);
            return pellet;
        }
        else if (bouncePelletPrefab != null)
        {
            return Instantiate(bouncePelletPrefab);
        }
        return null;
    }

    /// <summary>
    /// BounceBlast 파편 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnBouncePelletToPool(GameObject pellet)
    {
        if (pellet != null)
        {
            pellet.SetActive(false);
            bouncePelletPool.Enqueue(pellet);
        }
    }

    /// <summary>
    /// 등장 연출 시퀀스
    /// </summary>
    private IEnumerator AppearingSequence()
    {
        ChangeState(BossState.Appearing);

        // 하늘 위에서 낙하
        Vector3 startPosition = transform.position + Vector3.up * 20f;
        transform.position = startPosition;

        // 낙하 애니메이션
        float fallDuration = 2f;
        float elapsedTime = 0f;
        Vector3 targetPosition = startPosition - Vector3.up * 20f;

        while (elapsedTime < fallDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / fallDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;

        // 착지 모션 대기
        yield return new WaitForSeconds(1f);

        // Appearing 상태 해제 후 Idle로 전이
        ChangeState(BossState.Idle);
    }

    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(BossState newState)
    {
        if (currentState == BossState.Dead && newState != BossState.Dead)
            return;

        currentState = newState;

        // 애니메이터 파라미터 설정
        if (animator != null)
        {
            animator.SetInteger("State", (int)newState);
        }

        // 상태별 초기화 로직
        switch (newState)
        {
            case BossState.Idle:
                StartCoroutine(IdleState());
                break;
            case BossState.Attacking:
                StartCoroutine(AttackingState());
                break;
            case BossState.Resting:
                StartCoroutine(RestingState());
                break;
            case BossState.Dead:
                StartCoroutine(DeadState());
                break;
        }
    }

    /// <summary>
    /// Behavior Tree 빌드 - 페이즈별 BT 트리 조립
    /// </summary>
    private void BuildBehaviorTrees()
    {
        // 1페이즈 BT
        _phase1BT = new BossBT.Selector(new List<BossBT.BTNode>
        {
            // 콤보 A - 근거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 5f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            }),
            // 콤보 B - 근거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 5f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
                ,new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
                ,new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            }),
            // 콤보 E - 중거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 5f, 30f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.WaitNode(3f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            }),
            // 콤보 F - 중거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 5f, 30f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            }),
            // 콤보 C - 원거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 30f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            }),
            // 콤보 D - 원거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 30f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
            })
        });

        // 2페이즈 BT
        _phase2BT = new BossBT.Selector(new List<BossBT.BTNode>
        {
            // 콤보 G - 근거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 5f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 H - 근거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 5f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 K - 중거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 5f, 30f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 L - 중거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 5f, 30f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 M - 중거리 (드론 소환)
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 5f, 30f),
                new BossBT.CheckMaxDroneCount(() => activeDrones.Count, maxDroneCount),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.DroneSpawn, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this)
            }),
            // 콤보 I - 원거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 30f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 J - 원거리
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 30f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Cannon, this, this)
            }),
            // 콤보 N - 원거리 (드론 소환)
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 30f, 9999f),
                new BossBT.CheckMaxDroneCount(() => activeDrones.Count, maxDroneCount),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.DroneSpawn, this, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this)
            })
        });
    }

    /// <summary>
    /// Idle 상태 - BT 트리 준비
    /// </summary>
    private IEnumerator IdleState()
    {
        while (currentState == BossState.Idle)
        {
            // 페이즈에 맞는 BT 트리 선택
            _activeBT = GetCurrentPhase() == 1 ? _phase1BT : _phase2BT;

            // 트리 내 모든 노드 Reset
            ResetBehaviorTree(_activeBT);

            // Attacking 상태로 전이
            ChangeState(BossState.Attacking);
            yield break;
        }
    }

    /// <summary>
    /// Behavior Tree 전체 Reset
    /// </summary>
    private void ResetBehaviorTree(BossBT.BTNode node)
    {
        if (node is BossBT.BTComposite composite)
        {
            composite.Reset();
        }
        else
        {
            node.Reset();
        }
    }

    /// <summary>
    /// Attacking 상태 - BT 틱 실행
    /// </summary>
    private IEnumerator AttackingState()
    {
        while (currentState == BossState.Attacking)
        {
            // 매 프레임 BT 틱
            BossBT.NodeStatus status = _activeBT.Evaluate();

            if (status == BossBT.NodeStatus.Success || status == BossBT.NodeStatus.Failure)
            {
                // BT 완료, Resting 상태로 전이
                ChangeState(BossState.Resting);
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Resting 상태 - 2초 휴식
    /// </summary>
    private IEnumerator RestingState()
    {
        yield return new WaitForSeconds(2f);
        ChangeState(BossState.Idle);
    }

    /// <summary>
    /// 현재 페이즈 계산
    /// </summary>
    public int GetCurrentPhase()
    {
        return (currentHP / maxHP > 0.5f) ? 1 : 2;
    }

    /// <summary>
    /// 피격 처리
    /// </summary>
    public void TakeDamage(float damage)
    {
        // Appearing 상태에서는 무적
        if (currentState == BossState.Appearing)
            return;

        // Attacking, Resting, Idle 상태에서만 피격 판정
        if (currentState != BossState.Attacking && 
            currentState != BossState.Resting && 
            currentState != BossState.Idle)
            return;

        currentHP -= damage;

        // 체력 바 업데이트
        if (healthBar != null)
        {
            healthBar.UpdateHealth(currentHP);
        }

        // 페이즈 재계산
        int newPhase = GetCurrentPhase();
        if (newPhase != currentPhase && !hasPhaseTransitioned)
        {
            currentPhase = newPhase;
            hasPhaseTransitioned = true;
            StartCoroutine(PhaseTransitionEffect());

            // 진행 중이던 코루틴 정리 및 BT 교체
            StopAllCoroutines();
            _activeBT = GetCurrentPhase() == 1 ? _phase1BT : _phase2BT;
            ResetBehaviorTree(_activeBT);
            ChangeState(BossState.Idle);
        }

        // 사망 체크
        if (currentHP <= 0)
        {
            ChangeState(BossState.Dead);
        }
    }

    /// <summary>
    /// 페이즈 전환 연출
    /// </summary>
    private IEnumerator PhaseTransitionEffect()
    {
        if (phaseTransitionEffect != null)
        {
            Instantiate(phaseTransitionEffect, transform.position, Quaternion.identity);
        }

        // 카메라 쉐이크 등 추가 연출 가능
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// Dead 상태 - 사망 처리
    /// </summary>
    private IEnumerator DeadState()
    {
        // 사망 애니메이션 재생
        if (animator != null)
        {
            animator.SetTrigger("Dead");
        }

        yield return new WaitForSeconds(2f);

        // 사망 이펙트
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        // 보스 오브젝트 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 플레이어 참조 설정
    /// </summary>
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>
    /// 현재 상태 getter
    /// </summary>
    public BossState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// 공격력 getter
    /// </summary>
    public float GetAttackPower()
    {
        return attackPower;
    }

    /// <summary>
    /// 부스터 점프 속도 getter
    /// </summary>
    public float GetBoosterJumpSpeed()
    {
        return boosterJumpSpeed;
    }

    /// <summary>
    /// 드론 등록
    /// </summary>
    public void RegisterDrone(GameObject drone)
    {
        if (drone != null && !activeDrones.Contains(drone))
        {
            activeDrones.Add(drone);
        }
    }

    /// <summary>
    /// 드론 제거
    /// </summary>
    public void UnregisterDrone(GameObject drone)
    {
        if (drone != null && activeDrones.Contains(drone))
        {
            activeDrones.Remove(drone);
        }
    }

    /// <summary>
    /// 드론 소환 위치 getter
    /// </summary>
    public Transform GetDroneSpawnOrigin()
    {
        return droneSpawnOrigin;
    }

    /// <summary>
    /// 드론 상승 목표 위치 getter
    /// </summary>
    public Vector3 GetDroneRiseTarget()
    {
        return transform.position + droneRiseTargetOffset;
    }

    /// <summary>
    /// 드론 상승 속도 getter
    /// </summary>
    public float GetDroneRiseSpeed()
    {
        return droneRiseSpeed;
    }

    /// <summary>
    /// 드론 소환 전조 시간 getter
    /// </summary>
    public float GetDroneSpawnAnticipationTime()
    {
        return droneSpawnAnticipationTime;
    }

    /// <summary>
    /// 드론 프리팹 getter
    /// </summary>
    public GameObject GetDronePrefab()
    {
        return dronePrefab;
    }

    /// <summary>
    /// 플레이어 Transform getter
    /// </summary>
    public Transform GetPlayer()
    {
        return player;
    }
}
