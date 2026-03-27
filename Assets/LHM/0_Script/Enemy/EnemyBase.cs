using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    public Rigidbody2D rigid2d;
    Animator animator;
    EnemyMovement enemyMovement;
    EnemyCombat enemyCombat;

    public Transform playerTransform;

    public float moveSpeed = 1.0f;
    public float dashSpeed = 1.0f;
    public float dashDuration = 1.0f;
    public Transform pointA;
    public Transform pointB;

    //탐지
    public float detectionRange = 1.0f; //(플레이어 탐지 거리)
    public float dashRange = 1.0f; //(돌진 시작 거리)

    //전투
    public float maxHp = 1.0f;
    public float currentHp = 1.0f;
    public float attackDamage = 1.0f;
    public float attackRange = 1.0f;
    public float attackCooldown = 1.0f;

    public bool isDead;
    public bool isAttacking;
    public bool isPlayerDetected;

    //애니메이션 파라미터
    string ANIM_WALK;
    string ANIM_CHASE;
    string ANIM_DASH;
    string ANIM_ATTACK;
    string ANIM_HIT;
    string ANIM_DEAD;

    void Start()
    {
        rigid2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
        enemyMovement = GetComponent<EnemyMovement>();
        GameObject playerTransform = GameObject.FindWithTag("Player");
        currentHp = maxHp;
        enemyMovement.Init();

    }



    void Update()
    {
        if(isDead == true) {
            return;
        }
        enemyMovement.DetectPlayer();
        enemyMovement.FlipSprite();
    }


    void OnDrawGizmos()
    {
        if (isDead == true)
            return;

        if (enemyCombat != null) {
            // 노란색 detectionRange 원 탐지
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(enemyCombat.transform.position, detectionRange);

            // 빨간색 dashRange 원 돌진
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(enemyCombat.transform.position, dashRange);

            // 파란색 attackRange 원 공격
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(enemyCombat.transform.position, attackRange);
        }
    }
}