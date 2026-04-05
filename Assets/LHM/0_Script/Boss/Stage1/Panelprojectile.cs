using UnityEngine;

public class PanelProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private bool isInitialized = false;

    public void Init(Vector2 targetPos, float projectileSpeed)
    {
        direction = (targetPos - (Vector2)transform.position).normalized;
        speed = projectileSpeed;
        isInitialized = true;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Update()
    {
        if (!isInitialized) return;
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerState>()?.TakeDamage(1f);
            Destroy(gameObject);
        }
    }
}
