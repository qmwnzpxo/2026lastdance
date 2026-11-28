using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace EnemyVersion3
{
    public class ActionRetreat : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionRetreat);

        // 후퇴 시작 후 이 시간(적 배율 시간) 안에 벽·절벽에 막히면 반대 방향으로 후퇴. 그 뒤에 막히면 후퇴 중지
        private const float ReverseWindow = 0.3f;

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.moveable == null || bb.playerTransform == null)
            {
                bb.lastActionResult = NodeState.Failure;
                bb.actionCompleted = true;
                return;
            }
            try
            {
                var tmg = CustomTimeManager.Instance;
                var mv = bb.moveable;

                // 후퇴는 수평 이동만 함 → 목표도 x 좌표만 사용
                // (플레이어 반대 방향 단위 벡터의 x 성분 × 후퇴 거리. 플레이어가 바로 위에 있으면 짧게 후퇴)
                Vector2 retreatDir = ((Vector2)bb.selfTransform.position
                                   - (Vector2)bb.playerTransform.position).normalized;
                float horizontalDistance = Mathf.Abs(retreatDir.x) * mv.retreatDistance;
                float targetX = bb.selfTransform.position.x + retreatDir.x * mv.retreatDistance;

                float elapsed = 0f;
                bool reversed = false;

                while (!ct.IsCancellationRequested)
                {
                    Vector2 current = bb.selfTransform.position;
                    if (Mathf.Abs(targetX - current.x) < 0.1f) break;

                    float dirX = Mathf.Sign(targetX - current.x);

                    if (IsBlocked(bb, current, dirX))
                    {
                        if (!reversed && elapsed <= ReverseWindow)
                        {
                            // 시작 직후 막힘 → 같은 거리만큼 반대 방향으로 후퇴 (한 번만)
                            reversed = true;
                            targetX = current.x - dirX * horizontalDistance;
                            continue; // 반대 방향도 막혔는지 바로 다시 확인 (reversed라 무한 반복 없음)
                        }
                        break; // 후퇴 중 막힘 (또는 반대도 막힘) → 그 자리에서 후퇴 중지
                    }

                    // 추격·순찰과 같이 적 배율 적용
                    bb.rigid2D.linearVelocity = new Vector2(
                        dirX * mv.moveSpeed * tmg.EnemyTimeScale,
                        bb.rigid2D.linearVelocity.y);

                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                    elapsed += tmg.EnemyFixedDeltaTime;
                }

                bb.rigid2D.linearVelocity = new Vector2(0f, bb.rigid2D.linearVelocity.y);
                if(bb.moveable != null)
                    bb.moveable.needsRetreat = false;
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }

        // 진행 방향에 벽이 있거나 발판 끝이면 막힘 (ActionPatrol과 같은 판정)
        private static bool IsBlocked(EnemyBlackboard bb, Vector2 pos, float dirX)
        {
            var mv = bb.moveable;
            bool wallAhead = Physics2D.Raycast(pos, new Vector2(dirX, 0f),
                mv.patrolWallCheckX, bb.groundLayer);

            Vector2 edgeOrigin = pos + new Vector2(dirX * mv.patrolEdgeCheckX, 0f);
            bool groundAhead = Physics2D.Raycast(edgeOrigin, Vector2.down,
                mv.patrolEdgeCheckY, bb.groundLayer);

            return wallAhead || !groundAhead;
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
                if (bb.enemyCombat._isAttack)
                {
                    bb.actionCompleted = true;
                    bb.lastActionResult = NodeState.Success;
                    return;
                }
                Debug.Log($"[BT Action attack] executeattack called");
                await bb.enemyCombat.ExecuteAttack(ct);

                bb.attackCooldownRemaining = bb.attackCooldown; // 감소는 EnemyBTController.UpdateBlackboard
                // 공격 직후 거리 확인 → 후퇴 필요 여부 설정
                if (bb.moveable != null)
                {
                    if (bb.DistanceToPlayer <= (float)bb.moveable?.retreatTriggerRange)
                        bb.moveable.needsRetreat = true;
                }

                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException) { }
        }
    }

    public class ActionChase : BTAsyncAction
    {
        protected override string ActionId => nameof(ActionChase);

        // 플레이어와의 x 거리가 이보다 작으면 추격 이동을 멈춤
        private const float ChaseDeadZoneX = 0.1f;

        protected override async UniTaskVoid ExecuteAsync(
            EnemyBlackboard bb, CancellationToken ct)
        {
            if (bb.moveable == null)
            {
                // 완료 표시를 해야 BT가 Failure를 받고 다음 분기로 넘어감 (안 하면 Running으로 남음)
                bb.lastActionResult = NodeState.Failure;
                bb.actionCompleted = true;
                return;
            }
            try
            {
                var tmg = CustomTimeManager.Instance;
                while (!ct.IsCancellationRequested)
                {
                    // 공격 가능 위치(사거리 안 + 시야 확보) 도달 또는 추격 해제 → BT가 다음 틱에 브랜치 재선택
                    // 시야 없이 거리만 보고 멈추면, 바닥 너머 위층 플레이어처럼 공격도 이동도 못 하고 계속 서 있게 됨
                    if (bb.DistanceToPlayer <= bb.attackRange && bb.hasLOS) break;
                    if (!bb.isDetected) break;

                    float dx = bb.playerTransform.position.x - bb.selfTransform.position.x;

                    // 플레이어가 거의 바로 위·아래 → 좌우로 떨며 왕복하지 않게 정지
                    if (Mathf.Abs(dx) < ChaseDeadZoneX)
                    {
                        bb.rigid2D.linearVelocity =
                            new Vector2(0f, bb.rigid2D.linearVelocity.y);
                        await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                        continue;
                    }

                    float dirX = Mathf.Sign(dx);
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
                        // 반드시 한 프레임 쉬고 다시 확인.
                        // hasLOS로 빠져나왔는데 벽이 그대로면 await 없이 바깥↔안쪽 루프를 무한 반복해
                        // 게임이 멈춤 (hasLOS·위치는 FixedTick에서만 갱신되므로 같은 프레임 안에선 안 바뀜)
                        await UniTask.WaitForFixedUpdate(cancellationToken: ct);
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
                // 완료 표시를 해야 BT가 Failure를 받고 다음 분기로 넘어감 (안 하면 Running으로 남음)
                bb.lastActionResult = NodeState.Failure;
                bb.actionCompleted = true;
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
                bb.isStunned = true;

                // bb.stunElapsed에서 이어서 진행 (새 스턴이면 ApplyStun이 0으로 초기화)
                while (!ct.IsCancellationRequested && bb.stunElapsed < bb.stunDuration)
                {
                    bb.stunElapsed += CustomTimeManager.Instance.EnemyFixedDeltaTime;
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                }

                bb.rigid2D.linearVelocity = Vector2.zero;
                bb.isStunned = false;
                bb.stunElapsed = 0f;
                bb.actionCompleted = true;
                bb.lastActionResult = NodeState.Success;
            }
            catch (System.OperationCanceledException)
            {
                bb.isStunned = false;
                bb.stunElapsed = 0f;
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