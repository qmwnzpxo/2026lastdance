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

    protected override void Awake()
    {
        base.Awake();
    }





}
