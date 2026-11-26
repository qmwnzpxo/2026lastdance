using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 플레이어와의 x 거리로 현재 구간과 그 구간에 머문 시간(체류)을 추적한다 (boss.md 11장 체류 보정).
    /// 어댑터가 매 프레임 World 채널 dt 로 Update 를 호출한다.
    /// </summary>
    public sealed class DistanceBandTracker
    {
        private readonly float _nearMaxDistance;
        private readonly float _farMinDistance;

        /// <summary>구간 경계를 정해 만든다.</summary>
        /// <param name="nearMaxDistance">이 값 미만이면 근거리. spec: boss.md 11장 5</param>
        /// <param name="farMinDistance">이 값 이상이면 원거리. spec: boss.md 11장 20</param>
        public DistanceBandTracker(float nearMaxDistance, float farMinDistance)
        {
            _nearMaxDistance = BossFlowMachine.RequireNonNegativeFinite(nearMaxDistance, nameof(nearMaxDistance));
            _farMinDistance = BossFlowMachine.RequireNonNegativeFinite(farMinDistance, nameof(farMinDistance));

            if (_farMinDistance < _nearMaxDistance)
            {
                throw new ArgumentException(
                    $"원거리 하한({farMinDistance})은 근거리 상한({nearMaxDistance}) 이상이어야 한다.",
                    nameof(farMinDistance));
            }
        }

        /// <summary>현재 구간. 첫 Update 전에는 의미 없음 (HasBand 확인).</summary>
        public DistanceBand Band { get; private set; }

        /// <summary>Update 를 한 번이라도 받았는지.</summary>
        public bool HasBand { get; private set; }

        /// <summary>현재 구간에 연속으로 머문 시간(초). 구간이 바뀐 프레임에 0.</summary>
        public float DwellSeconds { get; private set; }

        /// <summary>거리를 구간으로 나눈다. 경계값 자체는 바깥쪽 구간 (5 → 중거리, 20 → 원거리).</summary>
        /// <param name="distanceX">x 거리 절댓값. 0 이상 유한값.</param>
        public DistanceBand Classify(float distanceX)
        {
            float distance = BossFlowMachine.RequireNonNegativeFinite(distanceX, nameof(distanceX));

            if (distance < _nearMaxDistance)
            {
                return DistanceBand.Near;
            }

            return distance < _farMinDistance ? DistanceBand.Mid : DistanceBand.Far;
        }

        /// <summary>이번 프레임 거리를 반영한다. 같은 구간이면 체류 시간을 dt 만큼 늘리고, 바뀌면 0으로 되돌린다.</summary>
        /// <param name="distanceX">x 거리 절댓값. 0 이상 유한값.</param>
        /// <param name="dt">World 채널 dt. 0 이상 유한값.</param>
        public void Update(float distanceX, float dt)
        {
            float step = BossFlowMachine.RequireNonNegativeFinite(dt, nameof(dt));
            DistanceBand band = Classify(distanceX);

            if (!HasBand || band != Band)
            {
                Band = band;
                HasBand = true;
                DwellSeconds = 0f;
                return;
            }

            DwellSeconds += step;
        }

        /// <summary>새 전투 시작 등에서 추적을 처음 상태로 되돌린다.</summary>
        public void Reset()
        {
            HasBand = false;
            Band = DistanceBand.Near;
            DwellSeconds = 0f;
        }
    }
}
