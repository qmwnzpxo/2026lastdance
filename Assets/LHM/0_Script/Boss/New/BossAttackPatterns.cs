using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 곡사포 탄종 열거형
/// </summary>
public enum HowitzerAmmoType
{
    Normal,     // 일반 곡사포 — 낙하 후 단순 폭발
    AirBurst,   // 공중 폭발 — 지면 도달 전 일정 높이에서 터져 파편 다방향 산탄
    BounceBlast // 바운스 폭발 — 지면 1회 충돌 후 여러 갈래로 튀어올라 다시 낙하하여 2차 폭발
}

/// <summary>
/// 드론 패턴 열거형
/// </summary>
public enum DronePattern
{
    LaserBomber,   // 맵 전체 레이저 폭격
    GroundTracker, // 지상 추적 폭격
    StealthSniper  // 은신 저격
}

/// <summary>
/// 보스 공격 패턴 구현 - 각 개별 공격 패턴 메서드 모음
/// </summary>
public class BossAttackPatterns : MonoBehaviour
{
    [Header("참조 컴포넌트")]
    [SerializeField] private BossController bossController;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb2d;

    [Header("공격 패턴 설정")]
    [SerializeField] private float stompDamage = 3f;
    [SerializeField] private float stompRange = 2f;
    [SerializeField] private float stompAnticipationTime = 0.5f;
    [SerializeField] private float stompCastTime = 1f;

    [SerializeField] private float machinegunDamage = 1f;
    [SerializeField] private float machinegunBurstInterval = 0.2f;
    [SerializeField] private float machinegunRapidInterval = 0.05f;
    [SerializeField] private float machinegunRapidDuration = 0.5f;

    [SerializeField] private float howitzerDamage = 10f;
    [SerializeField] private float howitzerRiseHeight = 8f;      // 수직 상승 목표 높이
    [SerializeField] private float howitzerRiseSpeed = 12f;      // 상승 속도
    [SerializeField] private float howitzerFallSpeed = 20f;      // 낙하 속도
    [SerializeField] private float howitzerWarningRadius = 9f;   // 경고 마커 배치 반경 (원래 6f -> 9f로 증가)
    [SerializeField] private float howitzerWarningDuration = 1f;  // 경고 표시 지속 시간
    [SerializeField] private float howitzerExplosionRange = 3f;   // 착탄 폭발 범위
    [SerializeField] private float howitzerBurstInterval = 0.3f; // 포탄 발사 간격
    [SerializeField] private int howitzerShotCount = 6;          // 발사 포탄 수

    // AirBurst 전용
    [SerializeField] private float airBurstHeight = 4f;         // 폭발 발동 고도
    [SerializeField] private int airBurstPelletCount = 8;        // 파편 수
    [SerializeField] private float airBurstPelletSpeed = 10f;     // 파편 속도
    [SerializeField] private float airBurstPelletRange = 6f;     // 파편 유효 사거리
    [SerializeField] private float airBurstDamage = 5f;          // 파편 1개당 데미지

    // BounceBlast 전용
    [SerializeField] private int bounceCount = 4;                 // 바운스 파편 수
    [SerializeField] private float bounceMinAngle = 45f;          // 솟구침 최소 각도
    [SerializeField] private float bounceMaxAngle = 70f;          // 솟구침 최대 각도
    [SerializeField] private float bounceSpeed = 8f;              // 바운스 파편 초속
    [SerializeField] private float bounceBlastRange = 2.5f;      // 2차 폭발 범위
    [SerializeField] private float bounceBlastDamage = 7f;        // 2차 폭발 데미지

    // 탄종 가중치
    [SerializeField] private float[] howitzerAmmoWeights = { 0.5f, 0.25f, 0.25f };

    [SerializeField] private float cannonDamage = 10f;
    [SerializeField] private float cannonRange = 3f;
    [SerializeField] private float cannonCastTime = 1.5f;

    [SerializeField] private float chargeDamage = 5f;
    [SerializeField] private float chargeRange = 10f;
    [SerializeField] private float chargeCastTime = 0.8f;
    [SerializeField] private float chargeStunDuration = 1f;  // 벽 충돌 후 스턴 지속 시간

    [Header("드론 소환 설정")]
    [SerializeField] private float droneSpawnAnticipationTime = 0.4f;

    // 패턴 A (LaserBomber)
    [SerializeField] private int dronePatternA_Count = 10;
    [SerializeField] private float droneAimDuration = 0.5f;
    [SerializeField] private float droneLaserDuration = 0.8f;
    [SerializeField] private float droneLaserDamage = 5f;

    // 패턴 B (GroundTracker)
    [SerializeField] private int dronePatternB_Count = 1;
    [SerializeField] private float droneTrackDuration = 3f;   // 추적 후 좌표 저장까지 시간
    [SerializeField] private float droneStrikeDuration = 1f;   // 경고→폭격 딜레이
    [SerializeField] private float droneStrikeDamage = 15f;

    // 패턴 C (StealthSniper)
    [SerializeField] private int dronePatternC_Count = 5;
    [SerializeField] private float droneActiveDuration = 3f;
    [SerializeField] private float droneInactiveDuration = 1f;
    [SerializeField] private float droneSnipeInterval = 0.5f;
    [SerializeField] private float droneSnipeDamage = 3f;
    [SerializeField] private int dronePatternC_MaxCycles = 5;

    // 패턴 가중치
    [SerializeField] private float[] dronePatternWeights = { 0.33f, 0.34f, 0.33f };

    [Header("판정 박스 설정")]
    [SerializeField] private Transform stompHitBox;
    [SerializeField] private Transform chargeHitBox;
    [SerializeField] private LayerMask playerLayer;

    [Header("발사 위치 설정")]
    [Tooltip("기관총 총알 발사 위치 목록 (비어있으면 폴백 사용)")]
    [SerializeField] private Transform[] bulletFirePoints;
    [Tooltip("곡사포/대포 포탄 발사 위치 목록 (비어있으면 폴백 사용)")]
    [SerializeField] private Transform[] cannonballFirePoints;
    [Tooltip("기관총, 곡사포, 대포 발사 시 포구 위치에 생성할 발사/폭발 이펙트")]
    [SerializeField] private GameObject launchExplosionEffect;

    [Header("곡사포 파편 프리팹")]
    [SerializeField] private GameObject airBurstPelletPrefab;   // AirBurst 파편 프리팹
    [SerializeField] private GameObject bouncePelletPrefab;     // BounceBlast 파편 프리팹

    [Header("돌진/짓밟기 이펙트 설정")]
    [Tooltip("돌진 시 생성할 이펙트 프리팹 (부스터 화염 등)")]
    [SerializeField] private GameObject chargeEffectPrefab;
    [Tooltip("돌진 시 보스 주변을 감싸는 추가 이펙트 프리팹 (배리어, 바람막 등)")]
    [SerializeField] private GameObject chargeSurroundEffectPrefab;
    [Tooltip("돌진 이펙트 생성 및 부착 위치 (Transform)")]
    [SerializeField] private Transform chargeEffectSpawnPoint;
    [Tooltip("돌진 주변 이펙트의 생성 위치 및 각도를 조절할 빈 오브젝트 위치 (Transform)")]
    [SerializeField] private Transform chargeSurroundEffectSpawnPoint;
    [Tooltip("짓밟기(Stomp) 충격 시 생성할 폭발/충격 이펙트")]
    [SerializeField] private GameObject stompEffectPrefab;

    [Header("공격 패턴 연속 제어 설정")]
    [Tooltip("동일 패턴을 연속해서 사용할 수 있는 최대 횟수 (지정 횟수 이상 도달 시 랜덤 대체 패턴 실행)")]
    [SerializeField] private int maxConsecutiveSamePattern = 2;

    /// <summary>돌진이 벽에 부딪혀 스턴에 들어갈 때 (v2 그로기 게이지용, T61).</summary>
    public event System.Action ChargeHitWall;

    private AttackPattern lastExecutedPattern = AttackPattern.Wait3Sec; // 보조용 값으로 초기화
    private int consecutivePatternCount = 0;


    [Header("사운드 설정")]
    [Tooltip("발사 음향을 출력할 AudioSource (지정 안하면 자생 자동 검색)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("기관총 발사 사운드")]
    [SerializeField] private AudioClip machinegunFireSound;
    [Tooltip("곡사포 / 대포 발사 사운드")]
    [SerializeField] private AudioClip howitzerFireSound;
    [Tooltip("돌진 부스터 사운드")]
    [SerializeField] private AudioClip chargeSound;
    [Tooltip("짓밟기 충격 사운드")]
    [SerializeField] private AudioClip stompSound;






    // ── Animation Event 콜백 플래그 (중복 호출 방지) ──
    private bool _stompImpactCalled = false;
    private bool _howitzerLaunchPending = false;

    // ── 돌진 벽 충돌 감지 플래그 (OnTriggerEnter2D → Charge 코루틴 통신용) ──
    private bool _chargeHitWall = false;

    // ── Howitzer 포격 자세 유지 플래그 (TriggerStopHowitzer() 호출 시 해제) ──
    private bool _howitzerStopped = false;

    private void Awake()
    {
        if (bossController == null)
            bossController = GetComponent<BossController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (rb2d == null)
            rb2d = GetComponent<Rigidbody2D>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if(playerLayer == 0)
            playerLayer = LayerMask.GetMask("player");
    }


    /// <summary>
    /// 페이즈 전환 등 외부 강제 중단 시 호출 —
    /// BossController.StopAllCoroutines()는 BossController에서 시작된 코루틴만 멈추고
    /// BossAttackPatterns 에서 시작된 Charge/Howitzer/DroneSpawn 등은 그대로 살아있으므로
    /// 이 메서드를 반드시 별도로 호출해야 함.
    /// </summary>
    public void ForceStopAllPatterns()
    {
        StopAllCoroutines();

        // 내부 플래그 전부 초기화 (다음 패턴 시작 시 오염 방지)
        _chargeHitWall       = false;
        _stompImpactCalled   = false;
        _howitzerLaunchPending = false;
        _howitzerStopped     = false;

        // 고착 방지: 모든 애니메이터 트리거 리셋
        if (animator != null)
        {
            animator.ResetTrigger("StompAnticipation");
            animator.ResetTrigger("MachinegunAnticipation");
            animator.ResetTrigger("HowitzerAnticipation");
            animator.ResetTrigger("CannonAnticipation");
            animator.ResetTrigger("ChargeAnticipation");
            animator.ResetTrigger("DroneSpawnAnticipation");
            animator.ResetTrigger("stopHowitzer");
            animator.ResetTrigger("StopHowitzer");
            animator.ResetTrigger("AttackStart");
        }

        // 물리 속도 강제 정지 (Charge 도중 중단 시 관성 남음 방지)
        if (rb2d != null)
            rb2d.linearVelocity = Vector2.zero;

        // Facing 잠금 해제 (Charge 도중 중단 시 _facingLocked 고착 방지)
        if (bossController != null)
            bossController.UnlockFacing();
    }


    /// <summary>
    /// 발사 위치 배열에서 랜덤으로 하나 선택.
    /// 배열이 비어있거나 null이면 fallback 반환
    /// </summary>
    private Vector3 GetRandomFirePoint(Transform[] points, Vector3 fallback)
    {
        if (points == null || points.Length == 0)
            return fallback;

        // null 아닌 항목만 추려서 랜덤 선택
        var valid = System.Array.FindAll(points, p => p != null);
        if (valid.Length == 0)
            return fallback;

        return valid[Random.Range(0, valid.Length)].position;
    }

    /// <summary>
    /// 가중치 기반 랜덤 탄종 결정
    /// </summary>
    private HowitzerAmmoType SelectRandomAmmoType()
    {
        if (howitzerAmmoWeights == null || howitzerAmmoWeights.Length == 0)
            return HowitzerAmmoType.Normal;

        float totalWeight = 0f;
        foreach (float weight in howitzerAmmoWeights)
        {
            totalWeight += weight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        for (int i = 0; i < howitzerAmmoWeights.Length; i++)
        {
            cumulativeWeight += howitzerAmmoWeights[i];
            if (randomValue <= cumulativeWeight)
            {
                return (HowitzerAmmoType)i;
            }
        }

        return HowitzerAmmoType.Normal;
    }

    /// <summary>
    /// 가중치 기반 랜덤 드론 패턴 결정
    /// </summary>
    private DronePattern SelectRandomDronePattern()
    {
        if (dronePatternWeights == null || dronePatternWeights.Length == 0)
            return DronePattern.LaserBomber;

        float totalWeight = 0f;
        foreach (float weight in dronePatternWeights)
        {
            totalWeight += weight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        for (int i = 0; i < dronePatternWeights.Length; i++)
        {
            cumulativeWeight += dronePatternWeights[i];
            if (randomValue <= cumulativeWeight)
            {
                return (DronePattern)i;
            }
        }

        return DronePattern.LaserBomber;
    }

    /// <summary>
    /// 공격 패턴 실행
    /// </summary>
    /// <param name="useRepeatGuard">false 면 동일 패턴 연속 대체를 건너뛴다 (v2 선택기가 반복 방지를 맡을 때, T38).</param>
    public IEnumerator ExecutePattern(AttackPattern pattern, BossController controller, bool useRepeatGuard = true)
    {
        bossController = controller;

        // 보조 대기 패턴(Wait3Sec)이 아닐 때만 동일 패턴 연속 실행 검사 실행
        if (useRepeatGuard && pattern != AttackPattern.Wait3Sec && bossController != null)
        {
            if (pattern == lastExecutedPattern)
            {
                consecutivePatternCount++;
                if (consecutivePatternCount >= maxConsecutiveSamePattern)
                {
                    AttackPattern replacementPattern = GetRandomPatternForPhase(bossController.GetCurrentPhase(), pattern);
                    Debug.Log($"[Boss] 패턴 '{pattern}'이 {consecutivePatternCount}회 연속 도달하여 랜덤 대체 패턴 '{replacementPattern}'(으)로 전환 실행합니다.");
                    pattern = replacementPattern;
                    
                    lastExecutedPattern = pattern;
                    consecutivePatternCount = 1;
                }
            }
            else
            {
                lastExecutedPattern = pattern;
                consecutivePatternCount = 1;
            }
        }

        Debug.Log($"[Boss] 패턴 실행 시작: {pattern}");

        // ── 공통 Animator 파라미터 설정 및 고착 트리거 리셋 ──
        if (animator != null)
        {
            // 이전 패턴들의 잔여 트리거가 오작동을 유발하지 않도록 일괄 리셋
            animator.ResetTrigger("StompAnticipation");
            animator.ResetTrigger("MachinegunAnticipation");
            animator.ResetTrigger("HowitzerAnticipation");
            animator.ResetTrigger("CannonAnticipation");
            animator.ResetTrigger("ChargeAnticipation");
            animator.ResetTrigger("DroneSpawnAnticipation");
            animator.ResetTrigger("stopHowitzer");
            animator.ResetTrigger("StopHowitzer");

            animator.SetInteger("AttackType", (int)pattern);
            animator.SetTrigger("AttackStart");
        }

        switch (pattern)
        {
            case AttackPattern.Stomp:
                yield return StartCoroutine(Stomp());
                break;
            case AttackPattern.MachinegunBurst:
                yield return StartCoroutine(MachinegunBurst());
                break;
            case AttackPattern.MachinegunRapid:
                yield return StartCoroutine(MachinegunRapid());
                break;
            case AttackPattern.Howitzer:
                yield return StartCoroutine(Howitzer());
                break;
            case AttackPattern.Cannon:
                yield return StartCoroutine(Cannon());
                break;
            case AttackPattern.Charge:
                yield return StartCoroutine(Charge());
                break;
            case AttackPattern.DroneSpawn:
                yield return StartCoroutine(DroneSpawn());
                break;
        }

        Debug.Log($"[Boss] 패턴 실행 완료: {pattern}");
    }

    /// <summary>
    /// 현재 보스 페이즈에 따라 연속 패턴을 방지할 랜덤 대체 공격 패턴을 반환합니다.
    /// </summary>
    private AttackPattern GetRandomPatternForPhase(int phase, AttackPattern excludePattern)
    {
        List<AttackPattern> available = new List<AttackPattern>();

        // 1페이즈 기본 패턴 후보군
        available.Add(AttackPattern.Stomp);
        available.Add(AttackPattern.MachinegunBurst);
        available.Add(AttackPattern.MachinegunRapid);
        available.Add(AttackPattern.Howitzer);
        available.Add(AttackPattern.Charge);

        // 2페이즈 전용 패턴 후보군 추가
        if (phase >= 2)
        {
            available.Add(AttackPattern.Cannon);
            available.Add(AttackPattern.DroneSpawn);
        }

        // 연속 사용된 패턴은 랜덤 후보군에서 차단
        available.Remove(excludePattern);

        // 만약을 대비한 예외 안전장치
        if (available.Count == 0)
        {
            return AttackPattern.Stomp;
        }

        int randomIndex = UnityEngine.Random.Range(0, available.Count);
        return available[randomIndex];
    }

    /// <summary>
    /// 1. 밟기 (Stomp)
    /// </summary>
    private IEnumerator Stomp()
    {
        Debug.Log("[Boss] 밟기 패턴 시작");
        _stompImpactCalled = false;

        // 전조 애니메이션 재생
        if (animator != null)
        {
            animator.SetTrigger("StompAnticipation");
        }

        // 클립 길이 자동 조회 (폴백: 인스펙터 stompAnticipationTime)
        float anticipationDur = GetClipLength(animator, "Boss_Stomp_Anticipation");
        if (anticipationDur <= 0f) anticipationDur = stompAnticipationTime;
        yield return new WaitForSeconds(anticipationDur);

        // 수직 내리찍기 공격
        if (animator != null)
        {
            animator.SetTrigger("StompAttack");
        }

        // 클립 길이 자동 조회 (폴백: 인스펙터 잔여 시간)
        float attackDur = GetClipLength(animator, "Boss_Stomp_Attack");
        if (attackDur <= 0f) attackDur = Mathf.Max(0f, stompCastTime - stompAnticipationTime);
        yield return new WaitForSeconds(attackDur);

        // 폴백: Animation Event(OnStompImpact)가 발동하지 않은 경우 직접 호출
        if (!_stompImpactCalled)
            OnStompImpact();
    }

    /// <summary>
    /// 2. 기관총 3연발 (MachinegunBurst)
    /// </summary>
    private IEnumerator MachinegunBurst()
    {
        Debug.Log("[Boss] 기관총 3연발 패턴 시작");
        // 장전 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("MachinegunAnticipation");
        }

        // 0.2초 간격으로 3발 발사
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(machinegunBurstInterval);
            FireBullet();
        }

        yield return new WaitForSeconds(0.1f); // 여유 시간
    }

    /// <summary>
    /// 3. 기관총 연사 (MachinegunRapid)
    /// </summary>
    private IEnumerator MachinegunRapid()
    {
        Debug.Log("[Boss] 기관총 연사 패턴 시작");
        // 장전 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("MachinegunAnticipation");
        }

        // 0.05초 간격으로 0.5초 동안 연사
        float elapsedTime = 0f;
        while (elapsedTime < machinegunRapidDuration)
        {
            FireBullet();
            yield return new WaitForSeconds(machinegunRapidInterval);
            elapsedTime += machinegunRapidInterval;
        }

        yield return new WaitForSeconds(0.1f); // 여유 시간
    }

    /// <summary>
    /// 총알 발사
    /// </summary>
    private void FireBullet()
    {
        if (bossController == null || player == null)
            return;

        // 배열에서 랜덤 발사 위치 선택
        Vector3 firePosition = GetRandomFirePoint(
            bulletFirePoints,
            transform.position + Vector3.right * 1.5f
        );

        // 총알 오브젝트 풀에서 가져오기
        GameObject bullet = bossController.GetBulletFromPool();
        if (bullet == null)
            return;
        bullet.transform.position = firePosition;

        // 발사 폭발 이펙트 생성
        if (launchExplosionEffect != null)
        {
            GameObject fx = Instantiate(launchExplosionEffect, firePosition, Quaternion.identity);
            fx.SetActive(true);
            Destroy(fx, 1f);
        }

        // 발사 사운드 재생
        if (audioSource != null && machinegunFireSound != null)
        {
            audioSource.PlayOneShot(machinegunFireSound);
        }


        // 플레이어 방향 계산
        Vector3 direction = (player.position - firePosition).normalized;

        // 총알 스크립트 설정
        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            // Initialize()에서 direction.x 기준으로 스케일이 올바르게 설정됨.
            // SetFacingToPlayer()는 player.position 기준으로 덮어쓰므로,
            // 플레이어가 천장·벽에 있을 때 direction과 부호가 달라져 역방향 버그가 발생함 → 제거.
            projectile.Initialize(direction, machinegunDamage, bossController);
        }
    }




    /// <summary>
    /// 4. 곡사포 (Howitzer) [1페이즈] - 5단계 사양
    /// Step 1: 발사 전조 및 발사 (0.3초 간격 3연발)
    /// Step 2: 포탄 수직 상승
    /// Step 3: 경고 마커 표시
    /// Step 4: 포탄 낙하 (순서대로 돌진)
    /// Step 5: 착탄 및 폭발
    /// </summary>
    private IEnumerator Howitzer()
    {
        Debug.Log("[Boss] 곡사포 패턴 시작");
        if (player == null)
        {
            Debug.LogError("[Boss] Howitzer: player 참조가 누락되었습니다. 패턴을 시작할 수 없습니다.");
            yield break;
        }
        
        // 등뒤 곡사포 장전 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("HowitzerAnticipation");
        }

        // Step 1: 발사 전조 및 발사 (0.3초 간격 3연발)
        List<GameObject> cannonballs = new List<GameObject>();
        List<HowitzerAmmoType> ammoTypes = new List<HowitzerAmmoType>();

        for (int i = 0; i < howitzerShotCount; i++)
        {
            yield return new WaitForSeconds(howitzerBurstInterval);

            // 발사 위치: 각 포탄마다 cannonballFirePoints 배열에서 랜덤 선택
            Vector3 firePosition = GetRandomFirePoint(
                cannonballFirePoints,
                transform.position - Vector3.right * 1f
            );

            // 탄종 결정 (각 포탄마다 독립적으로 결정)
            HowitzerAmmoType ammoType = SelectRandomAmmoType();
            ammoTypes.Add(ammoType);

            GameObject cannonball = bossController.GetCannonballFromPool();
            if (cannonball != null)
            {
                cannonball.transform.position = firePosition;
                cannonballs.Add(cannonball);

                // 발사 폭발 이펙트 생성
                if (launchExplosionEffect != null)
                {
                    GameObject fx = Instantiate(launchExplosionEffect, firePosition, Quaternion.identity);
                    fx.SetActive(true);
                    Destroy(fx, 1f);
                }

                // 발사 사운드 재생
                if (audioSource != null && howitzerFireSound != null)
                {
                    audioSource.PlayOneShot(howitzerFireSound);
                }



                // HowitzerCannonball 컴포넌트 확인
                HowitzerCannonball howitzerCannonball = cannonball.GetComponent<HowitzerCannonball>();
                if (howitzerCannonball != null)
                {
                    // BossController 참조 설정
                    howitzerCannonball.SetBossController(bossController);

                    // 탄종 정보 설정
                    howitzerCannonball.SetAmmoType(ammoType);

                    // AirBurst/BounceBlast 관련 파라미터 설정
                    if (ammoType == HowitzerAmmoType.AirBurst)
                    {
                        howitzerCannonball.SetAirBurstParameters(
                            airBurstHeight,
                            airBurstPelletCount,
                            airBurstPelletSpeed,
                            airBurstPelletRange,
                            airBurstDamage,
                            airBurstPelletPrefab
                        );
                    }
                    else if (ammoType == HowitzerAmmoType.BounceBlast)
                    {
                        howitzerCannonball.SetBounceBlastParameters(
                            bounceCount,
                            bounceMinAngle,
                            bounceMaxAngle,
                            bounceSpeed,
                            bounceBlastRange,
                            bounceBlastDamage,
                            bouncePelletPrefab
                        );
                    }

                    // 수직 상승 시작
                    Vector3 targetHeight = transform.position + Vector3.up * howitzerRiseHeight;
                    howitzerCannonball.StartRising(targetHeight, howitzerRiseSpeed);

                    // 발사 시 1회 플레이어 방향으로 좌우 전환 (이후 고정)
                    howitzerCannonball.SetFacingToPlayer(player);
                }
                else
                {
                    // HowitzerCannonball 컴포넌트가 없으면 기본 동작
                    BossProjectile projectile = cannonball.GetComponent<BossProjectile>();
                    if (projectile != null)
                    {
                        Vector3 direction = player != null ? (player.position - firePosition).normalized : Vector3.down;
                        projectile.InitializeCannonball(direction, howitzerDamage, howitzerExplosionRange, bossController);
                    }
                }
            }
        }

        // Step 2: 포탄 수직 상승 중인 상태에서 바로 다음 단계 진행

        // Step 3: 경고 마커 표시
        List<GameObject> warningMarkers = new List<GameObject>();
        List<Vector3> targetPositions = new List<Vector3>();
        
        for (int i = 0; i < howitzerShotCount; i++)
        {
            // 플레이어 근처 발판 찾기
            GameObject[] platforms = GameObject.FindGameObjectsWithTag("Platform");
            GameObject nearestPlatform = null;
            float nearestDistance = float.MaxValue;

            if (player != null)
            {
                foreach (var platform in platforms)
                {
                    float distance = Vector3.Distance(platform.transform.position, player.position);
                    if (distance < nearestDistance && distance < (howitzerWarningRadius + 3f))
                    {
                        nearestDistance = distance;
                        nearestPlatform = platform;
                    }
                }
            }

            // 플레이어 또는 발판 위치 기준 (Null 방어)
            Vector3 basePos = transform.position;
            if (nearestPlatform != null)
            {
                basePos = nearestPlatform.transform.position;
            }
            else if (player != null)
            {
                basePos = player.position;
            }

            // X축 랜덤 좌표 선정 (+3 범위 증가 적용)
            float randomX = UnityEngine.Random.Range(-(howitzerWarningRadius + 3f), howitzerWarningRadius + 3f);
            
            // Y 좌표는 발판 높이가 아닌 플레이어 높이 기준으로 강제 고정
            float targetY = player != null ? player.position.y : basePos.y;
            Vector3 targetPos = new Vector3(basePos.x + randomX, targetY, 0f);
            targetPositions.Add(targetPos);
            
            // 경고 마커 활성화
            GameObject marker = bossController.GetWarningMarkerFromPool();
            if (marker != null)
            {
                marker.transform.position = targetPos;
                warningMarkers.Add(marker);
            }
        }

        // 경고 표시 지속 시간 대기
        yield return new WaitForSeconds(howitzerWarningDuration);

        // Step 4: 포탄 낙하 (순서대로 돌진)
        Debug.Log($"[Boss] Howitzer 포탄 낙하 시작 (포탄 수: {cannonballs.Count})");
        for (int i = 0; i < cannonballs.Count && i < targetPositions.Count; i++)
        {
            GameObject cannonball = cannonballs[i];
            Vector3 targetPos = targetPositions[i];
            
            HowitzerCannonball howitzerCannonball = cannonball.GetComponent<HowitzerCannonball>();
            if (howitzerCannonball != null)
            {
                // 낙하 시작
                howitzerCannonball.StartFalling(targetPos, howitzerFallSpeed, howitzerDamage, howitzerExplosionRange);
            }
            
            // 이전 포탄 착탄 대기
            yield return new WaitForSeconds(0.5f);
        }

        // Step 5: 경고 마커 비활성화
        foreach (var marker in warningMarkers)
        {
            if (marker != null)
            {
                bossController.ReturnWarningMarkerToPool(marker);
            }
        }

        // 모든 포탄 착탄 대기
        yield return new WaitForSeconds(1f);
        Debug.Log("[Boss] Howitzer 모든 포탄 낙하 완료, stopHowitzer 트리거 작동");

        // 포격 자세 종료 애니메이션 트리거 (대소문자 둘 다 대비)
        // 코루틴 내에서 포탄 투하가 다 끝나면 자동으로 트리거를 호출하여 
        // 애니메이터가 루프 자세를 풀고 Idle로 돌아가게 합니다.
        if (animator != null)
        {
            animator.SetTrigger("stopHowitzer");
            animator.SetTrigger("StopHowitzer");
        }
    }

    /// <summary>
    /// 5. 대포 (Cannon) [2페이즈 전용]
    /// </summary>
    private IEnumerator Cannon()
    {
        Debug.Log("[Boss] 대포 패턴 시작");
        // 머리 대포 장전 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("CannonAnticipation");
        }

        yield return new WaitForSeconds(0.5f);

        // 배열에서 랜덤 발사 위치 선택
        Vector3 firePosition = GetRandomFirePoint(
            cannonballFirePoints,
            transform.position + Vector3.up * 2f
        );
        FireCannonball(firePosition, cannonDamage, cannonRange);

        yield return new WaitForSeconds(cannonCastTime - 0.5f);
    }

    /// <summary>
    /// 대포탄 발사 - bulletPool 사용 (BossProjectile 기반 포물선 탄도)
    /// </summary>
    private void FireCannonball(Vector3 firePosition, float damage, float range)
    {
        if (bossController == null || player == null)
            return;

        // 총알 풀에서 꺼내기 (BossProjectile 컴포넌트 보장)
        GameObject cannonball = bossController.GetBulletFromPool();
        if (cannonball == null)
            return;

        cannonball.transform.position = firePosition;

        // 발사 폭발 이펙트 생성
        if (launchExplosionEffect != null)
        {
            GameObject fx = Instantiate(launchExplosionEffect, firePosition, Quaternion.identity);
            fx.SetActive(true);
            Destroy(fx, 1f);
        }

        // 발사 사운드 재생
        if (audioSource != null && howitzerFireSound != null)
        {
            audioSource.PlayOneShot(howitzerFireSound);
        }



        // 플레이어 방향 계산
        Vector3 direction = (player.position - firePosition).normalized;

        // BossProjectile로 포물선 탄도 발사
        BossProjectile projectile = cannonball.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            projectile.InitializeCannonball(direction, damage, range, bossController);
        }
        else
        {
            // BossProjectile 없으면 즉시 반환
            bossController.ReturnBulletToPool(cannonball);
            Debug.LogWarning("[Boss] Cannon: BossProjectile 컴포넌트 없음 - 발사 실패");
        }
    }

    /// <summary>
    /// 6. 돌진 (Charge) - 벽 충돌까지 돌진 + 스턴
    /// </summary>
    private IEnumerator Charge()
    {
        Debug.Log("[Boss] 돌진 패턴 시작");
        // 등뒤 부스터 시동 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("ChargeAnticipation");
        }

        yield return new WaitForSeconds(0.3f);

        // 플레이어 방향 계산 (X축 부호만 사용)
        // normalize()를 쓰면 플레이어가 위에 있을 때 direction.x 가 0에 수렴해
        // 부동소수점 오차로 부호가 뒤집혀 반대 방향으로 돌진하는 버그가 발생함.
        if (player == null)
            yield break;

        float chargeDirectionX = player.position.x >= transform.position.x ? 1f : -1f;
        float chargeSpeed = bossController != null ? bossController.GetBoosterJumpSpeed() : 10f;

        // 돌진 방향으로 스프라이트 고정 (FacePlayer() 억제)
        if (bossController != null)
            bossController.LockFacing(chargeDirectionX);

        // 돌진 시작 (최대 3초 또는 벽 충돌까지)
        bool isCharging = true;
        bool hasHitWall = false;
        _chargeHitWall = false;  // 돌진 시작 시 플래그 초기화
        float chargeStartTime = Time.time;
        const float MAX_CHARGE_TIME = 3f;
        Vector3 startPosition = transform.position;

        // 돌진 이펙트 생성 및 부착 (보스를 따라가도록 부모 지정)
        GameObject chargeFX = null;
        if (chargeEffectPrefab != null && chargeEffectSpawnPoint != null)
        {
            chargeFX = Instantiate(chargeEffectPrefab, chargeEffectSpawnPoint.position, Quaternion.identity, chargeEffectSpawnPoint);
            chargeFX.SetActive(true);
        }

        // 보스 주변을 감싸는 추가 이펙트 생성 및 부착
        GameObject chargeSurroundFX = null;
        if (chargeSurroundEffectPrefab != null)
        {
            // 빈 오브젝트(SpawnPoint)가 지정되어 있으면 그 위치와 각도에 180도 회전을 더하고, 없으면 보스 위치 + 180도 회전 기준
            if (chargeSurroundEffectSpawnPoint != null)
            {
                chargeSurroundFX = Instantiate(
                    chargeSurroundEffectPrefab, 
                    chargeSurroundEffectSpawnPoint.position, 
                    chargeSurroundEffectSpawnPoint.rotation * Quaternion.Euler(0f, 0f, 180f), 
                    chargeSurroundEffectSpawnPoint
                );
            }
            else
            {
                chargeSurroundFX = Instantiate(
                    chargeSurroundEffectPrefab, 
                    transform.position, 
                    Quaternion.Euler(0f, 0f, 180f), 
                    transform
                );
            }
            chargeSurroundFX.SetActive(true);
        }

        // 돌진 부스터 루프 사운드 재생
        if (audioSource != null && chargeSound != null)
        {
            audioSource.clip = chargeSound;
            audioSource.loop = true;
            audioSource.Play();
        }



        while (isCharging && !hasHitWall)
        {
            // 타임아웃 체크
            if (Time.time - chargeStartTime > MAX_CHARGE_TIME)
            {
                Debug.LogWarning("[Boss] 돌진 타임아웃 - 강제 종료");
                break;
            }

            // 최대 거리 체크 (15m 초과 시 종료)
            if (Vector3.Distance(transform.position, startPosition) > 15f)
            {
                Debug.LogWarning("[Boss] 돌진 최대 거리 도달 - 강제 종료");
                break;
            }

            // 이동
            if (rb2d != null)
            {
                rb2d.linearVelocity = new Vector2(chargeDirectionX * chargeSpeed, rb2d.linearVelocity.y);
            }

            // 판정 박스 활성화 (OnChargeImpactCheck로 위임)
            OnChargeImpactCheck();

            // 벽 충돌 감지 방법 1: OnTriggerEnter2D 플래그 (isTrigger 환경)
            if (_chargeHitWall)
            {
                hasHitWall = true;
                break;
            }

            // 벽 충돌 감지 방법 2: Raycast (레이어 무관, tag 기준)
            Vector2 rayOrigin    = (Vector2)transform.position + new Vector2(chargeDirectionX * 0.5f, 0f);
            Vector2 rayDirection = new Vector2(chargeDirectionX, 0f);
            float rayDistance = 1.5f;

            RaycastHit2D[] wallHits = Physics2D.RaycastAll(rayOrigin, rayDirection, rayDistance);
            foreach (RaycastHit2D hit in wallHits)
            {
                if (hit.collider != null && hit.collider.CompareTag("Wall"))
                {
                    hasHitWall = true;
                    break;
                }
            }
            if (hasHitWall) break;

            yield return null;
        }

        // 돌진 종료 후 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 돌진 사운드 정지
        if (audioSource != null && audioSource.clip == chargeSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }


        // 돌진 이펙트 소멸 처리
        if (chargeFX != null)
        {
            Destroy(chargeFX, 0.5f); // 스턴 상태나 정지 시 자연스럽게 사라지도록 0.5초 딜레이 소멸
        }

        if (chargeSurroundFX != null)
        {
            Destroy(chargeSurroundFX, 0.5f);
        }

        // 벽 충돌 시 스턴 처리

        if (hasHitWall)
        {
            Debug.Log("[Boss] 돌진 벽 충돌 - 스턴 시작");
            ChargeHitWall?.Invoke(); // v2 그로기 +25 (T61)

            // 충돌 이펙트 (필요시 추가)
            // 스턴 애니메이션 트리거
            if (animator != null)
            {
                animator.SetTrigger("ChargeStun");
            }

            // 1초 동안 스턴
            yield return new WaitForSeconds(chargeStunDuration);

            Debug.Log("[Boss] 돌진 스턴 해제");
        }

        // 돌진 종료 — facing 잠금 해제 (이후 FacePlayer()가 플레이어 방향으로 복귀)
        if (bossController != null)
            bossController.UnlockFacing();
    }

    /// <summary>
    /// isTrigger 환경에서 "Wall" 태그 오브젝트 충돌 시 돌진 중단 플래그 세팅
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Wall"))
        {
            _chargeHitWall = true;
        }
    }

    /// <summary>
    /// 플레이어 참조 설정
    /// </summary>
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>
    /// 7. 드론 소환 (DroneSpawn) [2페이즈 전용]
    /// 활성 드론이 1마리 이상 있으면 소환을 건너뛰고 기관총 연사로 대체.
    /// Step 1: 소환 전조
    /// Step 2: 드론 상승
    /// Step 3: 등장 완료 및 패턴 실행
    /// </summary>
    private IEnumerator DroneSpawn()
    {
        Debug.Log("[Boss] 드론 소환 패턴 시작");

        // 드론이 이미 1마리 이상 활성화 중이면 기관총 연사로 대체
        if (bossController.GetActiveDroneCount() >= 1)
        {
            Debug.Log("[Boss] 드론 이미 활성 → 기관총 연사로 대체");
            yield return StartCoroutine(MachinegunRapid());
            yield break;
        }

        // Step 1: 소환 전조
        if (animator != null)
        {
            animator.SetTrigger("DroneSpawnAnticipation");
        }

        yield return new WaitForSeconds(droneSpawnAnticipationTime);

        Transform spawnOrigin = bossController.GetDroneSpawnOrigin();

        if (spawnOrigin != null)
        {
            // 지렁이 드론 소환 (기존 BossDrone)
            DronePattern selectedPattern = SelectRandomDronePattern();

            GameObject drone = bossController.GetDroneFromPool();
            if (drone != null)
            {
                // 보스의 현재 위치에서 드론 생성 (보스에게서 출발)
                drone.transform.position = transform.position;
                bossController.RegisterDrone(drone);

                // 지렁이 드론의 머리(ConductorDrone) 컴포넌트가 있는지 확인
                ConductorDrone conductor = drone.GetComponent<ConductorDrone>();
                if (conductor != null)
                {
                    // 빈 오브젝트(spawnOrigin)의 위치값을 중심으로 무한대를 도는 centerPosition 설정
                    conductor.Initialize(bossController.GetPlayer(), bossController, spawnOrigin.position);
                    Debug.Log("[Boss] 지렁이 지휘자 드론 소환 완료 (보스 위치에서 생성, 빈 오브젝트 기준 무한대 궤도)");
                }
                else
                {
                    // 일반 드론(BossDrone)인 경우 기존과 동일하게 초기화
                    BossDrone droneScript = drone.GetComponent<BossDrone>();
                    if (droneScript != null)
                    {
                        droneScript.Initialize(bossController.GetPlayer(), bossController, selectedPattern, GetDronePatternParameters(selectedPattern));
                        Debug.Log($"[Boss] 일반 드론 소환 완료: 패턴={selectedPattern}");
                    }
                }
            }
        }

        // 드론 상승 대기
        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// 드론 패턴별 파라미터 반환
    /// </summary>
    private BossDrone.DronePatternParameters GetDronePatternParameters(DronePattern pattern)
    {
        BossDrone.DronePatternParameters parameters = new BossDrone.DronePatternParameters();

        switch (pattern)
        {
            case DronePattern.LaserBomber:
                parameters.count = dronePatternA_Count;
                parameters.duration1 = droneAimDuration;
                parameters.duration2 = droneLaserDuration;
                parameters.damage = droneLaserDamage;
                break;
            case DronePattern.GroundTracker:
                parameters.count = dronePatternB_Count;
                parameters.duration1 = droneTrackDuration;
                parameters.duration2 = droneStrikeDuration;
                parameters.damage = droneStrikeDamage;
                break;
            case DronePattern.StealthSniper:
                parameters.count = dronePatternC_Count;
                parameters.duration1 = droneActiveDuration;
                parameters.duration2 = droneInactiveDuration;
                parameters.damage = droneSnipeDamage;
                parameters.interval = droneSnipeInterval;
                parameters.maxCycles = dronePatternC_MaxCycles;
                break;
        }

        return parameters;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Animation Event 공개 콜백 메서드
    // 애니메이션 클립의 이벤트 트랙(Animation Event)에서 함수명으로 직접 호출
    // 또는 코루틴 내부 폴백으로도 호출됨
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// [Animation Event] 밟기 충돌 판정
    /// Boss_Stomp_Attack 클립에서 내리찍는 프레임에 Animation Event로 연결
    /// _stompImpactCalled 플래그로 코루틴 폴백과 중복 호출 방지
    /// </summary>
    public void OnStompImpact()
    {
        _stompImpactCalled = true;

        // 짓밟기 사운드 재생
        if (audioSource != null && stompSound != null)
        {
            audioSource.PlayOneShot(stompSound);
        }

        // 카메라 흔들기 (지진 효과)
        CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.ShakeCamera(0.4f, 0.3f);
        }


        // 짓밟기 충격 이펙트 생성
        if (stompEffectPrefab != null)
        {
            Vector3 spawnPos = stompHitBox != null ? stompHitBox.position : transform.position;
            GameObject stompFX = Instantiate(stompEffectPrefab, spawnPos, Quaternion.identity);
            stompFX.SetActive(true);
            Destroy(stompFX, 2f);
        }

        if (stompHitBox == null) return;

        Collider2D[] hitTargets = Physics2D.OverlapBoxAll(
            stompHitBox.position,
            stompHitBox.localScale,
            0f, playerLayer
        );

        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<IDamageable>()?.TakeDamage(stompDamage);
            }
        }
        Debug.Log("[Boss] OnStompImpact: 밟기 판정 실행 (사운드 및 이펙트 생성 완료)");
    }


    /// <summary>
    /// [Animation Event] 기관총 단발 발사
    /// MachinegunBurst/Rapid 클립의 발사 프레임에 Animation Event로 연결 (선택)
    /// 코루틴의 for/while 루프 대신 이벤트 주도 발사가 필요할 때 사용
    /// </summary>
    public void OnMachinegunFire()
    {
        FireBullet();
    }

    /// <summary>
    /// [Animation Event / 외부 스크립트 호출] 곡사포 포격 자세 종료 신호
    /// 애니메이션의 특정 지점이나 Behavior Tree 등에서 이 메서드를 호출하면
    /// 곡사포 포격 후 대기 상태를 풀고 종료 애니메이션 트리거(stopHowitzer)를 발동합니다.
    /// </summary>
    public void TriggerStopHowitzer()
    {
        _howitzerStopped = true;
        Debug.Log("[Boss] TriggerStopHowitzer: 곡사포 자세 유지 종료 요청 수신");
    }

    /// [Animation Event] 곡사포 발사 훅
    /// Boss_Howitzer 클립의 발사 프레임에 Animation Event로 연결 (선택)
    /// 기존 Howitzer() 코루틴 타이밍은 유지되며, 추가 이펙트/사운드 트리거 용도
    /// _howitzerLaunchPending 을 true로 설정 → Howitzer 코루틴에서 폴링 가능
    /// </summary>
    public void OnHowitzerLaunch()
    {
        _howitzerLaunchPending = true;
        Debug.Log("[Boss] OnHowitzerLaunch: 곡사포 발사 이벤트 수신");
    }

    /// <summary>
    /// [Animation Event / 코루틴 내부] 돌진 충돌 판정
    /// Charge() 코루틴 내 while 루프에서 매 프레임 호출
    /// 또는 Boss_Charge_Anticipation 클립에 Animation Event로 연결 가능
    /// </summary>
    public void OnChargeImpactCheck()
    {
        if (chargeHitBox == null) return;

        Collider2D[] hitTargets = Physics2D.OverlapBoxAll(
            chargeHitBox.position,
            chargeHitBox.localScale,
            0f
        );

        foreach (Collider2D target in hitTargets)
        {
            if (target.CompareTag("Player"))
            {
                target.GetComponent<IDamageable>()?.TakeDamage(chargeDamage);
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 클립 길이 자동 조회 유틸리티
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// Animator Controller에서 clipName과 일치하는 AnimationClip의 길이(초)를 반환
    /// 클립을 찾지 못하면 0f 반환 + Debug.LogWarning → 인스펙터 폴백 값 사용 권장
    /// </summary>
    /// <param name="anim">대상 Animator</param>
    /// <param name="clipName">조회할 클립 이름 (확장자 제외)</param>
    private float GetClipLength(Animator anim, string clipName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[Boss] GetClipLength: Animator 또는 Controller가 null. clipName={clipName}");
            return 0f;
        }

        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        Debug.LogWarning($"[Boss] GetClipLength: '{clipName}' 클립을 찾지 못했습니다. 인스펙터 폴백 값을 사용합니다.");
        return 0f;
    }
}
