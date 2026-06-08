using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace EnemyVersion3
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
                    if (Vector2.Distance(current, targetPos) < 0.1f 
                        || Mathf.Abs(targetPos.x - current.x) < 0.1f) break;

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
                if(bb.enemyCombat._isAttack) return;
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
                var tmg = CustomTimeManager.Instance;
                while (!ct.IsCancellationRequested)
                {
                    // 공격 범위 진입 또는 추격 해제 → BT가 다음 틱에 브랜치 재선택
                    if (bb.DistanceToPlayer <= bb.attackRange) break;
                    if (!bb.isDetected) break;

                    float dirX = Mathf.Sign(
                        bb.playerTransform.position.x - bb.selfTransform.position.x);
                    Vector2 pos = bb.selfTransform.position;

                    bool wallAhead = Physics2D.Raycast(
                        pos,
                        new Vector2(dirX, 0f),
                        bb.moveable.patrolWallCheckX,
                        bb.groundLayer);

                    if (wallAhead)
                    {
                        // 정지 후 대기
                        // hasLOS: 플레이어가 보이면 → 외부 루프 continue로 추격 재개
                        // !isDetected: 게이지 소진 → 외부 루프 break로 순찰 복귀
                        while (!ct.IsCancellationRequested)
                        {
                            bb.rigid2D.linearVelocity =
                                new Vector2(0f, bb.rigid2D.linearVelocity.y);

                            if (bb.hasLOS) break; // 시야 확보 → 추격 재개
                            if (!bb.isDetected) break; // 게이지 소진 → 순찰 복귀

                            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                        }
                        continue;
                    }
                    
                    Vector2 edgeOrigin = pos + new Vector2(dirX * bb.moveable.patrolEdgeCheckX, 0f);
                    bool groundAhead = Physics2D.Raycast(
                        edgeOrigin, Vector2.down,
                        bb.moveable.patrolEdgeCheckY,
                        bb.groundLayer);

                    if (!groundAhead)
                    {
                        bb.rigid2D.linearVelocity =
                            new Vector2(0f, bb.rigid2D.linearVelocity.y);
                        await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                        continue;
                    }

                    bb.rigid2D.linearVelocity =
                        bb.moveable.GetSlopeVelocity(dirX, 
                        bb.moveable.moveSpeed * tmg.EnemyTimeScale, 
                        bb.groundLayer);

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
                var tmg = CustomTimeManager.Instance;
                while (!ct.IsCancellationRequested)
                {
                    if (bb.isDetected) break;

                    float dirX = bb.moveable.patrolDirection;

                    Vector2 pos = bb.selfTransform.position;
                    bool wallAhead = Physics2D.Raycast(pos, new Vector2(dirX, 0f),
                        bb.moveable.patrolWallCheckX, bb.groundLayer);

                    Vector2 edgeOrigin = pos + new Vector2(dirX * bb.moveable.patrolEdgeCheckX, 0f);
                    bool groundAhead = Physics2D.Raycast(edgeOrigin, Vector2.down,
                        bb.moveable.patrolEdgeCheckY, bb.groundLayer);

                    if (wallAhead || !groundAhead)
                    {
                        // 방향 반전 후 한 프레임 정지
                        bb.moveable.patrolDirection = -bb.moveable.patrolDirection;
                        bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                        await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                        continue;
                    }

                    bb.rigid2D.linearVelocity = 
                        bb.moveable.GetSlopeVelocity(dirX, 
                        bb.moveable.moveSpeed * tmg.EnemyTimeScale, 
                        bb.groundLayer);
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }
    
    public class ActionStun : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionStun);

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            try
            {
                float elapsed = 0f;
                bb.isStunned = true;

                while (!ct.IsCancellationRequested && elapsed < bb.stunDuration)
                {
                    elapsed += CustomTimeManager.Instance.EnemyFixedDeltaTime;
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = Vector2.zero;
                bb.isStunned = false;
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException)
            {
                bb.isStunned = false;
            }
        }
    }

    public class ActionIdle : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionIdle);
        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (bb.isDetected) break;
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }
}