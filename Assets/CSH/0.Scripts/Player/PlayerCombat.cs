using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using DialogSystem.CinematicDialogue;

public class PlayerCombat : MonoBehaviour
{
    // ── 공격 설정 ────────────────────────────
    [Header("공격")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2   attackBoxSize     = new Vector2(1f, 1f);
    // 업그레이드 값(baseAttackBox + 증가량, 기본 1)은 인스펙터에 설정한 박스 크기에 곱하는 배율로 쓴다.
    // (예전엔 값을 그대로 대입해서 인스펙터 값이 1×1로 덮어써졌음)
    [RuntimeOnly] public float AttackBoxSize
    {
        set
        {
            // 첫 설정 시점에 인스펙터 값을 기준으로 보관 (PlayerUpgrader가 다른 Awake에서 먼저 부를 수 있어 지연 캡처)
            if (!_hasBaseAttackBoxSize)
            {
                _baseAttackBoxSize = attackBoxSize;
                _hasBaseAttackBoxSize = true;
            }
            attackBoxSize = _baseAttackBoxSize * value;
        }
    }
    private Vector2 _baseAttackBoxSize;
    private bool _hasBaseAttackBoxSize;

    [SerializeField] private float     attackBoxDistance = 1f;
    [SerializeField] private float     attackSecondHitDelay = 0.427f;
    //[SerializeField] private float     attackfloatHitDelay = 0.15f;
    [SerializeField] private float     attackActiveDuration = 0.233f;
    public float AttackSpeed = 1f;

    private LayerMask enemyLayer;
    private LayerMask bulletLayer;
    private LayerMask combined;

    public int attackRepeatTime = 1;
    public float dashGaugePerAttack = 5;

    private HashSet<Collider2D> _hitEnemiesInAttack = new HashSet<Collider2D>();
    /// <summary>빨강 칩 기록 중 공격 입력 (회전, 땅 공격 여부)</summary>
    public event Action<Quaternion, bool> OnAttackInput;

    private PlayerState state;
    private PlayerAnimation ani;
    private Camera mainCamera;
    private PlayerMove playerMove;

    // ────────────────────────────────────────
    #region Unity 생명주기

    void Awake()
    {
        if(state == null)
            state = GetComponent<PlayerState>();
        if(ani == null)
            ani = GetComponent<PlayerAnimation>();
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove>();
        if (attackTransform == null)
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
        if (DialogueManager.Instance != null && DialogueManager.Instance.PlaybackCts != null)
        {
            return;
        }
        if (!state.isAttacking)
        {
            RotateAttackPivot();
        }
        HandleAttack();

        // 디버그 히트스톱 (필요 없으면 제거)
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log($"[PlayerCombat] called HItstop");
            HitstopManager.Instance.TriggerHitstop(duration:0.8f,timeScale:0.5f);
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
        if (angle <= 90) { }
        attackTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    #endregion
    // ────────────────────────────────────────
    #region 공격

    void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (state.isAttacking) return;
        // 빨강 칩 재생 중엔 직접 공격 불가 (기록된 공격만 TriggerRedDashAttack으로 재생)
        if (state.isRedDashPlaying) return;

        if (state.isRedDashRecording)
        {
            bool ground = IsGroundAttack();
            OnAttackInput?.Invoke(attackTransform.rotation, ground);
            // 판정 없이 모션만 실행. isAttacking을 켜야 PlayerMove가 Run을 꺼서
            // 이동 중에도 공격 모션으로 전이됨 (ani.Attack()만 부르면 멈출 때까지 공격 모션이 안 나옴)
            PerformAttack(applyHit: false, groundAttack: ground).Forget();
            return;
        }

        PerformAttack().Forget();
    }

    /// <summary>
    /// 빨강 칩 재생용 공격. groundAttack은 기록 당시 값을 그대로 넘긴다.
    /// (재생 위치의 땅 판정은 기록과 달라질 수 있어, 그 값으로 공격 길이를 정하면 다음 기록 공격과 겹침)
    /// </summary>
    public void TriggerRedDashAttack(Quaternion attackRotation, bool groundAttack)
    {
        // 피벗 강제 회전
        attackTransform.rotation = attackRotation;
        playerMove.SetFacingRight(attackTransform.right.x >= 0f);
        // ani.Attack()은 PerformAttack 안에서 호출됨 (여기서도 부르면 트리거가 두 번 걸려 재생 때 한 번 더 휘두름)

        PerformAttack(applyHit: true, groundAttack: groundAttack).Forget();
        //ExecuteAttackBox(attackPos);
        //_hitEnemiesInAttack.Clear(); // 다음 연타를 위해 바로 클리어
    }

    // 땅(천장) 공격 = 긴 2타 공격(attackSecondHitDelay), 아니면 짧은 공중 공격(attackActiveDuration)
    private bool IsGroundAttack() => state.isGrounded || state.isOnCeiling;

    /// <param name="groundAttack">null이면 지금 땅 판정으로 결정. 빨강 칩 기록·재생은 기록 값을 넘김</param>
    private async UniTaskVoid PerformAttack(bool applyHit = true, bool? groundAttack = null)
    {
        bool ground = groundAttack ?? IsGroundAttack();
        state.isAttacking = true;
        playerMove.SetFacingRight(attackTransform.right.x >= 0f);

        ani.Attack();

        Vector2 attackPos = (Vector2)attackTransform.position
                          + (Vector2)attackTransform.right * attackBoxDistance;

        try
        {
            var timeManager = CustomTimeManager.Instance;
            if (ground)
            {
                for (int i = 0; i < attackRepeatTime; i++)
                {
                    float elapsed = 0f;
                    while (elapsed < attackSecondHitDelay/attackRepeatTime)
                    {
                        if (applyHit) ExecuteAttackBox(attackPos, i);

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
                    if (applyHit) ExecuteAttackBox(attackPos);

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
            state.AddDashGauge(dashGaugePerAttack);
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
            //target.gameObject.GetComponent<IDamageable>()?.TakeDamage(damage);
            var damaged = target.GetComponents<IDamageable>();
            if (damaged == null) return;
            else
            {
                foreach (IDamageable damageable in damaged)
                {
                    damageable.TakeDamage(damage);
                }
            }
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
