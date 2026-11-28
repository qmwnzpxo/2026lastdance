using UnityEngine;
using EnemyVersion3;

[RequireComponent(typeof(EnemyArmAim))]
public class  TurretEnemyBTController : EnemyBTController
{
    protected override BTNode BuildTree()
    {
        return new BTSelector(
            // 우선순위 1: 스턴
            new BTSequence(
                new ConditionIsStunned(),
                new ActionStun()
            ),
            // 우선순위 2: 공격
            new BTSequence(
                new ConditionIsChasing(),
                new ConditionInAttackRange(),
                new ConditionHasLOS(),
                new ConditionAttackCooldownReady(),
                new ActionAttack()
            ),
            // 추격 분기 제거: 포탑은 IMoveable이 없어 ActionChase가 아무것도 하지 않음
            // (예전엔 Running으로 남아 대기 역할을 대신했고, 지금은 바로 Failure를 돌려줌)
            /*new BTSequence(
                new ConditionIsChasing(),
                new ActionChase()
            ),*/
            // 우선순위 3: 대기
            new ActionIdle()
        );
    }

    protected override void InitBlackboad()
    {
        base.InitBlackboad();

        GetComponent<EnemyCombat>()?.Initialize(_bb.playerTransform);
    }
    // ────────────────────────────────────────
    #region Gizmos

    // 미사용 (부모와 같은 동작 / 쓰이지 않는 색)
    /*Color retreatdisColor = new Color(0f, 1f, 0.7f, 1f);
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
    }*/

    #endregion
}