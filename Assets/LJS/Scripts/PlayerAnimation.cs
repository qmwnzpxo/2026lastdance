using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    PlayerState state;
    [SerializeField] public Animator ani;

    void Awake()
    {
        if (state == null)
            state = GetComponent<PlayerState>();
        if (ani == null)
            ani = GetComponent<Animator>();
    }

    private bool HasAnimator()
    {
        if (ani == null) ani = GetComponent<Animator>();
        return ani != null;
    }

    private bool HasState()
    {
        if (state == null) state = GetComponent<PlayerState>();
        return state != null;
    }

    /*isGrounded
     * isRun
     * isPowerfulRun
     * isAttack
     * isJump
     * isAttackFlash*/
    public void IsGrounded()
    {
        if (!HasAnimator() || !HasState()) return;

        if (state.isGrounded || state.isOnCeiling || state.isWallSliding)
        {
            ani.SetBool("isGrounded", true);
            Jump_off();
        }
        else
        {
            ani.SetBool("isGrounded", false);
            Jump_on();
        }
    }

    public void Run_on() { if (HasAnimator()) ani.SetBool("isRun", true); }
    public void Run_off() { if (HasAnimator()) ani.SetBool("isRun", false); }
    public void Power_Run_on() { if (HasAnimator()) ani.SetBool("isPowerfulRun", true); }
    public void Power_Run_off() { if (HasAnimator()) ani.SetBool("isPowerfulRun", false); }
    public void Jump_on() { if (HasAnimator()) ani.SetBool("isJump", true); }
    public void Jump_off() { if (HasAnimator()) ani.SetBool("isJump", false); }
    public void Attack(float amount = 1f)
    {
        if (!HasAnimator()) return;

        // 공격 모션이 아직 재생 중일 때 트리거를 걸면, Attack 상태엔 isAttack 전이가 없어서
        // 트리거가 남아 있다가 모션이 끝난 뒤 한 번 더 휘두름(연타 멈춰도 한 발 더 나가는 문제).
        // → 이미 공격 모션 중이면 트리거 대신 그 모션을 처음부터 다시 재생
        var cur = ani.GetCurrentAnimatorStateInfo(0);
        var next = ani.GetNextAnimatorStateInfo(0);
        if (IsAttackState(cur) || (ani.IsInTransition(0) && IsAttackState(next)))
        {
            ani.ResetTrigger("isAttack");
            int hash = ani.IsInTransition(0) ? next.fullPathHash : cur.fullPathHash;
            ani.Play(hash, 0, 0f);
        }
        else
        {
            ani.SetTrigger("isAttack");
        }
        ani.speed *= amount;
    }

    bool IsAttackState(AnimatorStateInfo info) => info.IsName("Attack") || info.IsName("Attack_Air");
    public void Attack_flash() { if (HasAnimator()) ani.SetTrigger("isAttackFlash"); }

    public void Dash(int greenCombo = 1)
    {
        if (!HasAnimator() || !HasState()) return;

        switch (state.currentChip)
        {
            case PlayerChip.pink:
                ani.SetTrigger("isDash_Pink");
                break;
            case PlayerChip.red:
                ani.SetTrigger("isDash_Red");
                break;
            case PlayerChip.green:
                ani.SetInteger("GreenCombo", greenCombo);
                ani.SetTrigger("isDash_Green");
                break;
            case PlayerChip.blue:
                ani.SetTrigger("isDash_Pink");
                break;
        }
    }

    public void ResetCombo()
    {
        if (HasAnimator()) ani.SetInteger("GreenCombo", 0);
    }

    public void BlueDash()
    {
        if (HasAnimator()) ani.SetTrigger("isDash_Blue");
    }

    public void Death()
    {
        if (HasAnimator()) ani.SetTrigger("isDead");
    }
}
