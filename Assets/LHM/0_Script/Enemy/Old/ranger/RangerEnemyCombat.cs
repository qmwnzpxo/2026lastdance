//using Cysharp.Threading.Tasks;
//using UnityEngine;

//public class RangerEnemyCombat : EnemyCombat
//{
//    protected override void Start()
//    {
//        base.Start();
//        attackDelay = 0.5f;
//    }
//    protected override async UniTask PerformAttack()
//    {
//        RotateAttackPivot();
//        float distanceAfterWait = Vector2.Distance(transform.position, enemyBase.playerTransform.position);
//        Vector2 attackPos = (Vector2)attackTransform.position
//            + (Vector2)attackTransform.right * rangeCheck(distanceAfterWait);
//        await UniTask.WaitForSeconds(attackDelay);

//        ExecuteAttackBox(attackPos);
        
//        //Debug.DrawRay(attackTransform.position, attackTransform.right * distanceAfterWait, Color.magenta, 0.5f);
//        Debug.DrawRay(attackTransform.position, attackTransform.right * distanceAfterWait, Color.magenta, 0.5f);
//    }

//}
