using System;
using UnityEngine;

namespace UnMachine.Boss
{
    /// <summary>
    /// Resting 중 플레이어 쪽으로 걷는 이동 어댑터 (BACKLOG T32). BossFlowDriver 가 만들고 호출한다.
    /// Update 에서 목표 x 속도를 정하고(World 배율), FixedUpdate 에서 Rigidbody2D 에 반영한다.
    /// </summary>
    /// <remarks>
    /// 방향 전환(좌우 뒤집기)은 기존 BossController.FacePlayer 가 매 프레임 처리한다.
    /// 걷기 애니메이션(runboost 0.5배속)은 T54. y 속도(중력)는 건드리지 않는다.
    /// </remarks>
    public sealed class BossLocomotion
    {
        private readonly Rigidbody2D _body;
        private readonly BossController _boss;
        private readonly float _walkSpeed;
        private readonly float _stopDistance;

        private bool _isActive;
        private float _targetVelocityX;

        /// <param name="body">보스 Rigidbody2D.</param>
        /// <param name="boss">플레이어 참조(GetPlayer)를 얻을 보스.</param>
        /// <param name="walkSpeed">걷는 속도 (Unit/초, 0 이상).</param>
        /// <param name="stopDistance">플레이어와 x 거리가 이 값 이하면 정지 (0 이상).</param>
        public BossLocomotion(Rigidbody2D body, BossController boss, float walkSpeed, float stopDistance)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (boss == null)
            {
                throw new ArgumentNullException(nameof(boss));
            }

            if (walkSpeed < 0f || stopDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), "걷기 속도·정지 거리는 0 이상이어야 한다.");
            }

            _body = body;
            _boss = boss;
            _walkSpeed = walkSpeed;
            _stopDistance = stopDistance;
        }

        /// <summary>걷기 제어 중인지 (Resting 진입 ~ Stop).</summary>
        public bool IsActive => _isActive;

        /// <summary>마지막으로 정한 목표 x 속도 (디버그 표시용).</summary>
        public float TargetVelocityX => _targetVelocityX;

        /// <summary>마지막으로 잰 플레이어까지 x 거리 (플레이어 − 보스, 디버그 표시용).</summary>
        public float DistanceX { get; private set; }

        /// <summary>
        /// 걷기 제어를 시작한다. 목표 속도 0 으로 시작 → 다음 FixedUpdate 에서 패턴이 남긴 x 속도도 지운다.
        /// </summary>
        public void Begin()
        {
            _isActive = true;
            _targetVelocityX = 0f;
        }

        /// <summary>
        /// 이번 프레임 목표 x 속도를 정한다 (Update). 실제 반영은 <see cref="ApplyFixed"/>.
        /// </summary>
        /// <param name="worldRate">World 채널 진행 비율. 히트스톱 중 0, 평소 World 배율.</param>
        public void Tick(float worldRate)
        {
            if (!_isActive)
            {
                return;
            }

            Transform player = _boss.GetPlayer();
            if (player == null)
            {
                _targetVelocityX = 0f;
                return;
            }

            DistanceX = player.position.x - _body.position.x;
            _targetVelocityX = ComputeWalkVelocityX(DistanceX, _walkSpeed, _stopDistance) * worldRate;
        }

        /// <summary>목표 x 속도를 Rigidbody2D 에 반영한다 (FixedUpdate). y 속도는 유지.</summary>
        public void ApplyFixed()
        {
            if (!_isActive)
            {
                return;
            }

            _body.linearVelocity = new Vector2(_targetVelocityX, _body.linearVelocity.y);
        }

        /// <summary>걷기를 끝내고 x 속도를 즉시 0 으로. 제어 중이 아니면 아무것도 하지 않는다.</summary>
        public void Stop()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _targetVelocityX = 0f;
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        }

        /// <summary>
        /// 걷기 x 속도 (배율 적용 전). |거리| ≤ 정지 거리면 0, 아니면 플레이어 쪽으로 걷기 속도.
        /// 가까워지면 멈추기만 하고 뒤로 가지 않으므로 경계에서 떨지 않는다.
        /// </summary>
        /// <param name="distanceX">플레이어 x − 보스 x.</param>
        /// <param name="walkSpeed">걷는 속도 (Unit/초).</param>
        /// <param name="stopDistance">정지 거리 (Unit).</param>
        public static float ComputeWalkVelocityX(float distanceX, float walkSpeed, float stopDistance)
        {
            if (Mathf.Abs(distanceX) <= stopDistance)
            {
                return 0f;
            }

            return Mathf.Sign(distanceX) * walkSpeed;
        }
    }
}
