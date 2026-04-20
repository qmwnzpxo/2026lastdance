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
    public void Jump() { ani.SetBool("isGrounded", false); ani.SetTrigger("isJump"); }
    public void Attack() { ani.SetTrigger("isAttack"); }
    public void Attack_flash() { ani.SetTrigger("isAttackFlash"); }
}
