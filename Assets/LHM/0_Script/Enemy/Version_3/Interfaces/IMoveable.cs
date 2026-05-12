using UnityEngine;


namespace EnemyVersion3.Interfaces
{
    public interface IMoveable
    {

        // ── 이동 ────────────────────────────────────
        public float moveSpeed { get; }

        // ── 지면 순찰 ───────────────────────────────
        int patrolDirection { get; set; }   // 1 = 오른쪽, -1 = 왼쪽
        float patrolEdgeCheckX { get; }        // 엣지 감지 전방 오프셋
        float patrolEdgeCheckY { get; }        // 엣지 감지 아래 레이 길이
        float patrolWallCheckX { get; }        // 벽 감지 전방 레이 길이

        // ── 후퇴 ────────────────────────────────────
        public float retreatTriggerRange { get; set; }
        public float retreatDistance { get; set; }
        public bool needsRetreat { get; set; }

        /// <summary>
        /// 발 아래 레이캐스트로 지면 법선을 구한 뒤,
        /// 법선에 수직인 방향(=경사면 방향)으로 이동 속도 벡터를 반환.
        /// 공중이면 수평 이동으로 fallback.
        /// </summary>
        public Vector2 GetSlopeVelocity(float dirX, float speed, LayerMask groundLayer);
    }
}