using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BTConditions
{
    public class ConditionNeedsRetreat : BTCondition
    {
        public override NodeState Tick(EnemyBlackboard bb)
            => bb.needsRetreat ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionIsChasing : BTCondition
    {
        public override NodeState Tick(EnemyBlackboard bb)
            => bb.isDetected ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionInAttackRange : BTCondition
    {
        public override NodeState Tick(EnemyBlackboard bb)
            => bb.DistanceToPlayer <= bb.attackRange
                ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionAttackCooldownReady : BTCondition
    {
        public override NodeState Tick(EnemyBlackboard bb)
            => bb.AttackCooldownReady ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionHasMoveable : BTCondition
    {
        public override NodeState Tick(EnemyBlackboard bb)
            => bb.moveable != null ? NodeState.Success : NodeState.Failure;
    }
}