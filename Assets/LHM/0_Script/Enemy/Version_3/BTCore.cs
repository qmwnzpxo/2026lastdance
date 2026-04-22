using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public enum NodeState { Success, Failure, Running }

public abstract class BTNode
{
    public abstract NodeState Tick(EnemyBlackboard bb);
}

public class BTSelector : BTNode
{
    private BTNode[] _children;
    public BTSelector(params BTNode[] children) => _children = children;

    public override NodeState Tick(EnemyBlackboard bb)
    {
        foreach (var child in _children)
        {
            var state = child.Tick(bb);
            if (state != NodeState.Failure) return state;
        }
        return NodeState.Failure;
    }
}

public class BTSequence : BTNode
{
    private BTNode[] _children;
    public BTSequence(params BTNode[] children) => _children = children;

    public override NodeState Tick(EnemyBlackboard bb)
    {
        foreach (var child in _children)
        {
            var state = child.Tick(bb);
            if (state != NodeState.Success) return state;
        }
        return NodeState.Success;
    }
}

public abstract class BTCondition : BTNode { }

// 비동기 액션 베이스
// 상태는 Blackboard의 RunningActionId / ActionCompleted 로만 관리
public abstract class BTAsyncAction : BTNode
{
    protected abstract string ActionId { get; }

    public override NodeState Tick(EnemyBlackboard bb)
    {
        // 이 액션이 이미 실행 중
        if (bb.runningActionId == ActionId)
        {
            if (!bb.actionCompleted)
                return NodeState.Running;

            // 완료 처리
            var result = bb.lastActionResult;
            bb.runningActionId = null;
            bb.actionCompleted = false;
            return result;
        }

        // 다른 액션이 실행 중이면 취소 후 새로 시작
        bb.CancelAction();

        bb.actionCts = new CancellationTokenSource();
        bb.runningActionId = ActionId;
        bb.actionCompleted = false;
        bb.lastActionResult = NodeState.Failure;

        ExecuteAsync(bb, bb.actionCts.Token).Forget();
        return NodeState.Running;
    }

    protected abstract UniTaskVoid ExecuteAsync(
        EnemyBlackboard bb, CancellationToken ct);
}