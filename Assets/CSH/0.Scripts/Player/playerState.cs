using UnityEngine;

public class PlayerState : MonoBehaviour
{
    float currenthp=3f;
    //[field: SerializeField] public float HP { get { return currenthp; } private set; }
    public float maxhp=3;
    public float damage = 0.5f;
    PlayerMove playerMove;

    void Awake()
    {
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove>();

    }

    public void TakeDamage(float damage)
    {
        currenthp -= damage;
        if (currenthp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player has died.");
        {
            //playerMove.CancelAllTask();

        }
    }
}
