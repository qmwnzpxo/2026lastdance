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
        // 공격: 기록 당시 땅(천장) 공격이었는지. 재생 때 공격 길이를 기록과 같게 맞춰 공격끼리 겹치지 않게 함
        public bool groundAttack;

        public int itemId;
        public Vector2 eventPosition;
    }
    
    public class EnemySnapshotData
    {
        public EnemyBTController controller;
        public EnemyState state;

        // Transform
        public Vector2 position;

        // Rigidbody2D (없으면 hasRigidbody = false)
        public bool hasRigidbody;
        public Vector2 velocity;

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