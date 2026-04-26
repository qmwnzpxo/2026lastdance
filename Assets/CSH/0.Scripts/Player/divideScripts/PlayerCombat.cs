using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    // ── 공격 설정 ────────────────────────────
    [Header("공격")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2   attackBoxSize     = new Vector2(1f, 1f);
    [SerializeField] private float     attackBoxDistance = 1f;
    [SerializeField] private float     attackSecondHitDelay = 0.28f;
    [SerializeField] private float     attackfloatHitDelay = 0.15f;
    [SerializeField] private float     attackActiveDuration = 0.1f;
    [SerializeField] private LayerMask enemyLayer;

    public float attackRepeatTime = 2;

    private HashSet<Collider2D> _hitEnemiesInAttack = new HashSet<Collider2D>();

    private PlayerState state;
    private PlayerAnimation ani;
    private Camera mainCamera;

    // ────────────────────────────────────────
    #region Unity 생명주기

    void Awake()
    {
        if(state == null)
            state = GetComponent<PlayerState>();
        if(ani == null)
            ani = GetComponent<PlayerAnimation>();
        if(attackTransform == null)
            attackTransform = transform.Find("attack check");
        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("enemy");
        mainCamera = Camera.main;
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
            HitstopManager.Instance.TriggerHitstop();
    }

    #endregion
    // ────────────────────────────────────────
    #region 공격 피벗 회전

    void RotateAttackPivot()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

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
        PerformAttack().Forget();
    }

    private async UniTaskVoid PerformAttack()
    {
        state.isAttacking = true;

        ani.Attack();

        Vector2 attackPos = (Vector2)attackTransform.position
                          + (Vector2)attackTransform.right * attackBoxDistance;
        if (state.isGrounded||state.isOnCeiling)
        {
            for (int i = 0; i < attackRepeatTime; i++)
            {
                float elapsed = 0f;
                while(elapsed < attackActiveDuration)
                {
                    ExecuteAttackBox(attackPos);

                    await UniTask.WaitForFixedUpdate(cancellationToken: this.destroyCancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }

                _hitEnemiesInAttack.Clear();
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < attackActiveDuration)
            {
                ExecuteAttackBox(attackPos);

                await UniTask.WaitForFixedUpdate(cancellationToken: this.destroyCancellationToken);
                elapsed += Time.fixedDeltaTime;
            }

            _hitEnemiesInAttack.Clear();
        }

        //ani.Attack_off();
        state.isAttacking = false;
    }

    void ExecuteAttackBox(Vector2 attackPos)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPos, attackBoxSize, attackTransform.eulerAngles.z, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || _hitEnemiesInAttack.Contains(hit)) continue;
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;

            _hitEnemiesInAttack.Add(hit);

            Debug.Log($"공격 적중: {hit.name}");
            ApplyDamage(hit, state.damage);
            state.AddDashGauge(state.dashGaugePerAttack);
            HitstopManager.Instance.TriggerHitstop();
        }

        Debug.DrawRay(attackTransform.position,
            attackTransform.right * attackBoxDistance, Color.magenta, attackActiveDuration);
    }

    /// <summary>대상 컴포넌트에 맞게 데미지를 전달한다.</summary>
    private void ApplyDamage(Collider2D target, float damage)
    {
        try
        {
            target.GetComponent<IDamageable>()?.TakeDamage(damage);
        }
        catch (SystemException ex)
        {
            Debug.LogError($"데미지 적용 중 오류: {ex.Message}");
        }
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
