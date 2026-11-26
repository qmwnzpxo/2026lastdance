using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BossBT;
using BossSystem;
using UnMachine;

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
    Dead,        // 사망
    Walk,        // 걷기 이동 상태 추가

    // ── v2 흐름 상태 (T31, useFlowV2 On 일 때만 사용). Animator State 정수와 같아서 맨 끝에 고정값으로 둔다 ──
    Telegraph = 8,       // 패턴 예고
    Punishable = 9,      // 반격 틈 (피해 ×1.2)
    Stunned = 10,        // 벽 충돌 기절 (피해 ×1.2)
    Groggy = 11,         // 그로기 (피해 ×1.5)
    PhaseTransition = 12 // 페이즈 전환 〔임시값: 상태명〕
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
    DroneSpawn,          // 드론 소환 (보스 내부 → 머리 위 상승 등장)
    Wait3Sec             // 돌진-대기-돌진 콤보 내 3초 대기 전용
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

    [Header("피격 무적 설정")]
    [Tooltip("피격 시 깜빡일 SpriteRenderer 목록 (비워두면 자신 및 자식 자동 검색)")]
    [SerializeField] private SpriteRenderer[] hitFlashSprites;
    [Tooltip("피격 후 무적 지속 시간 (초)")]
    [SerializeField] private float hitInvincibilityDuration = 0.2f;
    [Tooltip("무적 중 깜빡임 횟수")]
    [SerializeField] private int hitFlashCount = 3;

    [Header("오디오 설정")]
    [Tooltip("배틀 BGM 재생용 AudioSource (지정 안할 시 자동 검색)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("보스 전투용 루프형 배경음악(BGM)")]
    [SerializeField] private AudioClip battleBGM;

    [Header("부스터 설정 (비행 이동)")]
    [Tooltip("보스 부스터용 이펙트 GameObject (활성화/비활성화로 제어)")]
    [SerializeField] private GameObject boosterEffect;
    [Tooltip("부스터 이펙트가 위치할 기준점 (보스 뒤편 등에 배치한 빈 GameObject의 Transform)")]
    [SerializeField] private Transform boosterEffectPoint;
    [Tooltip("부스터 이펙트의 크기(Scale) 조절 변수")]
    [SerializeField] private Vector3 boosterEffectScale = Vector3.one;
    [Tooltip("부스터 이펙트의 회전(Euler Angles) 조절 변수 (X축 등 회전 각도 제어용)")]
    [SerializeField] private Vector3 boosterEffectRotation = Vector3.zero;




    [Tooltip("보스 이동 시 출력할 부스터 사운드")]
    [SerializeField] private AudioClip boosterSound;
    [Tooltip("부스터 사운드 전용 AudioSource (지정 안할 시 자동 생성하여 루핑 재생)")]
    [SerializeField] private AudioSource boosterAudioSource;

    private bool isBoosterActive = false;
    private GameObject instantiatedBoosterEffect;






    [Header("드론 설정")]
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private Transform droneSpawnOrigin;
    [SerializeField] private Vector3 droneRiseTargetOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float droneRiseSpeed = 5f;
    [SerializeField] private float droneSpawnAnticipationTime = 0.4f;
    [SerializeField] private int maxDroneCount = 2;

    [Header("v2 흐름 (T31, BACKLOG Q19 C)")]
    [Tooltip("On = 전투 시작 후 BossFlowDriver 가 Idle~Resting 루프를 소유. Off = 기존 코루틴 흐름 그대로")]
    [SerializeField] private bool _useFlowV2 = false;
    [SerializeField] private UnMachine.Boss.BossFlowDriver _flowDriver;

    // Start 에서 토글 + Driver 준비 상태로 확정. Driver 가 없거나 꺼져 있으면 기존 흐름으로 동작한다.
    private bool _isFlowV2Active;

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
    private Queue<GameObject> dronePool = new Queue<GameObject>();


    // 씬에 활성화된 발사체 추적 (페이즈 전환 시 일괄 회수용)
    private List<GameObject> activeCannonballs = new List<GameObject>();
    private List<GameObject> activeBullets = new List<GameObject>();
    private List<GameObject> activeWarningMarkers = new List<GameObject>();

    // Behavior Tree
    private BossBT.BTNode _phase1BT;
    private BossBT.BTNode _phase2BT;
    private BossBT.BTNode _activeBT;

    // 드론 관리
    private List<GameObject> activeDrones = new List<GameObject>();

    // 페이즈 전환 추적
    private bool hasPhaseTransitioned = false;

    // 피격 무적
    private bool _isInvincible = false;
    private Coroutine _hitInvincibilityCoroutine;

    // 런타임 콘텍스트 (SO 기반 패턴 공유 참조 및 AnimationEventBridge)
    private BossContext _bossContext;

    // 돌진 등 패턴 중 FacePlayer() 억제 플래그
    private bool _facingLocked = false;

    // ── 외부 알림 이벤트 (T2: 기믹 Director·체력바 연동용) ──
    // 기존 FSM 흐름은 그대로 두고 발생 지점에서 호출만 한다.

    /// <summary>등장 연출이 끝나고 전투가 시작될 때 1회.</summary>
    public event System.Action BattleStarted;

    /// <summary>페이즈가 바뀐 직후 (인자: 새 페이즈 번호). 전환은 최초 1회만 일어난다.</summary>
    public event System.Action<int> PhaseChanged;

    /// <summary>Dead 상태 최초 진입 시 1회.</summary>
    public event System.Action Died;

    /// <summary>
    /// 무적(체력바 자물쇠 표시 대상) 여부가 바뀔 때. 0.2초 피격 무적은 포함하지 않는다 (BACKLOG Q4).
    /// 구독 직후에는 이벤트를 기다리지 말고 <see cref="IsInvulnerable"/> 를 먼저 읽는다.
    /// </summary>
    public event System.Action<bool> InvulnerableChanged;

    /// <summary>현재 무적(자물쇠 표시 대상) 여부. 등장 중·(v2) 페이즈 전환 중 true. (T19에서 2페이즈 쉴드 추가 예정)</summary>
    public bool IsInvulnerable => ComputeInvulnerable();

    // 마지막으로 알린 값 (변화 감지용). false로 시작해서 Start의 Appearing 진입 때 true 알림이 1회 나간다.
    private bool _isInvulnerableNotified = false;
    private bool _hasDied = false;

    /// <summary>
    /// HP UI 이벤트(온HpChanged) 연동용.
    /// 구시스템 DamageHandler와 HP UI가 연결되어 있으므로 제거 안 함.
    /// GetComponents<IDamageable>()의 중복 사굴를 막기 위해
    /// Awake에서 AcquireInvincibility().하여 영구 무적으로 설정함.
    /// </summary>
    private DamageHandler damageHandler;


    private void Awake()
    {
        currentHP = maxHP;
        InitializeObjectPools();

        // DamageHandler를 영구 무적으로 설정 —
        // PlayerCombat/PlayerDash의 GetComponents<IDamageable>()에서
        // 중복으로 호출될 때 DamageHandler.TakeDamage()는 즉시 return.
        // HP 실질 변경은 BossController.TakeDamage()에서만 수행.
        damageHandler = gameObject.GetComponent<DamageHandler>();
        if (damageHandler != null)
            damageHandler.AcquireInvincibility();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }


    /// <summary>
    /// 씰 주입 - SO 패턴 및 AnimationEventBridge 연동 전용
    /// BossContext를 생성하고 현재사 Animator/Player를 주입
    /// </summary>
    private void InjectSceneDependencies()
    {
        _bossContext = BossContext.Create(this, animator, player);
        Debug.Log("[Boss] BossContext 주입 완료");
    }

    private void Start()
    {
        if (healthBar != null)
        {
            healthBar.Initialize(maxHP);
        }

        // 런타임 콘텍스트 주입 (SO 패턴 및 AnimationEventBridge 주입)
        InjectSceneDependencies();

        // Behavior Tree 빌드
        BuildBehaviorTrees();

        // v2 흐름 확정 — Driver.Awake 가 끝난 뒤(Start)에 준비 상태를 읽는다.
        _isFlowV2Active = _useFlowV2 && _flowDriver != null && _flowDriver.IsReady;
        if (_useFlowV2 && !_isFlowV2Active)
            GameLog.Error(this, "[Boss] useFlowV2 On 이지만 BossFlowDriver 가 없거나 준비되지 않음 → 기존 흐름으로 동작");

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

        // 드론 풀 초기화
        if (dronePrefab != null)
        {
            for (int i = 0; i < maxDroneCount + 2; i++)
            {
                GameObject drone = Instantiate(dronePrefab);
                drone.SetActive(false);
                dronePool.Enqueue(drone);
            }
        }
    }


    /// <summary>
    /// 총알 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetBulletFromPool()
    {
        GameObject bullet = null;
        if (bulletPool.Count > 0)
        {
            bullet = bulletPool.Dequeue();
            bullet.SetActive(true);
        }
        else if (bulletPrefab != null)
        {
            bullet = Instantiate(bulletPrefab);
            bullet.SetActive(true);
        }
        if (bullet != null) activeBullets.Add(bullet);
        return bullet;
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
            activeBullets.Remove(bullet);
        }
    }

    /// <summary>
    /// 대포탄 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetCannonballFromPool()
    {
        GameObject cannonball = null;
        if (cannonballPool.Count > 0)
        {
            cannonball = cannonballPool.Dequeue();
            cannonball.SetActive(true);

            // 이전 상태 완전 초기화 (Waiting/Falling 상태로 남아있으면 머리 위에 고정되는 문제 방지)
            HowitzerCannonball hw = cannonball.GetComponent<HowitzerCannonball>();
            if (hw != null) hw.ResetForPool();
        }
        else if (cannonballPrefab != null)
        {
            cannonball = Instantiate(cannonballPrefab);
            cannonball.SetActive(true);
        }
        if (cannonball != null) activeCannonballs.Add(cannonball);
        return cannonball;
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
            activeCannonballs.Remove(cannonball);
        }
    }

    /// <summary>
    /// 경고 마커 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetWarningMarkerFromPool()
    {
        GameObject marker = null;
        if (warningMarkerPool.Count > 0)
        {
            marker = warningMarkerPool.Dequeue();
            marker.SetActive(true);
        }
        else if (warningMarkerPrefab != null)
        {
            marker = Instantiate(warningMarkerPrefab);
            marker.SetActive(true);
        }
        if (marker != null) activeWarningMarkers.Add(marker);
        return marker;
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
            activeWarningMarkers.Remove(marker);
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
            GameObject pellet = Instantiate(airBurstPelletPrefab);
            pellet.SetActive(true);
            return pellet;
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
            GameObject pellet = Instantiate(bouncePelletPrefab);
            pellet.SetActive(true);
            return pellet;
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
    /// 드론 오브젝트 풀에서 가져오기
    /// </summary>
    public GameObject GetDroneFromPool()
    {
        if (dronePool.Count > 0)
        {
            GameObject drone = dronePool.Dequeue();
            drone.SetActive(true);
            return drone;
        }
        else if (dronePrefab != null)
        {
            GameObject drone = Instantiate(dronePrefab);
            drone.SetActive(true);
            return drone;
        }
        return null;
    }


    /// <summary>
    /// 드론 오브젝트 풀에 반환하기
    /// </summary>
    public void ReturnDroneToPool(GameObject drone)
    {
        if (drone != null)
        {
            drone.SetActive(false);
            dronePool.Enqueue(drone);
        }
    }



    /// <summary>
    /// 씬에 남아있는 모든 활성 발사체를 풀로 회수 (페이즈 전환 시 호출)
    /// 캐논볼/총알/경고마커가 보스 이동을 막는 문제 방지
    /// FindObjectsByType 대신 직접 추적 리스트를 사용 (Unity 버전 호환)
    /// </summary>
    public void ClearAllActiveProjectiles()
    {
        // 활성 캐논볼 전체 회수 (추적 리스트 기반)
        for (int i = activeCannonballs.Count - 1; i >= 0; i--)
        {
            GameObject cb = activeCannonballs[i];
            if (cb != null && cb.activeSelf)
            {
                cb.SetActive(false);
                cannonballPool.Enqueue(cb);
            }
        }
        activeCannonballs.Clear();

        // 활성 총알 전체 회수 (추적 리스트 기반)
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            GameObject bullet = activeBullets[i];
            if (bullet != null && bullet.activeSelf)
            {
                bullet.SetActive(false);
                bulletPool.Enqueue(bullet);
            }
        }
        activeBullets.Clear();

        // 경고 마커 전체 회수 (추적 리스트 기반)
        for (int i = activeWarningMarkers.Count - 1; i >= 0; i--)
        {
            GameObject marker = activeWarningMarkers[i];
            if (marker != null && marker.activeSelf)
            {
                marker.SetActive(false);
                warningMarkerPool.Enqueue(marker);
            }
        }
        activeWarningMarkers.Clear();

        Debug.Log("[Boss] 페이즈 전환: 씬의 모든 발사체 회수 완료");
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

        // 배틀 BGM 루프 재생
        if (audioSource != null && battleBGM != null)
        {
            audioSource.clip = battleBGM;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 전투 시작 알림 (Idle 전이 직전 — 구독자가 1페이즈 기믹을 시작한다)
        GameLog.Info("[Boss] BattleStarted", this);
        BattleStarted?.Invoke();

        // Appearing 상태 해제 후 Idle로 전이 (v2 면 Driver 가 루프 시작)
        StartBattleLoop();
    }


    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(BossState newState)
    {
        if (currentState == BossState.Dead && newState != BossState.Dead)
            return;

        currentState = newState;

        // 무적 표시 여부 갱신 (바뀐 경우에만 알림)
        UpdateInvulnerableNotification();

        // Dead 최초 진입 알림 — Dead→Dead 재진입은 무시
        if (newState == BossState.Dead && !_hasDied)
        {
            _hasDied = true;
            GameLog.Info("[Boss] Died", this);
            Died?.Invoke();
        }

        // 애니메이터 파라미터 설정
        if (animator != null)
        {
            // Idle 및 Attacking 대기 상태로 바뀔 때 애니메이션을 강제로 롤백하지 않고 
            // 실제 공격 패턴이나 구체적인 비행이 작동할 때만 애니메이션 상태를 명시적으로 제어하도록 변경
            if (newState != BossState.Idle && newState != BossState.Attacking)
            {
                animator.SetInteger("State", (int)newState);
            }
        }


        // 상태별 초기화 로직

        // v2: Idle·Attacking·Resting 등은 BossFlowDriver 가 소유 → 레거시 코루틴을 시작하지 않는다 (Dead 만 기존 처리)
        if (_isFlowV2Active && newState != BossState.Dead)
            return;

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
            // 콤보 A - 근거리 (2m 미만): 밟기 → 0.2초 정지 → 기관총 3연발
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 2f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.WaitNode(0.2f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this)
            }),
            // 콤보 B - 중거리 (2m~8m): 기관총 3연발 → 0.2초 정지 → 기관총 연사
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 2f, 8f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunBurst, this, this),
                new BossBT.WaitNode(0.2f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this)
            }),
            // 콤보 C - 원거리 (9m 이상): 곡사포 → 0.2초 정지 → 돌진
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 9f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.WaitNode(0.2f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this)
            })
        });

        // 2페이즈 BT
        _phase2BT = new BossBT.Selector(new List<BossBT.BTNode>
        {
            // 콤보 D - 근거리 (2m 미만): 밟기 → 0.1초 정지 → 밟기
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 0f, 2f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this),
                new BossBT.WaitNode(0.1f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Stomp, this, this)
            }),
            // 콤보 E - 중거리 (2m~8m): 기관총 연사 → 0.1초 정지 → 드론 소환
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 2f, 8f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.MachinegunRapid, this, this),
                new BossBT.WaitNode(0.1f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.DroneSpawn, this, this)
            }),
            // 콤보 F - 원거리 (9m 이상): 곡사포 → 0.2초 정지 → 돌진
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
                new BossBT.CheckDistance(transform, player, 9f, 9999f),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Howitzer, this, this),
                new BossBT.WaitNode(0.2f, this),
                new BossBT.ExecuteAttackNode(attackPatterns, AttackPattern.Charge, this, this)
            }),
            // 콤보 G - 폴백: 거리 무관 곡사포 (D/E/F 모두 거리 조건 불일치 시)
            // 8m~9m 데드존 방지 역할
            new BossBT.Sequence(new List<BossBT.BTNode>
            {
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
    /// BTComposite는 override된 Reset()이 자동으로 자식들을 재귀 리셋함
    /// </summary>
    private void ResetBehaviorTree(BTNode node)
    {
        node?.Reset();
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
    /// Resting 상태 - 2초 휴식 + 플레이어 방향으로 이동 (1·2페이즈 공통)
    /// </summary>
    private IEnumerator RestingState()
    {
        // 2페이즈는 휴식 시간을 1초로 단축하여 보스를 더 공격적으로 만듦 (1페이즈는 2초 유지)
        float restDuration = GetCurrentPhase() == 1 ? 2f : 1f;
        float elapsed = 0f;

        // 현재 이동 상태 캐싱 (0: 정지, 1: 걷기, 2: 비행)
        // 매 프레임 애니메이션 트리거가 재발동되어 리셋되는 현상 방지
        int currentMoveMode = -1;

        while (elapsed < restDuration && currentState == BossState.Resting)
        {
            // 플레이어 방향으로 이동
            if (player != null && rb2d != null)
            {
                float diffX = player.position.x - transform.position.x;
                // 플레이어와의 거리가 1.5f 이하로 가까우면 더 쫓아가지 않고 제자리 정지 (달달달 떠는 현상 방지)
                if (Mathf.Abs(diffX) <= 1.5f)
                {
                    rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
                    
                    if (currentMoveMode != 0)
                    {
                        currentMoveMode = 0;
                        SetBoosterActive(false);
                        if (animator != null)
                        {
                            // 걷기 정지 시 애니메이터 State를 강제로 3(대기)으로 되돌리지 않고 Walk 포즈로 고정
                            animator.SetTrigger("StopWalk"); // 걷기 중지 트리거만 발동
                            animator.SetTrigger("Idle");     // 대기 전환 트리거만 발동 (사용 시)
                        }
                    }

                }
                else
                {
                    float dirX = Mathf.Sign(diffX);
                    rb2d.linearVelocity = new Vector2(dirX * walkSpeed, rb2d.linearVelocity.y);
                    
                    if (currentMoveMode != 1)
                    {
                        currentMoveMode = 1;
                        // 이동 모드로 전환 (이펙트 활성화 및 내부 애니메이션 자동 분기)
                        SetBoosterActive(true);
                    }
                }

            }

            elapsed += Time.deltaTime;
            yield return null;
        }



        // 이동 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
        }
        SetBoosterActive(false);

        ChangeState(BossState.Idle);
    }


    /// <summary>
    /// 보스의 부스터 불꽃 이펙트, 사운드, 비행 애니메이션(BoosterJump) 상태를 켜고 끕니다.
    /// </summary>
    public void SetBoosterActive(bool active)
    {
        if (isBoosterActive == active) return;
        isBoosterActive = active;

        // 1. 이펙트 오브젝트 제어 및 위치 동기화 (프리팹 대응 포함)
        if (boosterEffect != null)
        {
            if (instantiatedBoosterEffect == null)
            {
                // boosterEffect가 씬 상에 배치된 것이 아닌 프리팹 에셋인 경우 동적 생성
                if (boosterEffect.scene.name == null)
                {
                    instantiatedBoosterEffect = Instantiate(boosterEffect);
                }
                else
                {
                    instantiatedBoosterEffect = boosterEffect;
                }
            }

            if (active && boosterEffectPoint != null && instantiatedBoosterEffect != null)
            {
                // 기준점을 부모로 설정하여 보스 이동 시 항상 따라다니도록 세팅
                instantiatedBoosterEffect.transform.SetParent(boosterEffectPoint, false);
                instantiatedBoosterEffect.transform.localPosition = Vector3.zero;
                instantiatedBoosterEffect.transform.localRotation = Quaternion.Euler(boosterEffectRotation); // 설정한 회전 오일러 각도 적용
                instantiatedBoosterEffect.transform.localScale = boosterEffectScale; // 설정한 스케일 적용 (부모 크기 영향 방지)


            }

            if (instantiatedBoosterEffect != null)
            {
                instantiatedBoosterEffect.SetActive(active);

                // 하위 파티클 시스템 재출력 보장
                var particles = instantiatedBoosterEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particles)
                {
                    if (active)
                    {
                        ps.Clear();
                        ps.Play(true);
                    }
                    else
                    {
                        ps.Stop(true);
                    }
                }

                // 하위 트레일 렌더러의 잔상 꼬임 방지 및 방출 제어
                var trails = instantiatedBoosterEffect.GetComponentsInChildren<TrailRenderer>();
                foreach (var tr in trails)
                {
                    if (active)
                    {
                        tr.Clear();
                        tr.emitting = true;
                    }
                    else
                    {
                        tr.emitting = false;
                        tr.Clear();
                    }
                }
            }
        }





        // 2. 오디오 소스 초기화 및 제어
        if (boosterSound != null)
        {
            if (boosterAudioSource == null)
            {
                // BGM 오디오 소스와 분리하기 위해 런타임에 동적으로 AudioSource 추가
                boosterAudioSource = gameObject.AddComponent<AudioSource>();
                boosterAudioSource.playOnAwake = false;
                boosterAudioSource.spatialBlend = 1f; // 3D 사운드
                boosterAudioSource.loop = true;
            }

            if (active)
            {
                boosterAudioSource.clip = boosterSound;
                boosterAudioSource.Play();
            }
            else
            {
                boosterAudioSource.Stop();
            }
        }

        // 3. 애니메이터 상태 업데이트 (이동 중일 때 1페이즈=Walk(8), 2페이즈=BoosterJump(4) 자동 분기)
        if (animator != null)
        {
            if (active)
            {
                if (GetCurrentPhase() == 1)
                {
                    // 1페이즈: 걷기 애니메이션
                    animator.SetInteger("State", 8);
                    animator.SetTrigger("Walk");
                }
                else
                {
                    // 2페이즈: 비행(점프) 애니메이션
                    animator.SetInteger("State", 4); 
                    animator.SetTrigger("BoosterJump");
                }
            }
            else
            {
                // 이동 정지 트리거 발동
                animator.SetTrigger("StopBooster");
                animator.SetTrigger("StopWalk");

                // 이동 완료 후 강제로 대기(3 또는 0)로 리셋하지 않고 직전의 자세를 고정 유지함
            }
        }


    }


    /// <summary>
    /// 2페이즈 경계 HP 비율 (boss.md 5장 "HP 50% 최초 하회"). 정확히 50%는 1페이즈.
    /// </summary>
    public const float Phase2HpRatio = 0.5f;

    /// <summary>
    /// HP 비율 → 페이즈. 비율 >= 50% 이면 1, 미만이면 2. 체력바 표시도 이 함수를 쓴다.
    /// </summary>
    public static int PhaseForRatio(float hpRatio)
    {
        return hpRatio >= Phase2HpRatio ? 1 : 2;
    }

    /// <summary>
    /// 현재 페이즈 계산
    /// </summary>
    public int GetCurrentPhase()
    {
        return PhaseForRatio(currentHP / maxHP);
    }

    /// <summary>
    /// 플레이어 방향으로 보스 좌우 방향 전환 (localScale.x flip)
    /// Dead/Appearing 상태 또는 _facingLocked 중에는 동작하지 않음
    /// </summary>
    private void FacePlayer()
    {
        if (_facingLocked) return;
        if (player == null || currentState == BossState.Dead || currentState == BossState.Appearing)
            return;

        float dirX = player.position.x - transform.position.x;
        if (Mathf.Abs(dirX) > 0.1f)
        {
            Vector3 scale = transform.localScale;
            float absX = Mathf.Abs(scale.x);
            scale.x = dirX > 0 ? absX : -absX;
            transform.localScale = scale;
        }
    }

    /// <summary>
    /// 돌진 등 패턴 진행 중 facing 잠금 — dirX > 0이면 오른쪽, < 0이면 왼쪽으로 고정
    /// </summary>
    public void LockFacing(float dirX)
    {
        _facingLocked = true;
        if (Mathf.Abs(dirX) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            float absX = Mathf.Abs(scale.x);
            scale.x = dirX > 0 ? absX : -absX;
            transform.localScale = scale;
        }
    }

    /// <summary>
    /// facing 잠금 해제 — 이후 FacePlayer()가 다시 동작함
    /// </summary>
    public void UnlockFacing()
    {
        _facingLocked = false;
    }

    private void Update()
    {
        // 매 프레임 플레이어 방향으로 보스 회전
        FacePlayer();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F6))
            DebugSetHpToPhaseBoundary();

        if (Input.GetKeyDown(KeyCode.F1))
        {
            float targetHP = maxHP * 0.49f;
            currentHP = targetHP;

            if (healthBar != null)
                healthBar.UpdateHealth(currentHP);

            Debug.Log($"[Debug] F1: 보스 HP를 {targetHP:F0} (49%)로 강제 설정 → 2페이즈 전환 유도");

            // v2: TakeDamage 와 같은 경로 (BossFlowMachine PhaseTransition, Groggy 면 보류) — T33
            if (_isFlowV2Active)
            {
                TryRequestFlowPhaseTransition();
                return;
            }

            // 페이즈 전환 트리거 (이미 Attacking 중이라면 즉시 전환)
            int newPhase = GetCurrentPhase();
            if (newPhase != currentPhase && !hasPhaseTransitioned)
            {
                currentPhase = newPhase;
                hasPhaseTransitioned = true;

                BossBT.BTNode oldBT = _activeBT;
                _activeBT = _phase2BT;
                ResetBehaviorTree(oldBT);
                ResetBehaviorTree(_activeBT);

                // BossAttackPatterns 코루틴 강제 종료
                if (attackPatterns != null)
                    attackPatterns.ForceStopAllPatterns();

                SetBoosterActive(false);

                CancelHitInvincibility();
                StopAllCoroutines();

                SpawnPhaseTransitionEffect();

                NotifyPhaseChanged();

                StartBattleLoop();
                Debug.Log("[Debug] F1: 2페이즈 전환 완료 → 드론 패턴 활성화됨");
            }
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// [디버그 F6] HP를 정확히 경계값(maxHP × 50%)으로 설정. 페이즈 전환은 하지 않는다.
    /// 이 상태에서 1P 유지를 확인한 뒤, 실제 타격으로 TakeDamage 경로의 2P 전환을 확인한다.
    /// </summary>
    private void DebugSetHpToPhaseBoundary()
    {
        currentHP = maxHP * Phase2HpRatio;

        if (damageHandler != null)
            damageHandler.SetHP(currentHP / maxHP);

        if (healthBar != null)
            healthBar.UpdateHealth(currentHP);

        GameLog.Info(
            $"[Debug] F6: 보스 HP {currentHP:F0}/{maxHP:F0} (경계) → 계산 페이즈 {GetCurrentPhase()}, 현재 {currentPhase}",
            this);
    }
#endif

    /// <summary>
    /// 피격 처리
    /// </summary>
    public void TakeDamage(float damage)
    {
        // Appearing 상태에서는 무적
        if (currentState == BossState.Appearing)
            return;

        // 피격 무적 프레임 중이면 무시
        if (_isInvincible)
            return;

        // Attacking, Resting, Idle 상태에서만 피격 판정 (v2 는 예고·반격 틈·기절·그로기도 포함)
        if (currentState != BossState.Attacking &&
            currentState != BossState.Resting &&
            currentState != BossState.Idle &&
            !(_isFlowV2Active && IsFlowV2DamageableState(currentState)))
            return;

        // 피격 무적 + 깜빡임 시작 (이전 코루틴 중복 방지)
        if (_hitInvincibilityCoroutine != null)
            StopCoroutine(_hitInvincibilityCoroutine);
        _hitInvincibilityCoroutine = StartCoroutine(HitInvincibilityCoroutine());

        // v2: 상태별 받는 피해 배율 (spec: boss.md 4장)
        if (_isFlowV2Active)
        {
            float multiplier = _flowDriver.DamageMultiplier;
            float scaled = damage * multiplier;
            GameLog.Info($"[Boss] 피해 {damage:F1} × {multiplier:F2} = {scaled:F1} ({currentState})", this);
            damage = scaled;
        }

        currentHP -= damage;

        // DamageHandler HP 동기화 — OnHPChanged 이벤트를 통해 HP UI 갱신
        if (damageHandler != null)
            damageHandler.SetHP(currentHP / maxHP);

        // 체력 바 업데이트
        if (healthBar != null)
        {
            healthBar.UpdateHealth(currentHP);
        }

        // 페이즈 재계산
        int newPhase = GetCurrentPhase();
        if (_isFlowV2Active)
        {
            // v2: 정리·무적·2P 시작은 BossFlowMachine PhaseTransition 이 맡는다 (T33, spec: boss.md 8장)
            TryRequestFlowPhaseTransition();
        }
        else if (newPhase != currentPhase && !hasPhaseTransitioned)
        {
            currentPhase = newPhase;
            hasPhaseTransitioned = true;

            // 1) 실행 중인 BT 노드 플래그 선초기화 (코루틴 중단 전에 수행)
            BossBT.BTNode oldBT = _activeBT;
            _activeBT = GetCurrentPhase() == 1 ? _phase1BT : _phase2BT;
            ResetBehaviorTree(oldBT);
            ResetBehaviorTree(_activeBT);

            // 2) BossAttackPatterns 코루틴 강제 종료
            //    StopAllCoroutines()는 BossController 코루틴만 멈추므로
            //    BossAttackPatterns(별개 MonoBehaviour)는 반드시 별도 정리해야 함
            if (attackPatterns != null)
                attackPatterns.ForceStopAllPatterns();  // Charge 관성/facing lock/플래그 포함 정리

            // 3) 부스터 이펙트 비활성화 (RestingState 도중 중단 시 이펙트가 남는 현상 방지)
            SetBoosterActive(false);

            // 4) 무적/깜빡임 즉시 해제 후 BossController 코루틴 전체 중단
            CancelHitInvincibility();
            StopAllCoroutines();

            // 3) 씬에 남은 발사체 전부 회수 → 보스 이동 막힘 방지
            ClearAllActiveProjectiles();

            // 4) 페이즈 전환 이펙트
            SpawnPhaseTransitionEffect();

            // 페이즈 전환 알림 (정리 완료 후, 새 루프 시작 전 — 구독자가 이전 페이즈 기믹을 회수하고 다음 기믹을 시작)
            NotifyPhaseChanged();

            // 5) 새 상태 시작 (레거시 Idle 코루틴. v2 는 위 분기에서 처리)
            StartBattleLoop();
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
    /// <summary>
    /// 피격 무적 코루틴 — hitInvincibilityDuration 동안 깜빡임 + 무적 처리
    /// </summary>
    private IEnumerator HitInvincibilityCoroutine()
    {
        _isInvincible = true;

        // SpriteRenderer 자동 검색 (인스펙터 미지정 시 한 번만 수행)
        if (hitFlashSprites == null || hitFlashSprites.Length == 0)
            hitFlashSprites = GetComponentsInChildren<SpriteRenderer>();

        // 깜빡임 간격: 무적 시간을 (횟수 × 2)로 나눠 켜고/끄기 반복
        float flashInterval = hitInvincibilityDuration / (hitFlashCount * 2);

        for (int i = 0; i < hitFlashCount; i++)
        {
            SetSpritesVisible(false);
            yield return new WaitForSeconds(flashInterval);
            SetSpritesVisible(true);
            yield return new WaitForSeconds(flashInterval);
        }

        _isInvincible = false;
        _hitInvincibilityCoroutine = null;
    }

    /// <summary>
    /// 피격 무적 즉시 취소 — StopAllCoroutines() 전에 반드시 호출하여
    /// 스프라이트 가시성과 무적 플래그를 안전하게 복원
    /// </summary>
    private void CancelHitInvincibility()
    {
        if (_hitInvincibilityCoroutine != null)
        {
            StopCoroutine(_hitInvincibilityCoroutine);
            _hitInvincibilityCoroutine = null;
        }
        _isInvincible = false;
        SetSpritesVisible(true);
    }

    /// <summary>
    /// 지정된 SpriteRenderer 배열의 활성/비활성 일괄 전환
    /// </summary>
    private void SetSpritesVisible(bool visible)
    {
        if (hitFlashSprites == null) return;
        foreach (var sr in hitFlashSprites)
            if (sr != null) sr.enabled = visible;
    }

    private IEnumerator PhaseTransitionEffect()
    {
        if (phaseTransitionEffect != null)
        {
            GameObject fx = Instantiate(phaseTransitionEffect, transform.position, Quaternion.identity);
            
            // 카메라 자식으로 들어가되 월드 Z가 0.16f로 유지되도록 카메라 Z 기반 fallbackDistance 동적 계산
            Hovl.HS_ScreenEffect hsEffect = fx.GetComponent<Hovl.HS_ScreenEffect>();
            if (hsEffect != null)
            {
                Camera cam = Camera.main;
                float camZ = cam != null ? cam.transform.position.z : -10f;
                hsEffect.snapOnStart = true;
                hsEffect.parentToCameraOnStart = true;
                hsEffect.fallbackDistance = 0.16f - camZ;
            }

            Vector3 pos = fx.transform.position;
            pos.z = 0.16f;
            fx.transform.position = pos;
        }





        // 카메라 쉐이크 등 추가 연출 가능
        yield return new WaitForSeconds(0.5f);
    }






    /// <summary>
    /// Dead 상태 - 사망 처리
    /// </summary>
    private IEnumerator DeadState()
    {
        Debug.Log("[Boss] 보스 사망 상태 진입 - 오브젝트 정리 시작");

        // 1. 모든 공격 코루틴 및 물리 이동/Facing 고정 강제 중단
        if (attackPatterns != null)
        {
            attackPatterns.ForceStopAllPatterns();
        }

        // 2. 씬에 남아있는 모든 발사체 (총알, 캐논볼, 경고 마커) 비활성화 및 회수
        ClearAllActiveProjectiles();

        // 3. 씬에 남아있는 모든 드론 비활성화 및 회수
        for (int i = activeDrones.Count - 1; i >= 0; i--)
        {
            GameObject drone = activeDrones[i];
            if (drone != null)
            {
                ReturnDroneToPool(drone);
            }
        }
        activeDrones.Clear();

        // 사망 애니메이션 재생
        if (animator != null)
        {
            animator.SetTrigger("Dead");
        }

        // 배틀 BGM 정지
        if (audioSource != null && audioSource.clip == battleBGM)
        {
            audioSource.Stop();
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
    /// 페이즈 전환 알림. 레거시 전환 블록(TakeDamage, 에디터 F1) 또는 v2 ApplyFlowPhase 에서 currentPhase 갱신 후 호출한다.
    /// </summary>
    private void NotifyPhaseChanged()
    {
        GameLog.Info($"[Boss] PhaseChanged → {currentPhase}", this);
        PhaseChanged?.Invoke(currentPhase);
    }

    /// <summary>
    /// 전투 루프를 시작한다 (전투 시작, 페이즈 전환 직후). v2 면 BossFlowDriver 가 현재 페이즈로 루프를 (다시) 시작하고,
    /// 아니면 기존처럼 Idle 코루틴으로 들어간다.
    /// </summary>
    private void StartBattleLoop()
    {
        if (_isFlowV2Active)
        {
            _flowDriver.StartLoop(currentPhase);
            return;
        }

        ChangeState(BossState.Idle);
    }

    /// <summary>(useFlowV2) BossFlowDriver 가 흐름 상태를 반영한다. 상태값·Animator 만 바꾸고 코루틴은 시작하지 않는다.</summary>
    public void ApplyFlowState(BossState state)
    {
        if (!_isFlowV2Active)
        {
            GameLog.Warn($"[Boss] useFlowV2 가 꺼져 있는데 ApplyFlowState({state}) 호출 → 무시", this);
            return;
        }

        ChangeState(state);
    }

    /// <summary>
    /// (useFlowV2) HP 가 50% 아래로 처음 내려갔으면 Driver 에 페이즈 전환을 요청한다 (1회). Groggy 중이면 Driver 가 보류한다.
    /// spec: boss.md 8장 전환 1 "HP 50% 최초 하회"
    /// </summary>
    private void TryRequestFlowPhaseTransition()
    {
        if (hasPhaseTransitioned || GetCurrentPhase() != 2)
            return;

        // 받아들여졌을 때만 1회 소진 (루프 시작 전·Dead 로 거절되면 다음 판정에서 다시 시도)
        if (_flowDriver.RequestPhaseTransition())
            hasPhaseTransitioned = true;
    }

    /// <summary>
    /// (useFlowV2) PhaseTransition 진입 시 BossFlowDriver 가 호출한다. 패턴 강제 종료·발사체 회수·부스터 끄기·
    /// 피격 깜빡임 해제·기존 전환 이펙트. 무적은 상태값(PhaseTransition)으로 <see cref="ComputeInvulnerable"/> 가 판정한다.
    /// spec: boss.md 8장 전환 2 "진행 중 패턴 중단, 화면의 탄 제거, 완전 무적"
    /// </summary>
    public void EnterFlowPhaseTransition()
    {
        if (!_isFlowV2Active)
        {
            GameLog.Warn("[Boss] useFlowV2 가 꺼져 있는데 EnterFlowPhaseTransition 호출 → 무시", this);
            return;
        }

        if (attackPatterns != null)
            attackPatterns.ForceStopAllPatterns(); // Charge 관성·facing lock 포함 정리

        SetBoosterActive(false);
        CancelHitInvincibility();
        ClearAllActiveProjectiles();
        SpawnPhaseTransitionEffect(); // 기존 화면 이펙트 유지. 역재생·증기·틴트는 T34

        GameLog.Info("[Boss] PhaseTransition 진입: 패턴 중단·발사체 회수·무적", this);
    }

    /// <summary>
    /// (useFlowV2) BossFlowMachine 의 페이즈가 바뀌면(PhaseTransition 종료) Driver 가 호출한다 → PhaseChanged 알림.
    /// 호출 직후 Driver 가 새 페이즈로 Idle → 다음 패턴을 시작한다 (기존 "알림 후 새 루프" 순서 유지).
    /// </summary>
    public void ApplyFlowPhase(int phase)
    {
        if (!_isFlowV2Active)
        {
            GameLog.Warn($"[Boss] useFlowV2 가 꺼져 있는데 ApplyFlowPhase({phase}) 호출 → 무시", this);
            return;
        }

        if (phase == currentPhase)
            return;

        currentPhase = phase;
        NotifyPhaseChanged();
    }

    /// <summary>페이즈 전환 화면 이펙트 생성 (레거시·v2 공용). 카메라 자식으로 붙고 월드 Z 0.16 유지.</summary>
    private void SpawnPhaseTransitionEffect()
    {
        if (phaseTransitionEffect == null)
            return;

        GameObject fx = Instantiate(phaseTransitionEffect, transform.position, Quaternion.identity);

        // 카메라 자식으로 들어가되 월드 Z가 0.16f로 유지되도록 카메라 Z 기반 fallbackDistance 동적 계산
        Hovl.HS_ScreenEffect hsEffect = fx.GetComponent<Hovl.HS_ScreenEffect>();
        if (hsEffect != null)
        {
            Camera cam = Camera.main;
            float camZ = cam != null ? cam.transform.position.z : -10f;
            hsEffect.snapOnStart = true;
            hsEffect.parentToCameraOnStart = true;
            hsEffect.fallbackDistance = 0.16f - camZ;
        }

        Vector3 pos = fx.transform.position;
        pos.z = 0.16f;
        fx.transform.position = pos;
    }

    /// <summary>v2 에서 기존 3상태(Idle·Attacking·Resting) 외에 피격을 받는 상태. PhaseTransition 은 제외 (T33 무적).</summary>
    private static bool IsFlowV2DamageableState(BossState state)
    {
        return state == BossState.Telegraph
            || state == BossState.Punishable
            || state == BossState.Stunned
            || state == BossState.Groggy;
    }

    /// <summary>무적 표시 대상 판정의 단일 기준. T19에서 2페이즈 쉴드 조건을 여기에 더한다.</summary>
    private bool ComputeInvulnerable()
    {
        // spec: boss.md 8장 전환 2 "완전 무적" (v2 PhaseTransition — 피격 판정에서도 제외됨, T33)
        return currentState == BossState.Appearing
            || currentState == BossState.PhaseTransition;
    }

    /// <summary>
    /// 현재 상태 기준 무적 표시 여부를 계산하고, 바뀌었으면 InvulnerableChanged 를 발생시킨다.
    /// 피격 0.2초 무적(_isInvincible)은 체력바에 표시하지 않으므로 제외한다 (BACKLOG Q4).
    /// </summary>
    private void UpdateInvulnerableNotification()
    {
        bool invulnerable = ComputeInvulnerable();
        if (invulnerable == _isInvulnerableNotified)
            return;

        _isInvulnerableNotified = invulnerable;
        GameLog.Info($"[Boss] InvulnerableChanged → {invulnerable}", this);
        InvulnerableChanged?.Invoke(invulnerable);
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
    /// 현재 활성 드론 수 getter
    /// </summary>
    public int GetActiveDroneCount()
    {
        return activeDrones.Count;
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
