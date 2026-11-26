using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// P1 점프 내려찍기 위치·단계 계산 (boss.md 6장 P1, BACKLOG T45 → T46 통합).
    /// 도약 → 화면 밖에서 그림자 x = 플레이어 x 추적 → 그림자 고정 → 그림자 위치로 낙하 → 착지.
    /// 그림자·보스 x 는 [minX, maxX] 로 클램프한다 (방 밖으로 안 나감).
    /// </summary>
    public sealed class JumpSlamModel
    {
        private readonly float _riseSeconds;
        private readonly float _trackSeconds;
        private readonly float _lockSeconds;
        private readonly float _fallSeconds;

        private float _groundY;
        private float _airY;
        private float _minX;
        private float _maxX;
        private float _phaseElapsed;

        /// <summary>단계 길이(초, 0 이상 유한값)로 만든다.</summary>
        /// <param name="riseSeconds">도약 시간.</param>
        /// <param name="trackSeconds">그림자 추적 시간. spec: 1.2초</param>
        /// <param name="lockSeconds">그림자 고정 시간. spec: 0.4초</param>
        /// <param name="fallSeconds">낙하 시간.</param>
        public JumpSlamModel(float riseSeconds, float trackSeconds, float lockSeconds, float fallSeconds)
        {
            _riseSeconds = BossFlowMachine.RequireNonNegativeFinite(riseSeconds, nameof(riseSeconds));
            _trackSeconds = BossFlowMachine.RequireNonNegativeFinite(trackSeconds, nameof(trackSeconds));
            _lockSeconds = BossFlowMachine.RequireNonNegativeFinite(lockSeconds, nameof(lockSeconds));
            _fallSeconds = BossFlowMachine.RequireNonNegativeFinite(fallSeconds, nameof(fallSeconds));
        }

        /// <summary>현재 단계.</summary>
        public JumpSlamPhase Phase { get; private set; } = JumpSlamPhase.Idle;

        /// <summary>보스 x.</summary>
        public float X { get; private set; }

        /// <summary>보스 y.</summary>
        public float Y { get; private set; }

        /// <summary>그림자(착지 예정) x.</summary>
        public float ShadowX { get; private set; }

        /// <summary>현재 단계 경과(초). 디버그용.</summary>
        public float PhaseElapsed => _phaseElapsed;

        /// <summary>화면 밖에 있는지 (콜라이더 Off·피해 거부 구간). spec: 낙하 시작 시 다시 켬</summary>
        public bool IsOffscreen => Phase == JumpSlamPhase.Tracking || Phase == JumpSlamPhase.Locked;

        /// <summary>그림자를 보여야 하는지.</summary>
        public bool IsShadowVisible =>
            Phase == JumpSlamPhase.Tracking || Phase == JumpSlamPhase.Locked || Phase == JumpSlamPhase.Falling;

        /// <summary>그림자가 고정됐는지 (고정·낙하).</summary>
        public bool IsShadowLocked => Phase == JumpSlamPhase.Locked || Phase == JumpSlamPhase.Falling;

        /// <summary>점프를 시작한다 → Rising.</summary>
        /// <param name="startX">보스 시작 x.</param>
        /// <param name="groundY">보스가 서 있는 y (착지 y).</param>
        /// <param name="airY">화면 밖 높이 y. groundY 보다 낮으면 groundY.</param>
        /// <param name="minX">x 클램프 최소.</param>
        /// <param name="maxX">x 클램프 최대. minX 보다 작으면 ArgumentException.</param>
        public void Start(float startX, float groundY, float airY, float minX, float maxX)
        {
            if (float.IsNaN(startX) || float.IsNaN(groundY) || float.IsNaN(airY)
                || float.IsNaN(minX) || float.IsNaN(maxX))
            {
                throw new ArgumentException("NaN 인자는 허용하지 않는다.");
            }

            if (maxX < minX)
            {
                throw new ArgumentException($"maxX({maxX}) < minX({minX})", nameof(maxX));
            }

            _groundY = groundY;
            _airY = Math.Max(airY, groundY);
            _minX = minX;
            _maxX = maxX;
            _phaseElapsed = 0f;
            X = startX;
            Y = groundY;
            ShadowX = Clamp(startX);
            Phase = JumpSlamPhase.Rising;
        }

        /// <summary>시간을 진행한다. 한 프레임에 여러 단계를 넘어갈 수 있다 (남은 시간 이월).</summary>
        /// <param name="dt">World 채널 dt (0 이상 유한값).</param>
        /// <param name="playerX">플레이어 x (추적 단계에서만 쓴다).</param>
        public void Tick(float dt, float playerX)
        {
            _phaseElapsed += BossFlowMachine.RequireNonNegativeFinite(dt, nameof(dt));

            while (true)
            {
                switch (Phase)
                {
                    case JumpSlamPhase.Rising:
                        Y = Lerp(_groundY, _airY, Progress(_riseSeconds));
                        if (!TryAdvance(_riseSeconds, JumpSlamPhase.Tracking))
                        {
                            return;
                        }

                        Y = _airY;
                        break;

                    case JumpSlamPhase.Tracking:
                        if (!float.IsNaN(playerX))
                        {
                            ShadowX = Clamp(playerX);
                        }

                        X = ShadowX; // 화면 밖에서 그림자 바로 위에 있게
                        if (!TryAdvance(_trackSeconds, JumpSlamPhase.Locked))
                        {
                            return;
                        }

                        break;

                    case JumpSlamPhase.Locked:
                        X = ShadowX;
                        if (!TryAdvance(_lockSeconds, JumpSlamPhase.Falling))
                        {
                            return;
                        }

                        break;

                    case JumpSlamPhase.Falling:
                        X = ShadowX;
                        Y = Lerp(_airY, _groundY, Progress(_fallSeconds));
                        if (!TryAdvance(_fallSeconds, JumpSlamPhase.Landed))
                        {
                            return;
                        }

                        Y = _groundY;
                        break;

                    default:
                        return; // Idle·Landed: 진행 없음
                }
            }
        }

        /// <summary>x 를 방 범위로 클램프한다.</summary>
        public float Clamp(float x)
        {
            return x < _minX ? _minX : (x > _maxX ? _maxX : x);
        }

        private bool TryAdvance(float duration, JumpSlamPhase next)
        {
            if (_phaseElapsed < duration)
            {
                return false;
            }

            _phaseElapsed -= duration;
            Phase = next;
            return true;
        }

        private float Progress(float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            float t = _phaseElapsed / duration;
            return t > 1f ? 1f : t;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
