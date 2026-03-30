using System.Collections;

using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    EnemyBase enemyBase;
    EnemyCombat enemyCombat;
    bool isMovingToB;
    Coroutine currentMoveCoroutine;

    public void Init()
    {
        enemyBase = GetComponent<EnemyBase>();
        enemyCombat = GetComponent<EnemyCombat>();
        currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
    }

    public void DetectPlayer()
    {
        if (enemyBase == null || enemyBase.isDead) return;
        if (enemyBase.playerTransform == null) return;
        if (enemyBase.isAttacking) return; // 공격 중엔 감지 무시

        Vector2 diff = enemyBase.playerTransform.position - transform.position;
        bool detected = Mathf.Abs(diff.x) <= 10f && Mathf.Abs(diff.y) <= 3f;

        if (detected == enemyBase.isPlayerDetected) return;

        enemyBase.isPlayerDetected = detected;

        if (currentMoveCoroutine != null)
            StopCoroutine(currentMoveCoroutine);

        currentMoveCoroutine = StartCoroutine(detected ? EnemyChaseMove() : EnemyNormalMove());
    }

    IEnumerator EnemyNormalMove()
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

    public IEnumerator EnemyChaseMove()
    {
        while (true)
        {
            float chaseDistance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);

            if (chaseDistance < enemyBase.dashRange)
            {
                // X velocity 정지 후 대시
                enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);
                yield return StartCoroutine(EnemyDash());
                continue;
            }

            // 플레이어 X 방향으로만 추적
            float dirX = enemyBase.playerTransform.position.x - transform.position.x;
            enemyBase.rigid2d.linearVelocity = new Vector2(
                Mathf.Sign(dirX) * enemyBase.moveSpeed,
                enemyBase.rigid2d.linearVelocity.y
            );

            yield return new WaitForFixedUpdate();
        }
    }

    public void StartChase()
    {
        if (currentMoveCoroutine != null)
            StopCoroutine(currentMoveCoroutine);
        currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
    }

    IEnumerator EnemyDash()
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

    public void FlipSprite()
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

    public void StopAllMove()
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
}
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