//using Cysharp.Threading.Tasks;
//using System;
//using System.Threading;
//using UnityEngine;
//public enum EnemyState
//{
//    Patrol,
//    Chase,
//    Attack
//}
//public class NewEnemyBase : MonoBehaviour, IDamageable
//{
//    [Header("컴포넌트")]
//    public Rigidbody2D rigid2d;
//    [ SerializeField] protected NewEnemyMovement enemyMovement;
//    [ SerializeField] protected NewEnemyCombat enemyCombat;
//    public Transform playerTransform;
//    [ SerializeField] protected Animator animator;

//    #region 박스형 탐지범위
//    /*
//    [Header("탐지")]
//    public float detectionRangeX = 10f;
//    public float detectionRangeY = 3f;
//    */
//    #endregion

//    [Header("탐지 - 부채꼴")]
//    public float detectionRange = 8f;       // 부채꼴 반지름
//    [Range(0f, 360f)]
//    public float detectionAngle = 60f;     // 시야각 (전체 각도)

//    [Header("체력")]
//    public float maxHp = 10f;
//    public float currentHp;

//    [Header("경직")]
//    public float staggerDuration = 0.3f;

//    // ── 상태 플래그 ──────────────────────────
//    public bool isDead;
//    public bool isAttacking;
//    public bool isStaggered;
//    public bool isPlayerDetected;

//    // ── CTS ─────────────────────────────────
//    // 현재 행동(순찰 / 추적 / 공격)을 중단할 토큰
//    protected CancellationTokenSource actionCts;
//    // 메인 루프 자체를 종료할 토큰
//    protected CancellationTokenSource loopCts;

//    //애니메이션 파라미터
//    string ANIM_WALK;
//    string ANIM_CHASE;
//    string ANIM_DASH;
//    string ANIM_ATTACK;
//    string ANIM_HIT;
//    string ANIM_DEAD;


//    protected virtual void Awake()
//    {
//        rigid2d = GetComponent<Rigidbody2D>();
//        animator = GetComponent<Animator>();
//        enemyCombat = GetComponent<NewEnemyCombat>();
//        enemyMovement = GetComponent<NewEnemyMovement>();
//        playerTransform = GameObject.FindWithTag("Player").transform;

//        currentHp = maxHp;
//    }
//    protected virtual void Start()
//    {
//        loopCts = new CancellationTokenSource();
//        RunLoop(loopCts.Token).Forget();
//    }


//    protected virtual async UniTaskVoid RunLoop(CancellationToken loopToken)
//    {
//        while(!loopToken.IsCancellationRequested && !isDead)
//        {
//            actionCts?.Dispose();
//            actionCts = new CancellationTokenSource();

//            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(loopToken, actionCts.Token);

//            CancellationToken actionToken = linkedCts.Token;

//            try
//            {
//                var state = DecideState();

//                switch (state)
//                {
//                    case EnemyState.Attack:
//                        isAttacking = true;
//                        isPlayerDetected = true;
//                        await OnAttackState(actionToken);
//                        break;

//                    case EnemyState.Chase:
//                        isPlayerDetected = true;
//                        await OnChaseState(actionToken);
//                        break;

//                    case EnemyState.Patrol:
//                        isPlayerDetected = false;
//                        await OnPatrolState(actionToken);
//                        break;
//                }
//                #region #if - else 버전 (DecideState 로 리팩토링하면서 주석 처리)
//                /*
//                if (isPlayerInAttackRange())
//                {
//                    isAttacking = true;
//                    await OnAttackState(actionToken);
//                    isAttacking = false;
//                }
//                else if (IsPlayerInRange())
//                {
//                    isPlayerDetected = true;

//                    await OnChaseState(actionToken);
//                }
//                else
//                {
//                    isPlayerDetected = false;

//                    await OnPatrolState(actionToken);
//                }*/
//                #endregion
//            }
//            //catch (OperationCanceledException)
//            catch(Exception)
//            {
//                // 피격 or 루프 종료로 인한 취소 → 정상 흐름
//                enemyMovement.Stop();
//            }

//            // 경직 중이라면 경직이 끝날 때까지 대기
//            if (isStaggered && !loopToken.IsCancellationRequested)
//                await WaitForStaggerEnd(loopToken);

//            // 다음 판단 전 한 프레임 양보
//            await UniTask.WaitForFixedUpdate(loopToken).SuppressCancellationThrow();
//        }
//    }

//    // ── 상태별 행동 (서브클래스에서 override 가능) ──────

//    /// <summary>순찰 상태 : 플레이어 미탐지 시 pointA ↔ pointB 왕복</summary>
//    protected virtual async UniTask OnPatrolState(CancellationToken ct)
//    {
//        if (enemyMovement != null)
//            await enemyMovement.Patrol(ct);
//        else
//            await UniTask.WaitUntilCanceled(ct); // 이동 컴포넌트 없으면 대기
//    }

//    /// <summary>추적 상태 : 플레이어 탐지 시 접근</summary>
//    protected virtual async UniTask OnChaseState(CancellationToken ct)
//    {
//        if (enemyMovement != null)
//            await enemyMovement.Chase(ct);
//        else
//            await UniTask.WaitUntilCanceled(ct);
//    }

//    /// <summary>공격 상태 : 플레이어 공격 사거리 내 접근시 공격</summary>
//    protected virtual async UniTask OnAttackState(CancellationToken ct)
//    {
//        if (enemyCombat != null)
//            await enemyCombat.Attack(ct);
//        else
//            await UniTask.WaitUntilCanceled(ct);
//    }

//    protected virtual EnemyState DecideState()
//    {
//        if (isPlayerInAttackRange()) return EnemyState.Attack;
//        if (IsPlayerInRange()) return EnemyState.Chase;
//        return EnemyState.Patrol;
//    }

//    public virtual void TakeDamage(float damage)
//    {
//        if (isDead) return;

//        currentHp -= damage;

//        if (currentHp <= 0f)
//        {
//            Die();
//            return;
//        }

//        // 현재 행동(순찰 / 추적 / 공격) 즉시 취소 → 경직 진입
//        EnterStagger();
//    }

//    protected virtual void EnterStagger()
//    {
//        isStaggered = true;
//        isAttacking = false;
//        enemyMovement.Stop();

//        // 현재 행동 취소 (루프가 catch 에서 받아 StopMovement 후 WaitForStaggerEnd 로 이동)
//        actionCts?.Cancel();
//    }

//    /// <summary>경직 시간 동안 대기. loopToken 으로만 외부 종료 감지.</summary>
//    protected virtual async UniTask WaitForStaggerEnd(CancellationToken loopToken)
//    {
//        await UniTask.WaitForSeconds(staggerDuration, cancellationToken: loopToken)
//                     .SuppressCancellationThrow();
//        isStaggered = false;
//    }

//    protected virtual void Die()
//    {
//        isDead = true;
//        enemyMovement.Stop();
//        loopCts?.Cancel();

//        gameObject.SetActive(false);
//    }


//    public virtual bool IsPlayerInRange()
//    {
//        if (playerTransform == null) return false;
//        Vector2 toPlayer = playerTransform.position - transform.position;
//        // 1. 거리 체크
//        if (toPlayer.magnitude > detectionRange) return false;

//        // 2. 현재 바라보는 방향 (스프라이트 플립 기준)
//        float facingDir = transform.localScale.x >= 0f ? 1f : -1f;
//        Vector2 facingVec = new Vector2(facingDir, 0f);

//        // 3. 각도 체크
//        float angle = Vector2.Angle(facingVec, toPlayer);
//        return angle <= detectionAngle * 0.5f;

//        #region 일반 원 범위(04/14)
//        /*
//        Vector2 diff = playerTransform.position - transform.position;
//        return Mathf.Abs(diff.x) <= detectionRangeX
//            && Mathf.Abs(diff.y) <= detectionRangeY;
//        */
//        #endregion
//    }

//    public virtual bool isPlayerInAttackRange()
//    {
//        if (playerTransform == null) return false;
//        float distance = Vector2.Distance(transform.position, playerTransform.position);
//        return distance <= enemyCombat.attackRange;
//    }

//    protected virtual void OnDrawGizmos()
//    {
//        if (isDead) return;
        
        
//        /*
//        // 탐지 박스
//        Gizmos.color = Color.yellow;
//        Gizmos.DrawWireCube(transform.position,
//            new Vector3(detectionRangeX * 2f, detectionRangeY * 2f, 0f));
//        */

//        Gizmos.color = Color.yellow;
//        float facingDir = transform.localScale.x >= 0f ? 1f : -1f;
//        float baseAngle = facingDir > 0f ? 0f : 180f;
//        float halfAngle = detectionAngle * 0.5f;
//        //int segments = 20;

//        Vector3 prevPoint = transform.position +
//            (Vector3)(Quaternion.Euler(0, 0, baseAngle - halfAngle) * Vector2.right) * detectionRange;

//        //// 원호
//        //for (int i = 0; i <= segments; i++)
//        //{
//        //    float t = (float)i / segments;
//        //    float currentAngle = (baseAngle - halfAngle) + detectionAngle * t;
//        //    Vector3 point = transform.position +
//        //        (Vector3)(Quaternion.Euler(0, 0, currentAngle) * Vector2.right) * detectionRange;
//        //    Gizmos.DrawLine(prevPoint, point);
//        //    prevPoint = point;
//        //}

//        // 양쪽 변
//        Vector3 leftEdge = transform.position +
//            (Vector3)(Quaternion.Euler(0, 0, baseAngle - halfAngle) * Vector2.right) * detectionRange;
//        Vector3 rightEdge = transform.position +
//            (Vector3)(Quaternion.Euler(0, 0, baseAngle + halfAngle) * Vector2.right) * detectionRange;
//        Gizmos.DrawLine(transform.position, leftEdge);
//        Gizmos.DrawLine(transform.position, rightEdge);

//        // 대시 범위
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, detectionRange);

//        // 공격 범위
//        Gizmos.color = Color.blue;
//        if(enemyCombat != null)
//            Gizmos.DrawWireSphere(transform.position, enemyCombat.attackRange);
//    }

//    public void CancelAction()
//    {
//        isAttacking = false;
//        isPlayerDetected = false;
//        actionCts?.Cancel();
//        actionCts?.Dispose();
//        actionCts = new CancellationTokenSource();
//    }

//    protected virtual void OnDestroy()
//    {
//        loopCts?.Cancel();
//        loopCts?.Dispose();
//        actionCts?.Cancel();
//        actionCts?.Dispose();
//    }
//}