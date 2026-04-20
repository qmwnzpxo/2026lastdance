using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    PlayerState state;
    [SerializeField]Animator ani;
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
        if (state.isGrounded || state.isOnCeiling || state.isWallSliding) ani.SetBool("isGrounded", true);
        else ani.SetBool("isGrounded", false);
    }
    public void Run_on() { ani.SetBool("isRun", true); }
    public void Run_off() { ani.SetBool("isRun", false); }
    public void Power_Run_on() { ani.SetBool("isPowerfulRun", true); }
    public void Power_Run_off() { ani.SetBool("isPowerfulRun", false); }
    public void Jump_on() { ani.SetBool("isJump", true); }
    public void Jump_off() { ani.SetBool("isJump", false); }
    public void Attack_on() { ani.SetBool("isAttack", true); }
    public void Attack_off() { ani.SetBool("isAttack", false); }
    public void Attack_flash_on() { ani.SetBool("isAttackFlash", true); }
    public void Attack_flash_off() { ani.SetBool("isAttackFlash", false); }
}
