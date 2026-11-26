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
    public void Attack(float amount = 1f) { if (HasAnimator()) { ani.SetTrigger("isAttack"); ani.speed *= amount; } }
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
