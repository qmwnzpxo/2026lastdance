using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// P1 착지 충격파 하나의 이동 (boss.md 6장 P1: 좌우로 퍼짐, 속도 12). 앞쪽 끝이 방 경계에 닿으면 소멸.
    /// </summary>
    public sealed class ShockwaveModel
    {
        private float _direction;
        private float _speed;
        private float _halfWidth;
        private float _minX;
        private float _maxX;

        /// <summary>중심 x.</summary>
        public float X { get; private set; }

        /// <summary>경계에 닿아 소멸해야 하는지.</summary>
        public bool IsExpired => _direction > 0f ? X + _halfWidth >= _maxX : X - _halfWidth <= _minX;

        /// <summary>새로 출발시킨다 (풀 재사용 대비 생성자 대신 메서드).</summary>
        /// <param name="startX">시작 중심 x.</param>
        /// <param name="direction">진행 방향 부호 (+ 오른쪽, - 왼쪽). 0 이면 ArgumentException.</param>
        /// <param name="speed">속도 (Unit/초, 0 이상).</param>
        /// <param name="width">폭 (0 이상).</param>
        /// <param name="minX">왼쪽 경계.</param>
        /// <param name="maxX">오른쪽 경계.</param>
        public void Launch(float startX, float direction, float speed, float width, float minX, float maxX)
        {
            if (direction == 0f || float.IsNaN(direction))
            {
                throw new ArgumentException("direction 은 0 이 아니어야 한다.", nameof(direction));
            }

            _direction = direction > 0f ? 1f : -1f;
            _speed = BossFlowMachine.RequireNonNegativeFinite(speed, nameof(speed));
            _halfWidth = BossFlowMachine.RequireNonNegativeFinite(width, nameof(width)) * 0.5f;
            _minX = minX;
            _maxX = maxX;
            X = startX;
        }

        /// <summary>진행 방향 부호 (+1 / -1).</summary>
        public float Direction => _direction;

        /// <summary>이동한다.</summary>
        /// <param name="dt">World 채널 dt.</param>
        public void Tick(float dt)
        {
            X += _direction * _speed * BossFlowMachine.RequireNonNegativeFinite(dt, nameof(dt));
        }
    }
}
