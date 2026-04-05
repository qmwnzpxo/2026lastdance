using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerState : MonoBehaviour
{
    [RuntimeOnly, SerializeField] public float currentHp = 3f;
    //[field: SerializeField] public float HP { get { return currenthp; } private set; }
    [RuntimeOnly, SerializeField] public float maxHp=3;
    [RuntimeOnly, SerializeField] public float damage = 0.5f;
    PlayerMove playerMove;

    CancellationTokenSource hitCts;
    [EditorOnly, SerializeField] public Light2D hitlight;

    void Awake()
    {
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove>();

        currentHp = maxHp;
        hitlight.enabled = false;
    }

    public void TakeDamage(float damage)
    {
        currentHp -= damage;
        hitCts?.Cancel();
        hitCts = new CancellationTokenSource();
        if (currentHp <= 0)
        {
            Die();
        }
        Hitglow(hitCts.Token).Forget();
    }

    void Die()
    {
        Debug.Log("Player has died.");
        {
            //playerMove.CancelAllTask();
            currentHp = maxHp;
        }
    }
    public async UniTaskVoid Hitglow(CancellationToken tk)
    {
        hitlight.enabled = true;
        var result = await UniTask.WaitForSeconds(0.3f, cancellationToken: tk)
            .SuppressCancellationThrow();
        if (!result)
        {
            hitlight.enabled = false;
        }
    }
    void OnDestroy() // 추가
    {
        hitCts?.Cancel();
        hitCts?.Dispose();
    }
}
