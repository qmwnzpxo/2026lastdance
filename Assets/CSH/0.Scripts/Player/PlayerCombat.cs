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
    [RuntimeOnly] public float AttackBoxSize { set { attackBoxSize.x = value; attackBoxSize.y = value; } }

    [SerializeField] private float     attackBoxDistance = 1f;
    [SerializeField] private float     attackSecondHitDelay = 0.427f;
    //[SerializeField] private float     attackfloatHitDelay = 0.15f;
    [SerializeField] private float     attackActiveDuration = 0.233f;
    public float AttackSpeed = 1f;

    private LayerMask enemyLayer;
    private LayerMask bulletLayer;
    private LayerMask combined;

    public int attackRepeatTime = 2;

    private HashSet<Collider2D> _hitEnemiesInAttack = new HashSet<Collider2D>();
    public event Action<Quaternion> OnAttackInput;

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
        if(bulletLayer == 0)
            bulletLayer = LayerMask.GetMask("enemyBullet");
        combined = enemyLayer | bulletLayer;

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
        {
            Debug.Log($"[PlayerCombat] called HItstop");
            HitstopManager.Instance.TriggerHitstop(duration:1f,timeScale:0.5f);
        }
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
        if (!Input.GetMouseButtonDown(0)) return;
        if (state.isAttacking) return;

        if (state.isRedDashRecording)
        {
            OnAttackInput?.Invoke(attackTransform.rotation);
            return;
        }

        PerformAttack().Forget();
    }
    public void TriggerRedDashAttack(Quaternion attackRotation)
    {
        // 피벗 강제 회전
        attackTransform.rotation = attackRotation;
        ani.Attack();

        Vector2 attackPos = (Vector2)attackTransform.position
                          + (Vector2)attackTransform.right * attackBoxDistance;


        PerformAttack().Forget();
        //ExecuteAttackBox(attackPos);
        //_hitEnemiesInAttack.Clear(); // 다음 연타를 위해 바로 클리어
    }

    private async UniTaskVoid PerformAttack()
    {
        state.isAttacking = true;

        ani.Attack();

        Vector2 attackPos = (Vector2)attackTransform.position
                          + (Vector2)attackTransform.right * attackBoxDistance;

        try
        {
            var timeManager = CustomTimeManager.Instance;
            if (state.isGrounded || state.isOnCeiling)
            {
                for (int i = 0; i < attackRepeatTime; i++)
                {
                    float elapsed = 0f;
                    while (elapsed < attackSecondHitDelay/attackRepeatTime)
                    {
                        ExecuteAttackBox(attackPos, i);

                        await UniTask.WaitForFixedUpdate(cancellationToken: this.destroyCancellationToken);
                        elapsed += timeManager.PlayerFixedDeltaTime * AttackSpeed;
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
                    elapsed += timeManager.PlayerFixedDeltaTime * AttackSpeed;
                }

                _hitEnemiesInAttack.Clear();
            }
        }
        finally
        {
            //ani.Attack_off();
            state.isAttacking = false;
        }
    }

    void ExecuteAttackBox(Vector2 attackPos, int type = 0)
    {

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPos, attackBoxSize, attackTransform.eulerAngles.z, combined);

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

        if (type != 0)
            Debug.DrawRay(attackTransform.position,
                attackTransform.right * attackBoxDistance, Color.cyan, attackActiveDuration);
        else
            Debug.DrawRay(attackTransform.position,
                attackTransform.right * attackBoxDistance, Color.magenta, attackActiveDuration);

    }

    /// <summary>대상 컴포넌트에 맞게 데미지를 전달한다.</summary>
    private void ApplyDamage(Collider2D target, float damage)
    {
        try
        {
            target.gameObject.GetComponent<IDamageable>()?.TakeDamage(damage);
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
