using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    PlayerState state;
    [SerializeField]public Animator ani;
    void Start()
    {
        state = GetComponent<PlayerState>();
        if(ani == null)
            ani = GetComponent<Animator>();
    }
    /*isGrounded
     * isRun
     * isPowerfulRun
     * isAttack
     * isJump
     * isAttackFlash*/
    public void IsGrounded() {
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
    public void Run_on() { ani.SetBool("isRun", true); }
    public void Run_off() { ani.SetBool("isRun", false); }
    public void Power_Run_on() { ani.SetBool("isPowerfulRun", true); }
    public void Power_Run_off() { ani.SetBool("isPowerfulRun", false); }
    public void Jump_on() { ani.SetBool("isJump", true); }
    public void Jump_off() { ani.SetBool("isJump", false); }
    public void Attack(float amount=1f) { ani.SetTrigger("isAttack"); ani.speed *= amount; }
    public void Attack_flash() { ani.SetTrigger("isAttackFlash"); }
    public void Dash(int greenCombo = 1)
    {
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
        ani.SetInteger("GreenCombo", 0);
    }


    public void BlueDash()
    {
        ani.SetTrigger("isDash_Blue");
    }
}
