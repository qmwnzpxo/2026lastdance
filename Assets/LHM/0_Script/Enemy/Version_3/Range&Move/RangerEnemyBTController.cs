using UnityEngine;
using EnemyVersion3.Interfaces;

[RequireComponent(typeof(EnemyArmAim))]
public class RangerEnemyBTController : EnemyBTController, IMoveable
{
    // ── 이동 ────────────────────────────────────
    [Header("이동")]
    public float _moveSpeed = 2f;

    // ── 지면 순찰 ───────────────────────────────
    [Header("지면 순찰")]
    [SerializeField] private int _patrolDirection = 1;
    [SerializeField] private float _patrolEdgeCheckX = 0.5f;
    [SerializeField] private float _patrolEdgeCheckY = 1.2f;
    [SerializeField] private float _patrolWallCheckX = 0.4f;

    // ── 후퇴 ────────────────────────────────────
    [Header("후퇴")]
    public float _retreatTriggerRange = 0;
    public float _retreatDistance = 0;

    // ── 팔 회전 ─────────────────────────────────
    protected EnemyArmAim _armAim;

    // ── 외부 API ────────────────────────────────
    public float moveSpeed => _moveSpeed;
    public float patrolEdgeCheckX => _patrolEdgeCheckX;
    public float patrolEdgeCheckY => _patrolEdgeCheckY;
    public float patrolWallCheckX => _patrolWallCheckX;
    public int patrolDirection { get => _patrolDirection;
        set => _patrolDirection = value; }
    public float retreatTriggerRange { get => _retreatTriggerRange;
        set => _retreatTriggerRange = value; }
    public float retreatDistance { get => _retreatDistance;
        set => _retreatDistance = value; }
    public bool needsRetreat { get; set; }


    protected override void InitBlackboad()
    {
        base.InitBlackboad();

        _bb.moveable.retreatTriggerRange = _retreatTriggerRange;
        _bb.moveable.retreatDistance = _retreatDistance;
        GetComponent<EnemyCombat>()?.Initialize(_bb.playerTransform);
    }

    public override void ReInit()
    {
        base.ReInit();
        _armAim=GetComponent<EnemyArmAim>();
        _armAim.EnemyBlackBoard = _bb;
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

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.forward);

        DrawGroundCheckGizmos();
    }

    private void DrawGroundCheckGizmos()
    {
        Vector3 pos = transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f); // 주황
        Gizmos.DrawRay(pos, Vector3.right * _patrolWallCheckX);
        Gizmos.DrawRay(pos, Vector3.left * _patrolWallCheckX);

        Gizmos.DrawWireSphere(pos + Vector3.right * _patrolWallCheckX, 0.05f);
        Gizmos.DrawWireSphere(pos + Vector3.left * _patrolWallCheckX, 0.05f);

        // ── 절벽 감지 (좌우 발판 끝 확인) ────────────────────────
        Gizmos.color = new Color(0f, 0.8f, 1f, 1f); // 하늘색

        Vector3 rightEdgeOrigin = pos + Vector3.right * _patrolEdgeCheckX;
        Gizmos.DrawWireSphere(rightEdgeOrigin, 0.05f);
        Gizmos.DrawRay(rightEdgeOrigin, Vector3.down * _patrolEdgeCheckY);

        Vector3 leftEdgeOrigin = pos + Vector3.left * _patrolEdgeCheckX;
        Gizmos.DrawWireSphere(leftEdgeOrigin, 0.05f);
        Gizmos.DrawRay(leftEdgeOrigin, Vector3.down * _patrolEdgeCheckY);

        // 절벽 감지 끝점
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(rightEdgeOrigin + Vector3.down * _patrolEdgeCheckY, 0.05f);
        Gizmos.DrawWireSphere(leftEdgeOrigin + Vector3.down * _patrolEdgeCheckY, 0.05f);
    }

    #endregion

}
