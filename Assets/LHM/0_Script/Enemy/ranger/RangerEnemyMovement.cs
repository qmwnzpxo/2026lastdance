using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class RangerEnemyMovement : EnemyMovement
{
    protected CancellationTokenSource attack_TK;


    public override void DetectPlayer()
    {
        if (enemyBase == null || enemyBase.isDead) return;
        if (enemyBase.playerTransform == null) return;

        Vector2 diff = enemyBase.playerTransform.position - transform.position;
        bool detected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;
        
        if (detected == enemyBase.isPlayerDetected) return;
        enemyBase.isPlayerDetected = detected;

        if (enemyBase.isAttacking) return;
        
        StartMove(detected ? EnemyChaseMove : EnemyNormalMove);
    }

    protected override async UniTaskVoid EnemyChaseMove(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Vector2 diff = enemyBase.playerTransform.position - transform.position;
            bool stillDetected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;
            if (!stillDetected)
            {
                enemyBase.isPlayerDetected = false;
                StartMove(EnemyNormalMove);
                return;
            }

            float distX = Mathf.Abs(enemyBase.playerTransform.position.x - transform.position.x);
            if (distX <= enemyBase.attackRange)
            {
                StartMove(RangeAttackLoop); // moveCts 갱신 → 현재 ct 취소됨
                return;
            }

            // 사거리 밖이면 추적 이동
            float dirX = enemyBase.playerTransform.position.x - transform.position.x;
            enemyBase.rigid2d.linearVelocity = new Vector2(
                Mathf.Sign(dirX) * enemyBase.moveSpeed,
                enemyBase.rigid2d.linearVelocity.y
            );

            await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();

        }
    }

    protected async UniTaskVoid RangeAttackLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 공격 사거리 벗어나면 이동 재개
            float distX = Mathf.Abs(enemyBase.playerTransform.position.x - transform.position.x);
            if (distX > enemyBase.attackRange)
            {
                StartMove(EnemyNormalMove);
                return;
            }

            // 정지
            enemyBase.rigid2d.linearVelocity = new Vector2(0f, enemyBase.rigid2d.linearVelocity.y);

            // 공격 중이 아닐 때만 공격 호출
            if (!enemyBase.isAttacking)
            {
                enemyCombat.Attack();
            }

            await UniTask.WaitForFixedUpdate(cancellationToken: ct).SuppressCancellationThrow();
        }
    }
}
