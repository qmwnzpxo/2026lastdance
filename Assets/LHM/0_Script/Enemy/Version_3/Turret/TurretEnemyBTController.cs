using BTActions;
using BTConditions;
using UnityEngine;

[RequireComponent(typeof(EnemyArmAim))]
public class  TurretEnemyBTController : EnemyBTController
{
    // ── 팔 회전 ─────────────────────────────────
    protected EnemyArmAim _armAim;

    public override void ReInit()
    {
        base.ReInit();
        _armAim = GetComponent<EnemyArmAim>();
        _armAim.EnemyBlackBoard = _bb;
    }

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
            // 우선순위 3: 추격
            new BTSequence(
                new ConditionIsChasing(),
                new ActionChase()
            ),
            // 우선순위 4: 대기
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

    Color retreatdisColor = new Color(0f, 1f, 0.7f, 1f);
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
    }

    #endregion
}