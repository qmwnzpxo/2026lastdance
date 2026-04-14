using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class NewEnemyCombat : MonoBehaviour
{
    protected NewEnemyBase enemyBase;


    [Header("전투")]
    public float attackDamage = 1.0f;
    public float attackRange = 1.0f;
    public float attackCooldown = 1.0f;
    [SerializeField] protected Transform attackTransform;
    [SerializeField] protected Vector2 attackBoxSize = new Vector2(0.8f, 0.8f);
    [SerializeField] protected float attackDelay = 0.2f;
    [SerializeField] protected LayerMask playerLayer;

    [Header("발사 오브젝트")]
    public GameObject bullet;
    protected Transform toAttackTransform;
    
    protected virtual void Awake()
    {
        enemyBase = GetComponent<NewEnemyBase>();

        if (playerLayer == 0)
            playerLayer = LayerMask.GetMask("player");
    }

    /// <summary>
    /// 공격 1사이클 : 딜레이 → 판정 → 쿨다운.
    /// ct 취소 시 즉시 반환 (경직 처리는 EnemyBase 에서).
    /// </summary>
    public virtual async UniTask Attack(CancellationToken ct)
    {
        // 공격 선딜
        bool canceled = await UniTask
            .WaitForSeconds(attackDelay, cancellationToken: ct)
            .SuppressCancellationThrow();

        if (!canceled)
            ExecuteAttackBox();

        // 공격 쿨다운
        await UniTask
            .WaitForSeconds(attackCooldown, cancellationToken: ct)
            .SuppressCancellationThrow();
        enemyBase.isAttacking = false;
    }

    protected virtual void ExecuteAttackBox()
    {
        if (attackTransform == null) return;

        Collider2D hit = Physics2D.OverlapBox(
            attackTransform.position, attackBoxSize, 0f, playerLayer);

        if(hit!=null)
        {
            Debug.Log($"[{name}] 공격 적중: {hit.name}");
            //hit.GetComponent<PlayerState>()?.TakeDamage(attackDamage);
            Shoot(hit.transform);
        }

        Debug.DrawRay(attackTransform.position,
            attackTransform.right * attackRange, Color.magenta, 0.5f);
    }

    protected virtual void Shoot(Transform Pos)
    {
        if(bullet==null) return;


    }


    public virtual void TakeDamage(float damage)
    {
        enemyBase?.OnDamage(damage);
    }

    public virtual void OnDestroy()
    {
        StopAllCoroutines();
    }

}


#region 04/10 이전 코드
/*
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
        if (distance > Mathf.Abs(enemyBase.attackRange))
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

}
*/
#endregion

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

