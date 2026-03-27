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
        EnemyNormalMove();
        currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
    }



    public void DetectPlayer()
    {
        if(enemyBase.isDead)
        {
            return;
        }

        float distancePlayer = Vector2.Distance(transform.position, enemyBase.playerTransform.position);


        // 거리가 detectionRange 이내이면
        if (distancePlayer > 0)
        {
            enemyBase.isPlayerDetected = true;
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = StartCoroutine(EnemyChaseMove());
        }
        else
        {
            enemyBase.isPlayerDetected= false;
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = StartCoroutine(EnemyNormalMove());
        }
        
    }


    IEnumerator EnemyNormalMove()

    {
        while(true)
        {
            if(isMovingToB = true)//pointb
            {
                Vector2.MoveTowards(transform.position, enemyBase.pointB.position,enemyBase.moveSpeed * Time.deltaTime);
            }
            else if(Vector2.Distance(transform.position, enemyBase.pointB.position)< 0.1f)
            {
                isMovingToB = false;
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
            enemyBase.rigid2d.velocity = dashDirection * enemyBase.dashSpeed;
            timer += Time.deltaTime;
            yield return null;
        }


        enemyBase.rigid2d.velocity = Vector2.zero;

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
        if (enemyBase.rigid2d.velocity.x != 0)
        {
            float direction = (enemyBase.rigid2d.velocity.x > 0) ? 1f : -1f;
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
            enemyBase.rigid2d.velocity = Vector2.zero;
        }

    }
}