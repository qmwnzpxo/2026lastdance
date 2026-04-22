//using Cysharp.Threading.Tasks;
//using System.Threading;
//using UnityEngine;

//public class NewRangerEnemyCombat : EnemyCombat
//{
//    [EditorOnly, SerializeField] protected Transform attackPoint;
//    LineRenderer line;

//    protected override void Start()
//    {
//        base.Start();
//        attackDelay = 0.5f;
//        line = attackPoint.GetComponent<LineRenderer>();
//    }
//    protected override async UniTask PerformAttack()
//    {
//        RotateAttackPivot();
//        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
//        Vector2 attackPos = (Vector2)attackTransform.position
//            + (Vector2)attackTransform.right * rangeCheck(distanceAfterWait);
//        await UniTask.WaitForSeconds(attackDelay, cancellationToken: this.GetCancellationTokenOnDestroy());

//        ExecuteAttackBox(attackPos);

//        spawnLine(attackPos).Forget();
        
//        //Debug.DrawRay(attackTransform.position, attackTransform.right * distanceAfterWait, Color.magenta, 0.5f);
//        Debug.DrawRay(attackTransform.position, attackTransform.right * distanceAfterWait, Color.magenta, 0.5f);

//    }

//    protected override async UniTaskVoid AttackCooldown(CancellationToken ct)
//    {
//        base.AttackCooldown(ct).Forget();

//        enemyBase.isAttacking = true;

//        await UniTask.WaitForSeconds(attackDelay*2,
//            cancellationToken: this.GetCancellationTokenOnDestroy());

//        enemyBase.isAttacking = false;

//    }

//    protected virtual async UniTask spawnLine(Vector2 attackPos)
//    {
//        line.enabled = true;
//        line.SetPosition(0, transform.position);
//        line.SetPosition(1, attackPos);
//        attackPoint.position = attackPos;

//        await UniTask.WaitForSeconds(attackDelay, cancellationToken: this.GetCancellationTokenOnDestroy());
//        line.enabled = false;
//    }

//}
