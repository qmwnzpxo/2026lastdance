using UnityEngine;

namespace BossBT
{
    /// <summary>
    /// Behavior Tree 노드 상태 열거형
    /// </summary>
    public enum NodeStatus
    {
        Running,  // 실행 중
        Success,  // 성공
        Failure   // 실패
    }

    /// <summary>
    /// Behavior Tree 노드 추상 기반 클래스
    /// 모든 BT 노드는 이 클래스를 상속받아야 함
    /// </summary>
    public abstract class BTNode
    {
        /// <summary>
        /// 노드 평가 - 매 틱마다 호출되어 현재 상태를 반환
        /// </summary>
        public abstract NodeStatus Evaluate();

        /// <summary>
        /// 노드 상태 초기화 - 트리 재사용 시 호출
        /// </summary>
        public virtual void Reset() { }
    }
}
