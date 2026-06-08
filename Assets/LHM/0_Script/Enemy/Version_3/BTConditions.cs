using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace EnemyVersion3
{
    public class ConditionNeedsRetreat : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.moveable != null && bb.moveable.needsRetreat 
            ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionIsChasing : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.isDetected ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionInAttackRange : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.DistanceToPlayer <= bb.attackRange
                ? NodeState.Success : NodeState.Failure;
    }
    public class ConditionHasLOS : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.hasLOS ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionAttackCooldownReady : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.AttackCooldownReady ? NodeState.Success : NodeState.Failure;
    }

    public class ConditionHasMoveable : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.moveable != null ? NodeState.Success : NodeState.Failure;
    }
    
    public class ConditionIsStunned : BTCondition
    {
        public override NodeState Evaluate(EnemyBlackboard bb)
            => bb.isStunned ? NodeState.Success : NodeState.Failure;
    }
}