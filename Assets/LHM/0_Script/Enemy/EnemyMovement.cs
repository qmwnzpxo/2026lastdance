using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    EnemyBase enemyBase;
    EnemyCombat enemyCombat;
    bool isMovingToB;
    Coroutine currentMoveCoroutine;//읽는용



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

        float distancePlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        bool detected = distancePlayer <= enemyBase.detectionRange;

        if (detected == enemyBase.isPlayerDetected) return;

        enemyBase.isPlayerDetected = detected;

        if (currentMoveCoroutine != null)
            StopCoroutine(currentMoveCoroutine);

        currentMoveCoroutine = StartCoroutine(detected ? EnemyChaseMove() : EnemyNormalMove());
    }

    //public void DetectPlayer()
    //{
    //    if (enemyBase == null || enemyBase.isDead) return;
    //    if (enemyBase.playerTransform == null) return;

    //    float distancePlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
    //    if (distancePlayer <= enemyBase.detectionRange)
    //    {
    //        enemyBase.isPlayerDetected = true;

    //        if (currentMoveCoroutine != null)
    //            StopCoroutine(currentMoveCoroutine);

    //        currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
    //    }
    //    else
    //    {
    //        enemyBase.isPlayerDetected = false;

    //        if (currentMoveCoroutine != null)
    //            StopCoroutine(currentMoveCoroutine);

    //        currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
    //    }
    //}
    IEnumerator EnemyNormalMove()
    {
        while (true)
        {
            Vector2 target = isMovingToB ? enemyBase.pointB.position : enemyBase.pointA.position;

            transform.position = Vector2.MoveTowards(transform.position, target, enemyBase.moveSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, target) < 0.1f)
            {
                isMovingToB = !isMovingToB;
            }

            yield return null;
        }
    }



    public IEnumerator EnemyChaseMove()
    {
        while (true)
        {
             float chasedistance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);

            if(chasedistance < enemyBase.dashRange)
            {
                StartCoroutine(EnemyDash());
                yield break;
            }
            transform.position = Vector2.MoveTowards(transform.position, enemyBase.playerTransform.position, enemyBase.moveSpeed * Time.deltaTime);

           yield return null;
        }
       
    }



    IEnumerator EnemyDash()
    {
        Vector2 dashDirection = (enemyBase.playerTransform.position - transform.position).normalized;

        float timer = 0f;

        while (timer < enemyBase.dashDuration)
        {
            enemyBase.rigid2d.linearVelocity = dashDirection * enemyBase.dashSpeed;
            timer += Time.deltaTime;
            yield return null;
        }


        enemyBase.rigid2d.linearVelocity = Vector2.zero;

        float distanceToPlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);

        if (distanceToPlayer <= enemyBase.attackRange)
        {
            enemyCombat.Attack();
        }
        else
        {
            StartCoroutine(EnemyChaseMove());
        }
    }

    public void FlipSprite()
    {
        if (enemyBase.rigid2d.linearVelocity.x != 0)
        {
            float direction = (enemyBase.rigid2d.linearVelocity.x > 0) ? 1f : -1f;
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction,
                                               transform.localScale.y,
                                               transform.localScale.z);
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
            enemyBase.rigid2d.linearVelocity = Vector2.zero;
        }

    }
}