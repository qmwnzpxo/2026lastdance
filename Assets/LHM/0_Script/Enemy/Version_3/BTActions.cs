using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BTActions
{
    public class ActionRetreat : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionRetreat);

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.moveable == null)
            {
                bb.lastActionResult = NodeState.Failure;
                return;
            }
            try
            {
                Vector2 retreatDir = ((Vector2)bb.selfTransform.position
                                   - (Vector2)bb.playerTransform.position).normalized;
                Vector2 targetPos = (Vector2)bb.selfTransform.position
                                   + retreatDir * (float)bb.moveable?.retreatDistance;

                while (!ct.IsCancellationRequested)
                {
                    Vector2 current = bb.selfTransform.position;
                    if (Vector2.Distance(current, targetPos) < 0.1f) break;

                    bb.rigid2D.linearVelocity = new Vector2(
                        (targetPos - current).normalized.x * (float)bb.moveable?.moveSpeed,
                        bb.rigid2D.linearVelocity.y);

                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                if(bb.moveable != null)
                    bb.moveable.needsRetreat = false;
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }
    public class ActionAttack : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionAttack);

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.enemyCombat == null)
            {
                bb.lastActionResult = NodeState.Failure;
                bb.actionCompleted = true;
                return;
            }

            try
            {

                Debug.Log($"[BT Action attack] executeattack called");
                await bb.enemyCombat.ExecuteAttack(ct);

                bb.lastAttackTime = Time.time;
                // 공격 직후 거리 확인 → 후퇴 필요 여부 설정
                if (bb.DistanceToPlayer <= (float)bb.moveable?.retreatTriggerRange)
                    bb.moveable.needsRetreat = true;

                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }

    public class ActionChase : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionChase);

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.moveable == null)
            {
                bb.lastActionResult = NodeState.Failure;
                return;
            }
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // 공격 범위 진입 또는 추격 해제 → BT가 다음 틱에 브랜치 재선택
                    if (bb.DistanceToPlayer <= bb.attackRange) break;
                    if (!bb.isDetected) break;

                    float dirX = bb.playerTransform.position.x
                               - bb.selfTransform.position.x;
                    bb.rigid2D.linearVelocity = new Vector2(
                        Mathf.Sign(dirX) * (float)bb.moveable?.moveSpeed,
                        bb.rigid2D.linearVelocity.y);

                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }

    public class ActionPatrol : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionPatrol);

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.moveable == null)
            {
                bb.lastActionResult = NodeState.Failure;
                return;
            }
            try
            {
                if (bb.moveable.patrolPoints == null || bb.moveable.patrolPoints.Length == 0)
                {
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                    bb.actionCompleted = true;
                    bb.lastActionResult = NodeState.Success;
                    return;
                }

                Transform target = bb.moveable.patrolPoints[bb.moveable.currentPatrolIndex];

                while (!ct.IsCancellationRequested)
                {
                    if (bb.isDetected) break;

                    Vector2 current = bb.selfTransform.position;
                    Vector2 targetPos = target.position;

                    if (Vector2.Distance(current, targetPos) < 0.1f|| Mathf.Abs(targetPos.x - current.x)<0.1f)
                    {
                        // 포인트 도달 → 인덱스 증가 후 이번 액션 종료
                        // 다음 틱에서 새 ActionPatrol이 다음 포인트로 이동
                        bb.moveable.currentPatrolIndex =
                            (bb.moveable.currentPatrolIndex + 1) % bb.moveable.patrolPoints.Length;
Debug.Log($"[BTActions] Actionpatrol {bb.moveable.currentPatrolIndex} / {bb.moveable.patrolPoints.Length}");
                        break;
                    }

                    bb.rigid2D.linearVelocity = new Vector2(
                        (targetPos - current).normalized.x * (float)bb.moveable?.moveSpeed,
                        bb.rigid2D.linearVelocity.y);

                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }
}