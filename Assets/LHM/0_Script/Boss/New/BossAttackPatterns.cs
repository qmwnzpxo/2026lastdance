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
    [SerializeField] private float howitzerWarningRadius = 6f;   // 경고 마커 배치 반경
    [SerializeField] private float howitzerWarningDuration = 1f;  // 경고 표시 지속 시간
    [SerializeField] private float howitzerExplosionRange = 3f;   // 착탄 폭발 범위
    [SerializeField] private float howitzerBurstInterval = 0.3f; // 포탄 발사 간격
    [SerializeField] private int howitzerShotCount = 3;          // 발사 포탄 수

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

    [Header("곡사포 파편 프리팹")]
    [SerializeField] private GameObject airBurstPelletPrefab;   // AirBurst 파편 프리팹
    [SerializeField] private GameObject bouncePelletPrefab;     // BounceBlast 파편 프리팹

    private void Awake()
    {
        if (bossController == null)
            bossController = GetComponent<BossController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (rb2d == null)
            rb2d = GetComponent<Rigidbody2D>();

        if(playerLayer == 0)
            playerLayer = LayerMask.GetMask("player");
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
    public IEnumerator ExecutePattern(AttackPattern pattern, BossController controller)
    {
        bossController = controller;
        Debug.Log($"[Boss] 패턴 실행 시작: {pattern}");

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
    /// 1. 밟기 (Stomp)
    /// </summary>
    private IEnumerator Stomp()
    {
        Debug.Log("[Boss] 밟기 패턴 시작");
        // 전조 애니메이션 재생
        if (animator != null)
        {
            animator.SetTrigger("StompAnticipation");
        }
        yield return new WaitForSeconds(stompAnticipationTime);

        // 수직 내리찍기 공격
        if (animator != null)
        {
            animator.SetTrigger("StompAttack");
        }

        // 판정 박스 활성화
        if (stompHitBox != null)
        {
            Collider2D[] hitTargets = Physics2D.OverlapBoxAll(
                stompHitBox.position,
                stompHitBox.localScale,
                0f, playerLayer
            );

            foreach (Collider2D target in hitTargets)
            {
                if (target.CompareTag("Player"))
                {
                    // 플레이어에게 데미지 적용
                    target.GetComponent<IDamageable>()?.TakeDamage(stompDamage);
                }
            }
        }

        yield return new WaitForSeconds(stompCastTime - stompAnticipationTime);
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

        // 총알 오브젝트 풀에서 가져오기
        GameObject bullet = bossController.GetBulletFromPool();
        if (bullet == null)
            return;

        // 발사 위치 설정 (오른팔)
        Vector3 firePosition = transform.position + Vector3.right * 1.5f;
        bullet.transform.position = firePosition;

        // 플레이어 방향 계산
        Vector3 direction = (player.position - firePosition).normalized;

        // 총알 스크립트 설정
        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile != null)
        {
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
        
        // 등뒤 곡사포 장전 전조 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("HowitzerAnticipation");
        }

        // Step 1: 발사 전조 및 발사 (0.3초 간격 3연발)
        List<GameObject> cannonballs = new List<GameObject>();
        List<HowitzerAmmoType> ammoTypes = new List<HowitzerAmmoType>();
        Vector3 firePosition = transform.position - Vector3.right * 1f;

        for (int i = 0; i < howitzerShotCount; i++)
        {
            yield return new WaitForSeconds(howitzerBurstInterval);

            // 탄종 결정 (각 포탄마다 독립적으로 결정)
            HowitzerAmmoType ammoType = SelectRandomAmmoType();
            ammoTypes.Add(ammoType);

            GameObject cannonball = bossController.GetCannonballFromPool();
            if (cannonball != null)
            {
                cannonball.transform.position = firePosition;
                cannonballs.Add(cannonball);

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
                }
                else
                {
                    // HowitzerCannonball 컴포넌트가 없으면 기본 동작
                    BossProjectile projectile = cannonball.GetComponent<BossProjectile>();
                    if (projectile != null)
                    {
                        Vector3 direction = (player.position - firePosition).normalized;
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

            foreach (var platform in platforms)
            {
                float distance = Vector3.Distance(platform.transform.position, player.position);
                if (distance < nearestDistance && distance < howitzerWarningRadius)
                {
                    nearestDistance = distance;
                    nearestPlatform = platform;
                }
            }

            // 발판이 있으면 발판 위치 기준, 없으면 플레이어 위치 기준
            Vector3 basePos = nearestPlatform != null ? nearestPlatform.transform.position : player.position;

            // X축 랜덤 좌표 선정
            float randomX = UnityEngine.Random.Range(-howitzerWarningRadius, howitzerWarningRadius);
            Vector3 targetPos = basePos + new Vector3(randomX, 0f, 0f);
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

        // 머리에서 발사
        Vector3 firePosition = transform.position + Vector3.up * 2f;
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

        // 플레이어 방향 계산
        if (player == null)
            yield break;

        Vector3 direction = (player.position - transform.position).normalized;
        float chargeSpeed = bossController != null ? bossController.GetBoosterJumpSpeed() : 10f;

        // 돌진 시작 (최대 3초 또는 벽 충돌까지)
        bool isCharging = true;
        bool hasHitWall = false;
        float chargeStartTime = Time.time;
        const float MAX_CHARGE_TIME = 3f;
        Vector3 startPosition = transform.position;

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
                rb2d.linearVelocity = new Vector2(direction.x * chargeSpeed, rb2d.linearVelocity.y);
            }

            // 판정 박스 활성화
            if (chargeHitBox != null)
            {
                Collider2D[] hitTargets = Physics2D.OverlapBoxAll(
                    chargeHitBox.position,
                    chargeHitBox.localScale,
                    0f
                );

                foreach (Collider2D target in hitTargets)
                {
                    if (target.CompareTag("Player"))
                    {
                        // 플레이어에게 데미지 적용
                        target.GetComponent<PlayerHealth>()?.TakeDamage(chargeDamage);
                    }
                }
            }

            // 벽 충돌 감지 (Raycast)
            Vector2 rayOrigin = transform.position;
            Vector2 rayDirection = new Vector2(direction.x, 0f);
            float rayDistance = 1f;

            RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, rayDirection, rayDistance, LayerMask.GetMask("Wall"));
            if (wallHit.collider != null)
            {
                hasHitWall = true;
                break;
            }

            yield return null;
        }

        // 돌진 종료 후 속도 정지
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        // 벽 충돌 시 스턴 처리
        if (hasHitWall)
        {
            Debug.Log("[Boss] 돌진 벽 충돌 - 스턴 시작");

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
    /// Step 1: 소환 전조
    /// Step 2: 드론 상승
    /// Step 3: 등장 완료 및 패턴 실행
    /// </summary>
    private IEnumerator DroneSpawn()
    {
        Debug.Log("[Boss] 드론 소환 패턴 시작");

        // Step 1: 소환 전조
        if (animator != null)
        {
            animator.SetTrigger("DroneSpawnAnticipation");
        }

        yield return new WaitForSeconds(droneSpawnAnticipationTime);

        Transform spawnOrigin = bossController.GetDroneSpawnOrigin();

        if (spawnOrigin != null)
        {
            // 50% 확률로 불도저 드론 또는 지렁이 드론 선택
            bool spawnBulldozer = (Random.value < 0.5f) && (bossController.GetBulldozerDronePrefab() != null);

            if (spawnBulldozer)
            {
                // 불도저 드론 소환
                GameObject drone = bossController.GetBulldozerDroneFromPool();
                if (drone != null)
                {
                    drone.transform.position = spawnOrigin.position;
                    bossController.RegisterDrone(drone);

                    BulldozerDrone bulldozer = drone.GetComponent<BulldozerDrone>();
                    if (bulldozer != null)
                    {
                        bulldozer.Initialize(bossController.GetPlayer(), bossController);
                        Debug.Log("[Boss] 불도저 드론 소환 완료");
                    }
                }
            }
            else
            {
                // 지렁이 드론 소환 (기존 BossDrone)
                DronePattern selectedPattern = SelectRandomDronePattern();

                GameObject drone = bossController.GetDroneFromPool();
                if (drone != null)
                {
                    drone.transform.position = spawnOrigin.position;
                    bossController.RegisterDrone(drone);

                    BossDrone droneScript = drone.GetComponent<BossDrone>();
                    if (droneScript != null)
                    {
                        droneScript.Initialize(bossController.GetPlayer(), bossController, selectedPattern, GetDronePatternParameters(selectedPattern));
                        Debug.Log($"[Boss] 지렁이 드론 소환 완료: 패턴={selectedPattern}");
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
}
