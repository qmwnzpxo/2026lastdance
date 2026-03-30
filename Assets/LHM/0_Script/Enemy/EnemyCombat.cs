using System.Collections;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    EnemyBase enemyBase;
    EnemyMovement enemyMovement;
    PlayerMove playerMove;
    private void Start()
    {
        enemyBase = GetComponent<EnemyBase>();
        enemyMovement = GetComponent<EnemyMovement>();
    }

    public void Attack()
    {
        if(enemyBase.isAttacking)
        {
            return;
        }

        StartCoroutine(AttackCooldown());
    }
    IEnumerator AttackCooldown()
    {
        enemyBase.isAttacking = true;

        float distance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distance <= enemyBase.attackRange)
        {
           // enemyBase.playerTransform.GetComponent<PlayerMove>().TakeDamage(enemyBase.attackDamage);
        }

        yield return new WaitForSeconds(enemyBase.attackCooldown);

        enemyBase.isAttacking = false;

        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distanceAfterWait <= enemyBase.detectionRange)
        {
            enemyMovement.StartChase();
        }
    }
    public void TakeDamage(float damage)
    {
        if(enemyBase.isDead)
        {
            return;
        }

        enemyBase.currentHp -= damage;
        
        if(enemyBase.currentHp <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        enemyBase.isDead = true;
        enemyMovement.StopAllMove();

        StartCoroutine(DestoryAfterDeath());
    }
    IEnumerator DestoryAfterDeath()
    {
        yield return new WaitForSeconds(1);
        Destroy(gameObject);
    }
}
