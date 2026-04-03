using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Playables;

public class EnemyCombat : MonoBehaviour
{
    protected EnemyBase enemyBase;
    protected EnemyMovement enemyMovement;
    protected PlayerMove playerMove;

    [SerializeField] protected Transform attackTransform;
    [SerializeField] protected Vector2 attackBoxSize = new Vector2(0.8f, 0.8f);
    //[SerializeField] private float attackBoxDistance = 1f;
    [SerializeField] protected float attackDelay = 0.2f;
    [SerializeField] protected LayerMask playerLayer;
    protected CancellationTokenSource attack_start_cts;

    protected virtual void Start()
    {
        enemyBase = GetComponent<EnemyBase>();
        enemyMovement = GetComponent<EnemyMovement>();
        if(playerLayer == 0)
        {
            playerLayer = LayerMask.GetMask("player");
        }
    }

    public virtual void Attack()
    {
        if(enemyBase.isAttacking)
        {
            return;
        }

        StartCoroutine(AttackCooldown());

    }

    #region 코루틴
    
    protected virtual IEnumerator AttackCooldown()
    {
        enemyBase.isAttacking = true;

        float distance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distance <= Mathf.Abs(enemyBase.attackRange))
        {
            // enemyBase.playerTransform.GetComponent<PlayerMove>().TakeDamage(enemyBase.attackDamage);
            Debug.Log(distance);
            PerformAttack().Forget();
        }

        yield return new WaitForSeconds(enemyBase.attackCooldown);

        enemyBase.isAttacking = false;

        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distanceAfterWait <= Mathf.Abs(enemyBase.detectionRange))
        {
            enemyMovement.StartChase();
        }
    }
    
    protected virtual IEnumerator DestoryAfterDeath()
    {
        yield return new WaitForSeconds(1);
        Destroy(gameObject);
    }
    
    #endregion

    #region 유니태스크

    protected virtual async UniTaskVoid AttackCooldown(CancellationToken cancellationToken)
    {
        enemyBase.isAttacking = true;
        float distance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distance <= Mathf.Abs(enemyBase.attackRange))
        {
            Debug.Log(distance);
            PerformAttack().Forget();
        }
        await UniTask.WaitForSeconds(enemyBase.attackCooldown);
        enemyBase.isAttacking = false;
        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        if (distanceAfterWait <= Mathf.Abs(enemyBase.detectionRange))
        {
            enemyMovement.StartChase();
        }
    }
    protected virtual async UniTaskVoid DestroyAfterDeath()
    {
        await UniTask.WaitForSeconds(1f);
        Destroy(gameObject);
    }

    #endregion
    protected virtual async UniTaskVoid PerformAttack()
    {
        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        Vector2 attackPos = (Vector2)attackTransform.position
            + (Vector2)attackTransform.right * rangeCheck(distanceAfterWait);

        await UniTask.WaitForSeconds(attackDelay, cancellationToken: this.GetCancellationTokenOnDestroy());

        ExecuteAttackBox(attackPos);
        Debug.DrawRay(attackTransform.position, attackTransform.right * distanceAfterWait, Color.magenta, 0.5f);
    }

    protected virtual float rangeCheck(float value)
    {
        float maxLrange = -enemyBase.attackRange;
        float maxRrange = enemyBase.attackRange;
        return Mathf.Max(maxLrange, Mathf.Min(maxRrange, value));
    }

    protected virtual void ExecuteAttackBox(Vector2 attackPos)
    {

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPos, attackBoxSize, 0, playerLayer);

        foreach (Collider2D hit in hits)
        {
            Debug.Log($"공격 적중: {hit.name}");
            hit.GetComponent<PlayerState>()?.TakeDamage(enemyBase.attackDamage);
        }
    
    }


    public virtual void TakeDamage(float damage)
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
    protected virtual void Die()
    {
        enemyBase.isDead = true;
        enemyMovement.StopAllMove();

        StartCoroutine(DestoryAfterDeath());
        //DestroyAfterDeath().Forget();
    }

    public virtual void OnDestroy()
    {
        StopAllCoroutines();

    }
}
