using System;

namespace UnMachine.Core
{
    /// <summary>
    /// GameClock 의 시간 채널 하나. 채널마다 배율(Scale)이 따로 있고,
    /// GameClock.Tick 이 매 프레임 이 채널의 DeltaTime 을 갱신한다. (ARCHITECTURE 3-1)
    /// </summary>
    public sealed class ClockChannel
    {
        private const float DefaultScale = 1f;

        private float _scale = DefaultScale;

        internal ClockChannel(string name, bool isAffectedByHitStop)
        {
            Name = name;
            IsAffectedByHitStop = isAffectedByHitStop;
        }

        /// <summary>디버그 표시용 채널 이름.</summary>
        public string Name { get; }

        /// <summary>히트스톱 중 이 채널을 멈추는지. World·Player = true, UI = false.</summary>
        public bool IsAffectedByHitStop { get; }

        /// <summary>
        /// 채널 배율. 0 = 정지, 1 = 기본. 음수·NaN·무한대는 거부한다.
        /// 바뀐 값은 다음 GameClock.Tick 부터 DeltaTime 에 반영된다.
        /// </summary>
        public float Scale
        {
            get => _scale;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"{Name} Scale({value})은 0 이상 유한값이어야 한다.");
                }

                _scale = value;
            }
        }

        /// <summary>마지막 Tick 기준 이 채널의 dt (실제 dt × Scale, 히트스톱 구간 제외).</summary>
        public float DeltaTime { get; private set; }

        /// <summary>
        /// 이번 프레임 dt 를 계산한다. frozenDt = 이번 프레임 중 히트스톱으로 멈춘 실제 시간.
        /// 히트스톱이 프레임 중간에 끝나면 남은 부분만 진행시켜 정지 길이가 프레임 단위로 늘어나지 않게 한다.
        /// </summary>
        internal void Advance(float realDt, float frozenDt)
        {
            float activeDt = IsAffectedByHitStop ? realDt - frozenDt : realDt;
            DeltaTime = activeDt * _scale;
        }

        internal void Reset()
        {
            _scale = DefaultScale;
            DeltaTime = 0f;
        }
    }
}
