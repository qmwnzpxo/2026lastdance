using UnityEngine;
using EnemyVersion3.Interfaces;

public class RangerEnemyBTController : EnemyBTController, IMoveable
{
    // ── 이동 ────────────────────────────────────
    [Header("이동")]
    public float _moveSpeed = 2f;
    public float moveSpeed => _moveSpeed;

    // ── 지면 순찰 ───────────────────────────────
    [Header("지면 순찰")]
    [SerializeField] private int _patrolDirection = 1;
    [SerializeField] private float _patrolEdgeCheckX = 0.5f;
    [SerializeField] private float _patrolEdgeCheckY = 1.2f;
    [SerializeField] private float _patrolWallCheckX = 0.4f;
    public int patrolDirection
    {
        get => _patrolDirection;
        set => _patrolDirection = value;
    }
    public float patrolEdgeCheckX => _patrolEdgeCheckX;
    public float patrolEdgeCheckY => _patrolEdgeCheckY;
    public float patrolWallCheckX => _patrolWallCheckX;

    // ── 후퇴 ────────────────────────────────────
    [Header("후퇴")]
    public float _retreatTriggerRange = 0;
    public float _retreatDistance = 0;
    public float retreatTriggerRange
    {
        get => _retreatTriggerRange;
        set => _retreatTriggerRange = value;
    }
    public float retreatDistance
    {
        get => _retreatDistance;
        set => _retreatDistance = value;
    }
    public bool needsRetreat { get; set; }

    protected override void Awake()
    {
        if (_groundLayer == 0)
            _groundLayer = LayerMask.GetMask("ground");
        Debug.Log(_groundLayer.value);
        InitBlackboad();


        _root = BuildTree();
    }

    protected override void InitBlackboad()
    {
        base.InitBlackboad();

        _bb.moveable.retreatTriggerRange = _retreatTriggerRange;
        _bb.moveable.retreatDistance = _retreatDistance;
        GetComponent<EnemyCombat>()?.Initialize(_bb.playerTransform);
    }


    public Vector2 GetSlopeVelocity(float dirX, float speed, LayerMask groundLayer)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.down, _patrolEdgeCheckY, groundLayer);

        Vector2 moveDir = hit.collider != null
            ? Mathf.Sign(dirX) * -Vector2.Perpendicular(hit.normal)
            : new Vector2(Mathf.Sign(dirX), 0f);

        return moveDir * speed;
    }


    // ────────────────────────────────────────
    #region Gizmos

    Color retreatdisColor = new Color(0f, 1f, 0.7f, 1f);
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _retreatTriggerRange);

        Gizmos.color = retreatdisColor;
        Gizmos.DrawWireSphere(transform.position, _retreatDistance);
    }

    #endregion

}
