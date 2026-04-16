using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class NewEnemyMovement : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 2.0f;
    public float dashSpeed = 3.0f;
    public float dashDuration = 0.5f;
    public Transform pointA;
    public Transform pointB;


    //public float detectionRangeX => enemyBase != null ? enemyBase.detectionRangeX : 10f;
    //public float detectionRangeY => enemyBase != null ? enemyBase.detectionRangeY : 3f;


    protected NewEnemyBase enemyBase;
    protected NewEnemyCombat enemyCombat;

    protected virtual void Awake()
    {
        enemyBase = GetComponent<NewEnemyBase>();
        enemyCombat = GetComponent<NewEnemyCombat>();
    }

    public virtual async UniTask Patrol(CancellationToken ct)
    {
        if(pointA == null || pointB == null)
        {
            Debug.LogWarning($"[{name}] Patrol: pointA 또는 pointB가 할당되지 않았습니다.");
            await UniTask.WaitUntilCanceled(ct); // 대기 후 종료
            return;
        }

        bool movingToB = false;
        while (!ct.IsCancellationRequested)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }
            if (enemyBase.IsPlayerInRange())
            {
                return;
            }
            Vector2 target= movingToB
                ? pointB.position
                : pointA.position;

            Vector2 dir = target - (Vector2)transform.position;

            if (dir.magnitude < 0.1f)
            {
                movingToB = !movingToB;
                enemyBase.rigid2d.linearVelocity =
                    new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
            }
            else
            {
                enemyBase.rigid2d.linearVelocity = new Vector2(
                    dir.normalized.x * moveSpeed,
                    enemyBase.rigid2d.linearVelocity.y);
            }

            FlipSprite();
            await UniTask.WaitForFixedUpdate(cancellationToken: ct); // 취소 시 예외 발생
        }

        return;
    }

    public virtual async UniTask Chase(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            float dist = Vector2.Distance(transform.position,
                                          enemyBase.playerTransform.position);

            if (dist <= enemyCombat.attackRange)
            {
                // 공격 범위 → Combat 위임
                enemyBase.rigid2d.linearVelocity =
                    new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);

                if (!enemyBase.isAttacking && enemyCombat != null)
                    return;
            }
            else if (dist <= enemyBase.detectionRange)
            {
                // 대시 범위 → 대시
                await Dash(ct);
            }
            else
            {
                if (!enemyBase.IsPlayerInRange())
                {
                    return;
                }

                // 일반 추적 이동
                float dirX = enemyBase.playerTransform.position.x - transform.position.x;
                enemyBase.rigid2d.linearVelocity = new Vector2(
                    Mathf.Sign(dirX) * moveSpeed,
                    enemyBase.rigid2d.linearVelocity.y);
            }

            FlipSprite();
            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
        }
    }

    protected virtual async UniTask Dash(CancellationToken ct)
    {
        float dirX = enemyBase.playerTransform.position.x - transform.position.x;
        float timer = 0f;

        while (timer < dashDuration && !ct.IsCancellationRequested)
        {
            enemyBase.rigid2d.linearVelocity = new Vector2(
                Mathf.Sign(dirX) * dashSpeed,
                enemyBase.rigid2d.linearVelocity.y);

            timer += Time.deltaTime;
            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
        }

        enemyBase.rigid2d.linearVelocity =
            new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
    }


    public void Stop()
    {
        enemyBase.CancelAction();
        if (enemyBase.rigid2d != null)
        {
            enemyBase.rigid2d.linearVelocity = new Vector2(0f,enemyBase.isDead?
                0f: enemyBase.rigid2d.linearVelocity.y);
        }
    }
    public virtual void FlipSprite()
    {
        if (enemyBase.rigid2d.linearVelocity.x == 0f) return;

        float dir = enemyBase.rigid2d.linearVelocity.x > 0f ? 1f : -1f;
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * dir,
            transform.localScale.y,
            transform.localScale.z);
    }
}

#region 04/10 이전 코드
/*
protected EnemyBase enemyBase;
protected EnemyCombat enemyCombat;
protected bool isMovingToB;
//protected Coroutine currentMoveCoroutine;
protected CancellationTokenSource currentMove_tk;

public float detectionRangeX = 10f; // 플레이어 탐지 범위 (X축)
public float detectionRangeY = 3f;  // 플레이어 탐지 범위 (Y축)

public virtual void Init()
{
    enemyBase = GetComponent<EnemyBase>();
    enemyCombat = GetComponent<EnemyCombat>();
    StartMove(EnemyNormalMove);
}

public virtual void DetectPlayer()
{
    if (enemyBase == null || enemyBase.isDead) return;
    if (enemyBase.playerTransform == null) return;

    Vector2 diff = enemyBase.playerTransform.position - transform.position;
    bool detected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;

    if (detected == enemyBase.isPlayerDetected) return;
    enemyBase.isPlayerDetected = detected;

    if (enemyBase.isAttacking) return;

    StartMove(detected ? EnemyChaseMove : EnemyNormalMove);
}

protected virtual void StartMove(Func<CancellationToken, UniTaskVoid> moveTask)
{
    currentMove_tk?.Cancel();
    currentMove_tk?.Dispose();
    currentMove_tk = new CancellationTokenSource();
    if(enemyBase.isDead) return;
    moveTask(currentMove_tk.Token).Forget();
}

protected virtual async UniTaskVoid EnemyNormalMove(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        Vector2 target = isMovingToB ?
            enemyBase.pointB.position : enemyBase.pointA.position;
        Vector2 direction = target - (Vector2)transform.position;

        if (direction.magnitude < 0.1f)
        {
            isMovingToB = !isMovingToB;
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
        }
        else
        {
            enemyBase.rigid2d.linearVelocity = new Vector2(
                direction.normalized.x * enemyBase.moveSpeed,
                enemyBase.rigid2d.linearVelocity.y
            );
        }

        await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();
    }
}

protected virtual async UniTaskVoid EnemyChaseMove(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        if (enemyBase.isDead)
        {
            currentMove_tk?.Cancel();
            currentMove_tk?.Dispose();
            break;
        }
        if (enemyBase.isAttacking)
        {
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
            await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();
            continue;
        }

        Vector2 diff = enemyBase.playerTransform.position - transform.position;
        bool stillDetected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;
        if (!stillDetected)
        {
            enemyBase.isPlayerDetected = false;
            StartMove(EnemyNormalMove);
            return;
        }

        float chaseDistance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (chaseDistance <= Mathf.Abs(enemyBase.dashRange))
        {
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
            await EnemyDash(ct);
            continue;
        }

        float dirX = enemyBase.playerTransform.position.x - transform.position.x;
        enemyBase.rigid2d.linearVelocity = new Vector2(
            Mathf.Sign(dirX) * enemyBase.moveSpeed,
            enemyBase.rigid2d.linearVelocity.y
        );

        await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();
    }
}

public virtual void StartChase()
{
    StartMove(EnemyChaseMove);
}

protected virtual async UniTask EnemyDash(CancellationToken ct)
{
    float dirX = enemyBase.playerTransform.position.x - transform.position.x;
    Vector2 dashDirection = new Vector2(Mathf.Sign(dirX), 0f);

    float timer = 0f;
    while (timer < enemyBase.dashDuration && !ct.IsCancellationRequested)
    {
        enemyBase.rigid2d.linearVelocity = new Vector2(
            dashDirection.x * enemyBase.dashSpeed,
            enemyBase.rigid2d.linearVelocity.y
        );
        timer += Time.deltaTime;
        await UniTask.Yield(cancellationToken: ct).SuppressCancellationThrow();
    }

    enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);

    if (!ct.IsCancellationRequested)
    {
        float distanceToPlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distanceToPlayer <= enemyBase.attackRange)
        {
            enemyCombat.Attack();
        }
    }
}

public virtual void FlipSprite()
{
    if (enemyBase.rigid2d.linearVelocity.x != 0)
    {
        float direction = (enemyBase.rigid2d.linearVelocity.x > 0) ? 1f : -1f;
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * direction,
            transform.localScale.y,
            transform.localScale.z
        );
    }
}

public virtual void StopAllMove()
{
    currentMove_tk?.Cancel();
    currentMove_tk?.Dispose();
    currentMove_tk = null;

    if (enemyBase.rigid2d != null)
        enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
}

private void OnDestroy()
{
    currentMove_tk?.Cancel();
    currentMove_tk?.Dispose();
}
*/
#endregion

#region 코루틴
/*
public virtual void Init()
{
    enemyBase = GetComponent<EnemyBase>();
    enemyCombat = GetComponent<EnemyCombat>();
    currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
}

public virtual void DetectPlayer()
{
    if (enemyBase == null || enemyBase.isDead) return;
    if (enemyBase.playerTransform == null) return;

    Vector2 diff = enemyBase.playerTransform.position - transform.position;
    bool detected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;

    if (detected == enemyBase.isPlayerDetected) return;

    enemyBase.isPlayerDetected = detected;
    if (enemyBase.isAttacking) return; // 공격 중엔 감지 무시

    if (currentMoveCoroutine != null)
        StopCoroutine(currentMoveCoroutine);

    currentMoveCoroutine = StartCoroutine(detected ? EnemyChaseMove() : EnemyNormalMove());
}

protected virtual IEnumerator EnemyNormalMove()
{
    while (true)
    {
        Vector2 target = isMovingToB ?
            enemyBase.pointB.position : enemyBase.pointA.position;

        Vector2 direction = (target - (Vector2)transform.position);

        // 목표 지점 근처에 도달하면 방향 전환
        if (direction.magnitude < 0.1f)
        {
            isMovingToB = !isMovingToB;
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
        }
        else
        {
            // X만 이동, Y(중력)는 유지
            enemyBase.rigid2d.linearVelocity = new Vector2(
                direction.normalized.x * enemyBase.moveSpeed,
                enemyBase.rigid2d.linearVelocity.y
            );
        }

        yield return new WaitForFixedUpdate();
    }
}

public virtual IEnumerator EnemyChaseMove()
{
    while (true)
    {
        float chaseDistance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);

        if (chaseDistance < Mathf.Abs(enemyBase.dashRange))
        {
            // X velocity 정지 후 대시
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
            yield return StartCoroutine(EnemyDash());
            continue;
        }

        // 플레이어 X 방향으로만 추적
        float dirX = enemyBase.playerTransform.position.x - transform.position.x;
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);

        enemyBase.rigid2d.linearVelocity = new Vector2(
            Mathf.Sign(dirX) * enemyBase.moveSpeed,
            enemyBase.rigid2d.linearVelocity.y);

        yield return new WaitForFixedUpdate();
    }
}

public virtual void StartChase()
{
    if (currentMoveCoroutine != null)
        StopCoroutine(currentMoveCoroutine);
    currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
}

protected virtual IEnumerator EnemyDash()
{
    float dirX = enemyBase.playerTransform.position.x - transform.position.x;
    Vector2 dashDirection = new Vector2(Mathf.Sign(dirX), 0f);

    float timer = 0f;
    while (timer < enemyBase.dashDuration)
    {
        enemyBase.rigid2d.linearVelocity = new Vector2(
            dashDirection.x * enemyBase.dashSpeed,
            enemyBase.rigid2d.linearVelocity.y // 중력 유지
        );
        timer += Time.deltaTime;
        yield return null;
    }

    // 대시 종료 후 X 정지
    enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);

    float distanceToPlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
    if (distanceToPlayer <= enemyBase.attackRange)
    {
        enemyCombat.Attack();
    }
    // 공격 범위 밖이면 EnemyChaseMove가 continue로 자동 재개
}

public virtual void FlipSprite()
{
    if (enemyBase.rigid2d.linearVelocity.x != 0)
    {
        float direction = (enemyBase.rigid2d.linearVelocity.x > 0) ? 1f : -1f;
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * direction,
            transform.localScale.y,
            transform.localScale.z
        );
    }
}

public virtual void StopAllMove()
{
    if (currentMoveCoroutine != null)
    {
        StopCoroutine(currentMoveCoroutine);
        currentMoveCoroutine = null;
    }

    if (enemyBase.rigid2d != null)
    {
        // X만 정지, Y(중력)는 유지
        enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
    }
}

*/
#endregion

#region 실패작
//using Cysharp.Threading.Tasks;
//using System.Collections;
//using System.Collections.Generic;
//using Unity.VisualScripting;
//using UnityEngine;

//public class EnemyMovement : MonoBehaviour
//{
//    EnemyBase enemyBase;
//    EnemyCombat enemyCombat;
//    bool isMovingToB;
//    Coroutine currentMoveCoroutine;//읽는용



//    public void Init()
//    {
//        enemyBase = GetComponent<EnemyBase>();
//        enemyCombat = GetComponent<EnemyCombat>();
//        currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
//    }



//    public void DetectPlayer()
//    {
//        if (enemyBase == null || enemyBase.isDead) return;
//        if (enemyBase.playerTransform == null) return;

//        //float distancePlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
//        //bool detected = distancePlayer <= enemyBase.detectionRange;
//        Vector2 diff = enemyBase.playerTransform.position - transform.position;
//        bool detected = Mathf.Abs(diff.x) <= 10f && Mathf.Abs(diff.y) <= 3f;

//        if (detected == enemyBase.isPlayerDetected) return;

//        enemyBase.isPlayerDetected = detected;

//        if (currentMoveCoroutine != null)
//            StopCoroutine(currentMoveCoroutine);

//        currentMoveCoroutine = StartCoroutine(detected ? EnemyChaseMove() : EnemyNormalMove());
//    }

//    //public void DetectPlayer()
//    //{
//    //    if (enemyBase == null || enemyBase.isDead) return;
//    //    if (enemyBase.playerTransform == null) return;

//    //    float distancePlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
//    //    if (distancePlayer <= enemyBase.detectionRange)
//    //    {
//    //        enemyBase.isPlayerDetected = true;

//    //        if (currentMoveCoroutine != null)
//    //            StopCoroutine(currentMoveCoroutine);

//    //        currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
//    //    }
//    //    else
//    //    {
//    //        enemyBase.isPlayerDetected = false;

//    //        if (currentMoveCoroutine != null)
//    //            StopCoroutine(currentMoveCoroutine);

//    //        currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
//    //    }
//    //}
//    IEnumerator EnemyNormalMove()
//    {
//        while (true)
//        {
//            Vector2 target = isMovingToB ?
//                enemyBase.pointB.position : enemyBase.pointA.position;

//            Vector2 newPos = Vector2.MoveTowards(
//                enemyBase.rigid2d.position,
//                target,
//                enemyBase.moveSpeed * Time.fixedDeltaTime
//            );
//            enemyBase.rigid2d.MovePosition(newPos); // ← transform 대신

//            if (Vector2.Distance(enemyBase.rigid2d.position, target) < 0.1f)
//                isMovingToB = !isMovingToB;

//            yield return new WaitForFixedUpdate(); // ← fixedUpdate 타이밍
//        }
//    }




//    public IEnumerator EnemyChaseMove()
//    {
//        while (true)
//        {
//            float chasedistance = Vector2.Distance(
//                enemyBase.rigid2d.position,
//                enemyBase.playerTransform.position
//            );

//            if (chasedistance < enemyBase.dashRange)
//            {
//                yield return StartCoroutine(EnemyDash()); // yield return으로 순서 보장
//                continue;
//            }

//            Vector2 newPos = Vector2.MoveTowards(
//                enemyBase.rigid2d.position,
//                enemyBase.playerTransform.position,
//                enemyBase.moveSpeed * Time.fixedDeltaTime
//            );
//            enemyBase.rigid2d.MovePosition(newPos);

//            yield return new WaitForFixedUpdate();
//        }
//    }

//    public void StartChase()
//    {
//        if (currentMoveCoroutine != null)
//            StopCoroutine(currentMoveCoroutine);
//        currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
//    }

//    IEnumerator EnemyDash()
//    {
//        Vector2 dashDirection = new Vector2(
//         (enemyBase.playerTransform.position.x - transform.position.x),
//         0f
//     ).normalized;
//        float timer = 0f;

//        while (timer < enemyBase.dashDuration)
//        {
//            // Y velocity는 건드리지 않고 X만 설정 (중력 유지)
//            enemyBase.rigid2d.linearVelocity = new Vector2(
//                dashDirection.x * enemyBase.dashSpeed,
//                enemyBase.rigid2d.linearVelocity.y // 기존 Y(중력) 유지
//            );
//            timer += Time.deltaTime;
//            yield return null;
//        }

//         enemyBase.rigid2d.linearVelocity = new Vector2(
//        0f,
//        enemyBase.rigid2d.linearVelocity.y // 여기도 Y 유지
//    );
//        enemyBase.rigid2d.linearVelocity = Vector2.zero;

//        float distanceToPlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);

//        if (distanceToPlayer <= enemyBase.attackRange)
//        {
//            enemyCombat.Attack();
//        }
//        else
//        {
//            StartCoroutine(EnemyChaseMove());
//        }
//    }

//    public void FlipSprite()
//    {
//        if (enemyBase.rigid2d.linearVelocity.x != 0)
//        {
//            float direction = (enemyBase.rigid2d.linearVelocity.x > 0) ? 1f : -1f;
//            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction,
//                                               transform.localScale.y,
//                                               transform.localScale.z);
//        }
//    }



//    public void StopAllMove()
//    {

//        if (currentMoveCoroutine != null)
//        {
//            StopCoroutine(currentMoveCoroutine);
//            currentMoveCoroutine = null;
//        }


//        if (enemyBase.rigid2d != null)
//        {
//            enemyBase.rigid2d.linearVelocity = Vector2.zero;
//        }

//    }
//}
#endregion

