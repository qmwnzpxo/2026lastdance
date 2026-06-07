using System.Collections.Generic;

namespace BossBT
{
    /// <summary>
    /// Behavior Tree 복합 노드 기반 클래스
    /// 자식 노드를 여러 개 가질 수 있는 노드
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        protected List<BTNode> children;

        public BTComposite(List<BTNode> children)
        {
            this.children = children;
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var child in children)
            {
                child.Reset();
            }
        }
    }

    /// <summary>
    /// Selector 노드 - 자식 노드 중 하나라도 Success/Running이면 해당 값 반환
    /// 자식 노드를 순서대로 평가하여 첫 번째로 Success 또는 Running을 반환하는 자식에서 즉시 반환
    /// 모든 자식이 Failure를 반환하면 Failure 반환
    /// </summary>
    public class Selector : BTComposite
    {
        public Selector(List<BTNode> children) : base(children) { }

        public override NodeStatus Evaluate()
        {
            foreach (var child in children)
            {
                NodeStatus childStatus = child.Evaluate();
                if (childStatus == NodeStatus.Success || childStatus == NodeStatus.Running)
                {
                    return childStatus;
                }
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Sequence 노드 - 자식 노드가 모두 Success여야 Success 반환
    /// 자식 노드를 순서대로 평가하여 첫 번째로 Failure 또는 Running을 반환하는 자식에서 즉시 반환
    /// 모든 자식이 Success를 반환해야 Success 반환
    /// </summary>
    public class Sequence : BTComposite
    {
        public Sequence(List<BTNode> children) : base(children) { }

        public override NodeStatus Evaluate()
        {
            foreach (var child in children)
            {
                NodeStatus childStatus = child.Evaluate();
                if (childStatus == NodeStatus.Failure || childStatus == NodeStatus.Running)
                {
                    return childStatus;
                }
            }
            return NodeStatus.Success;
        }
    }
}
