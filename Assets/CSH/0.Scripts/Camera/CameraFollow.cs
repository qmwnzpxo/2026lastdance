using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    private PlayerMove player;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);

    private void Awake()
    {
        if (target != null)
            player = target.GetComponent<PlayerMove>();
        else if (player == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            player = target.GetComponent<PlayerMove>();
        }
    }

    void LateUpdate()
    {
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}