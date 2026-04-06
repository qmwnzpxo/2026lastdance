using UnityEngine;

public class RangerEnemyBase : EnemyBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        //detectionRange = 5f;
        attackRange = 4f;
        dashDuration = 0;
        dashRange = 0;
    }

    // Update is called once per frame
    protected override void Update()
    {
        if (isDead == true)
        {
            return;
        }
        
        if (!isAttacking)
        {
            enemyMovement.DetectPlayer();
            enemyMovement.FlipSprite();
        }
        
    }
}
