using UnityEngine;

namespace CSHPlayer
{
    public struct GhostMoveData
    {
        public float timestamp;
        public Vector2 position;
        public Vector3 spriteScale;
        public Quaternion spriteRotation;
        public bool isRunning;
        public bool flipX;
        public bool isPowerRunning;
    }

    public enum GhostEventType { Attack, Jump, Item }
    public struct GhostEventData
    {
        public float timestamp;
        public GhostEventType eventType;

        public Quaternion rotation;

        public int itemId;
        public Vector2 eventPosition;
    }
    
    public class EnemySnapshotData
    {
        public EnemyBTController controller;
        public EnemyState state;

        // Transform
        public Vector2 position;

        // EnemyState
        public float hp;

        // EnemyBlackboard
        public bool isDetected;
        public float detectGauge;
        public float chaseEndTimer;
        public bool isStunned;
        public float lastAttackTime;
    }
}