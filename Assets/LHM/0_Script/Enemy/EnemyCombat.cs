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

        attack_start_cts = new CancellationTokenSource();
    }

    public virtual void Attack()
    {
        if(enemyBase.isAttacking||enemyBase.isDead)
        {
            return;
        }

        //StartCoroutine(AttackCooldown());
        AttackCooldown(attack_start_cts.Token).Forget();

    }

    #region 코루틴
    /*
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
    */
    #endregion


    #region 유니태스크

    protected virtual async UniTaskVoid AttackCooldown(CancellationToken ct)
    {
        enemyBase.isAttacking = true;

            float distance = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
        while (enemyBase.isAttacking)
        {
            if (distance <= Mathf.Abs(enemyBase.attackRange))
            {
                Debug.Log(distance);
                await PerformAttack();
            }

            await UniTask.WaitForSeconds(enemyBase.attackCooldown,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            if(distance > Mathf.Abs(enemyBase.attackRange))
            {
                enemyBase.isAttacking = false;
                break;
            }
            await UniTask.WaitForFixedUpdate(cancellationToken: this.destroyCancellationToken).
                SuppressCancellationThrow();
        }
    }

    protected virtual async UniTaskVoid DestroyAfterDeath()
    {
        await UniTask.WaitForSeconds(1f);
        Destroy(gameObject);
    }

    #endregion

    protected virtual async UniTask PerformAttack()
    {

        float signedDistX = enemyBase.playerTransform.position.x - transform.position.x;
        Vector2 attackPos = (Vector2)attackTransform.position
            + (Vector2)attackTransform.right * rangeCheck(signedDistX);
        Vector2 debugStart = attackTransform.position;

        await UniTask.WaitForSeconds(attackDelay,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        ExecuteAttackBox(attackPos);
        Debug.DrawRay(debugStart, attackTransform.right * signedDistX, Color.magenta, 0.5f);
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
    protected virtual Vector3 RotateAttackPivot()
    {

        Vector3 direction;
        if (attackTransform != null) {
            Vector2 dir = (enemyBase.playerTransform.position - attackTransform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            attackTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            direction = new Vector3(0, 0, angle);
        }
        else
        {
            Vector2 dir = (enemyBase.playerTransform.position - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            direction = new Vector3(0, 0, angle);
        }
        return direction;
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

        //StartCoroutine(DestoryAfterDeath());
        DestroyAfterDeath().Forget();
    }

    public virtual void OnDestroy()
    {
        StopAllCoroutines();
        //attack_start_cts?.Cancel();
        //attack_start_cts?.Dispose();
    }
}
