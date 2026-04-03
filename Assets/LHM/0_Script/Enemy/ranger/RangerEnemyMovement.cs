using UnityEngine;

public class RangerEnemyMovement : EnemyMovement
{

    public override void DetectPlayer()
    {
        if (enemyBase == null || enemyBase.isDead) return;
        if (enemyBase.playerTransform == null) return;

        Vector2 diff = enemyBase.playerTransform.position - transform.position;
        bool detected = Mathf.Abs(diff.x) <= detectionRangeX && Mathf.Abs(diff.y) <= detectionRangeY;
        if(detected)Debug.Log($"플레이어 감지 거리: {diff.magnitude})");

        if (detected == enemyBase.isPlayerDetected) return;

        enemyBase.isPlayerDetected = detected;

        if (enemyBase.isAttacking) return; // 공격 중엔 감지 무시
        if (currentMoveCoroutine != null)
            StopCoroutine(currentMoveCoroutine);

        currentMoveCoroutine = StartCoroutine(detected ? EnemyChaseMove() : EnemyNormalMove());
    }
}
