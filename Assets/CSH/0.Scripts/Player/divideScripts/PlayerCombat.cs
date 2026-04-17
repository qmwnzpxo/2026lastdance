using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    // ── 공격 설정 ────────────────────────────
    [Header("공격")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2   attackBoxSize     = new Vector2(1f, 1f);
    [SerializeField] private float     attackBoxDistance = 1f;
    [SerializeField] private float     attackSecondHitDelay = 0.3f;
    [SerializeField] private LayerMask enemyLayer;

    public float attackRepeatTime = 2;

    private PlayerState state;
    private PlayerAnimation ani;

    // ────────────────────────────────────────
    #region Unity 생명주기

    void Awake()
    {
        state = GetComponent<PlayerState>();
        ani = GetComponent<PlayerAnimation>();
        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("enemy");
    }

    void Update()
    {
        if (!state.isAttacking)
        {
            RotateAttackPivot();
        }
        HandleAttack();

        // 디버그 히트스톱 (필요 없으면 제거)
        if (Input.GetKeyDown(KeyCode.H))
            HitstopManager.Instance.TriggerHitstop(duration:0.2f);
    }

    #endregion
    // ────────────────────────────────────────
    #region 공격 피벗 회전

    void RotateAttackPivot()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -Camera.main.transform.position.z;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2 dir   = (mouseWorldPos - (Vector2)transform.position).normalized;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        attackTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    #endregion
    // ────────────────────────────────────────
    #region 공격

    void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0) || state.isAttacking) return;
        ani.Attack();
        PerformAttack().Forget();
    }

    private async UniTaskVoid PerformAttack()
    {
        state.isAttacking = true;

        Vector2 attackPos = (Vector2)attackTransform.position
                          + (Vector2)attackTransform.right * attackBoxDistance;
        for (int i = 0; i < attackRepeatTime; i++)
        {
            ExecuteAttackBox(attackPos);
            await UniTask.WaitForSeconds(attackSecondHitDelay/attackRepeatTime,
                cancellationToken: this.destroyCancellationToken);
        }

        state.isAttacking = false;
    }

    void ExecuteAttackBox(Vector2 attackPos)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPos, attackBoxSize, attackTransform.eulerAngles.z, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            Debug.Log($"공격 적중: {hit.name}");
            ApplyDamage(hit, state.damage);
            state.AddDashGauge(state.dashGaugePerAttack);
            HitstopManager.Instance.TriggerHitstop();
        }

        Debug.DrawRay(attackTransform.position,
            attackTransform.right * attackBoxDistance, Color.magenta, 0.15f);
    }

    /// <summary>대상 컴포넌트에 맞게 데미지를 전달한다.</summary>
    private void ApplyDamage(Collider2D target, float damage)
    {
        target.GetComponent<BossBase>()?.TakeDamage(damage);
        target.GetComponent<ObjectHp>()?.OnDamage(damage);
        target.GetComponent<EnemyBase>()?.OnDamage(damage);
        target.GetComponent<NewEnemyBase>()?.OnDamage(damage);
    }

    #endregion
    // ────────────────────────────────────────
    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (state == null) return;
        if (attackTransform != null && state.isAttacking)
        {
            Gizmos.color = Color.magenta;
            Vector2 attackPos = (Vector2)attackTransform.position
                              + (Vector2)attackTransform.right * attackBoxDistance;
            Gizmos.DrawWireCube(attackPos, attackBoxSize);
        }
    }

    #endregion
}
