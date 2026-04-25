using UnityEngine;


namespace EnemyVersion3.Interfaces
{
    public interface IMoveable
    {

        // ── 이동 ────────────────────────────────────
        public float moveSpeed { get; }
        public Transform[] patrolPoints { get; }

        public int currentPatrolIndex { get; set; }
        
        public float retreatTriggerRange { get; set; }
        public float retreatDistance { get; set; }

        public bool needsRetreat { get; set; }
    }
}