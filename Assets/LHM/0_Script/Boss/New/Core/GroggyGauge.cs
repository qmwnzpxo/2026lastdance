namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 그로기 게이지 (boss.md 7장). 피격으로 오르고, 마지막 증가 후 지연 시간이 지나면 초당 일정량 감소.
    /// 가득 차면 IsFull 유지 (감소·증가 멈춤) → 어댑터가 Groggy 진입 후 Reset.
    /// </summary>
    /// <remarks>1주 축소(T60 ✂️): 출처는 벽 충돌 + 받은 피해량 비례 두 가지만 (어댑터에서 양을 계산해 Add).</remarks>
    public sealed class GroggyGauge
    {
        private readonly float _max;
        private readonly float _decayDelaySeconds;
        private readonly float _decayPerSecond;
        private float _sinceLastAdd;

        /// <summary>게이지를 만든다.</summary>
        /// <param name="max">최대값 (0 초과). spec: boss.md 4장 100</param>
        /// <param name="decayDelaySeconds">마지막 증가 후 감소 시작까지(초). spec: 7장 3초</param>
        /// <param name="decayPerSecond">초당 감소량. spec: 7장 5</param>
        public GroggyGauge(float max, float decayDelaySeconds, float decayPerSecond)
        {
            _max = BossFlowMachine.RequireNonNegativeFinite(max, nameof(max));
            if (_max <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(max), max, "0보다 커야 한다.");
            }

            _decayDelaySeconds = BossFlowMachine.RequireNonNegativeFinite(decayDelaySeconds, nameof(decayDelaySeconds));
            _decayPerSecond = BossFlowMachine.RequireNonNegativeFinite(decayPerSecond, nameof(decayPerSecond));
        }

        /// <summary>현재 값 (0~최대).</summary>
        public float Value { get; private set; }

        /// <summary>0~1 비율 (UI 용).</summary>
        public float Normalized => Value / _max;

        /// <summary>가득 찼는지. Reset 전까지 유지.</summary>
        public bool IsFull => Value >= _max;

        /// <summary>게이지를 올린다. 가득 찬 상태면 무시.</summary>
        /// <param name="amount">0 이상 유한값.</param>
        /// <returns>이번 증가로 처음 가득 찼으면 true.</returns>
        public bool Add(float amount)
        {
            float step = BossFlowMachine.RequireNonNegativeFinite(amount, nameof(amount));
            if (IsFull || step <= 0f)
            {
                return false;
            }

            _sinceLastAdd = 0f;
            Value += step;
            if (Value < _max)
            {
                return false;
            }

            Value = _max;
            return true;
        }

        /// <summary>시간 진행: 지연 시간이 지나면 감소. 가득 찬 상태면 아무것도 안 함.</summary>
        /// <param name="dt">World 채널 dt. 0 이상 유한값.</param>
        public void Tick(float dt)
        {
            float step = BossFlowMachine.RequireNonNegativeFinite(dt, nameof(dt));
            if (IsFull || Value <= 0f)
            {
                return;
            }

            _sinceLastAdd += step;
            float decayTime = _sinceLastAdd - _decayDelaySeconds;
            if (decayTime <= 0f)
            {
                return;
            }

            // 이번 프레임 중 감소 구간에 해당하는 시간만큼만 뺀다.
            float decaying = decayTime < step ? decayTime : step;
            Value -= decaying * _decayPerSecond;
            if (Value < 0f)
            {
                Value = 0f;
            }
        }

        /// <summary>0으로 되돌린다 (Groggy 종료, 새 전투).</summary>
        public void Reset()
        {
            Value = 0f;
            _sinceLastAdd = 0f;
        }
    }
}
