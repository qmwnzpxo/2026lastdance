using UnityEngine;

namespace CSHPlayer
{
    public enum GhostState
    {
        Running,
        PowerRunning,
        Attack,
        Jump,
        Idle
    }
    public struct GhostMoveData
    {
        public float timestamp;
        public Vector2 position;
        public bool flipX;

        public Vector3 spriteScale;
        public Quaternion spriteRotation;
        public bool isRunning;
        public bool isPowerRunning;
    }

    public struct GhostAttackData
    {
        public int moveFrameIndex;
        public Quaternion rotation;
    }
}