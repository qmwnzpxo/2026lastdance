using UnityEngine;
using EnemyVersion3.Interfaces;

public class RangerEnemyBTController : EnemyBTController, IMoveable
{
    // ── 이동 ────────────────────────────────────
    [Header("이동")]
    public float _moveSpeed = 2f;
    public Transform[] _patrolPoints;
    public int _currentPatrolIndex = 0;

    public float moveSpeed => _moveSpeed;
    public Transform[] patrolPoints => _patrolPoints;
    public int currentPatrolIndex
    {
        get => _currentPatrolIndex;
        set => _currentPatrolIndex = value;
    }

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
        InitBlackboad();

        _bb.moveable.retreatTriggerRange = _retreatTriggerRange;
        _bb.moveable.retreatDistance = _retreatDistance;
        GetComponent<EnemyCombat>()?.Initialize(_bb.playerTransform);


        _root = BuildTree();
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
