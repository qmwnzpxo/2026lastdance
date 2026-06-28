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
        // Running 중인 자식 인덱스 기억 (-1 = 아직 시작 전)
        protected int currentChildIndex = -1;

        public BTComposite(List<BTNode> children)
        {
            this.children = children;
        }

        public override void Reset()
        {
            base.Reset();
            currentChildIndex = -1;
            foreach (var child in children)
            {
                child.Reset();
            }
        }
    }

    /// <summary>
    /// Selector 노드 - 자식 노드 중 하나라도 Success/Running이면 해당 값 반환
    /// Running 중인 자식 인덱스를 기억하여 매 프레임 처음부터 재평가하지 않음
    /// 모든 자식이 Failure를 반환하면 Failure 반환
    /// </summary>
    public class Selector : BTComposite
    {
        public Selector(List<BTNode> children) : base(children) { }

        public override NodeStatus Evaluate()
        {
            // Running 중인 자식이 있으면 해당 자식부터 이어서 평가
            int startIndex = currentChildIndex >= 0 ? currentChildIndex : 0;

            for (int i = startIndex; i < children.Count; i++)
            {
                NodeStatus childStatus = children[i].Evaluate();

                if (childStatus == NodeStatus.Running)
                {
                    currentChildIndex = i;
                    return NodeStatus.Running;
                }
                if (childStatus == NodeStatus.Success)
                {
                    currentChildIndex = -1;
                    return NodeStatus.Success;
                }
                // Failure면 다음 자식으로
            }

            currentChildIndex = -1;
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Sequence 노드 - 자식 노드가 모두 Success여야 Success 반환
    /// Running 중인 자식 인덱스를 기억하여 매 프레임 처음부터 재평가하지 않음
    /// 하나라도 Failure면 즉시 Failure 반환
    /// </summary>
    public class Sequence : BTComposite
    {
        public Sequence(List<BTNode> children) : base(children) { }

        public override NodeStatus Evaluate()
        {
            // Running 중인 자식이 있으면 해당 자식부터 이어서 평가
            int startIndex = currentChildIndex >= 0 ? currentChildIndex : 0;

            for (int i = startIndex; i < children.Count; i++)
            {
                NodeStatus childStatus = children[i].Evaluate();

                if (childStatus == NodeStatus.Running)
                {
                    currentChildIndex = i;
                    return NodeStatus.Running;
                }
                if (childStatus == NodeStatus.Failure)
                {
                    currentChildIndex = -1;
                    return NodeStatus.Failure;
                }
                // Success면 다음 자식으로
            }

            currentChildIndex = -1;
            return NodeStatus.Success;
        }
    }
}
