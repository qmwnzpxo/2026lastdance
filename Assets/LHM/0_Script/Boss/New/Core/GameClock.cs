using System;

namespace UnMachine.Core
{
    /// <summary>
    /// 게임 시간 채널 묶음 (ARCHITECTURE 3-1). 로직은 Time.deltaTime 대신 자기 채널의 DeltaTime 을 쓴다.
    /// 정적 접근을 허용한다 (ARCHITECTURE 2-3). 매 프레임 Tick 은 어댑터(GameClockDriver, T28) 하나만 호출한다.
    /// </summary>
    /// <remarks>
    /// 정적 상태이므로 도메인 리로드를 끈 에디터에서는 이전 플레이 값이 남는다 → 어댑터가 시작 시 Reset 을 호출한다.
    /// 일시정지는 Time.timeScale = 0 으로 처리하고 이 클래스는 다루지 않는다 (ARCHITECTURE 3-1).
    /// </remarks>
    public static class GameClock
    {
        /// <summary>세계 채널: 적·보스·발사체. 히트스톱 중 정지.</summary>
        public static ClockChannel World { get; } = new ClockChannel("World", isAffectedByHitStop: true);

        /// <summary>플레이어 채널. 히트스톱 중 정지.</summary>
        public static ClockChannel Player { get; } = new ClockChannel("Player", isAffectedByHitStop: true);

        /// <summary>UI 채널. 히트스톱 중에도 진행.</summary>
        public static ClockChannel UI { get; } = new ClockChannel("UI", isAffectedByHitStop: false);

        /// <summary>남은 히트스톱 시간(실제 초). 디버그 표시용.</summary>
        public static float HitStopRemaining { get; private set; }

        /// <summary>히트스톱이 남아 있는지.</summary>
        public static bool IsHitStopped => HitStopRemaining > 0f;

        /// <summary>
        /// HitStop 요청으로 종료 시각이 늦춰졌을 때 발생한다. 인자 = 새 남은 시간(실제 초).
        /// 어댑터(GameClockDriver)가 구독해 기존 HitstopManager 에 위임한다 (BACKLOG Q20·Q25).
        /// </summary>
        public static event Action<float> HitStopExtended;

        /// <summary>
        /// 히트스톱을 요청한다. 진행 중인 히트스톱과 겹치면 끝나는 시각이 늦은 쪽을 유지한다(누적하지 않음).
        /// spec: boss.md 5장 Hit (0.05초), ARCHITECTURE 3-1 "히트스톱 중첩(긴 쪽 유지)"
        /// </summary>
        /// <param name="seconds">실제 시간 기준 정지 길이(초). 0 이상 유한값.</param>
        /// <returns>이번 요청으로 종료 시각이 늦춰졌으면 true. 기존 히트스톱이 더 길면 false.</returns>
        public static bool HitStop(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), $"HitStop({seconds})은 0 이상 유한값이어야 한다.");
            }

            if (seconds <= HitStopRemaining)
            {
                return false;
            }

            HitStopRemaining = seconds;
            HitStopExtended?.Invoke(seconds);
            return true;
        }

        /// <summary>
        /// 프레임마다 1회 호출해서 히트스톱 타이머를 줄이고 채널별 DeltaTime 을 갱신한다.
        /// 히트스톱 타이머는 채널 배율과 무관하게 실제 시간으로 줄어든다.
        /// </summary>
        /// <param name="realDt">이번 프레임의 실제 경과 시간(초). 0 이상 유한값.</param>
        public static void Tick(float realDt)
        {
            if (float.IsNaN(realDt) || float.IsInfinity(realDt) || realDt < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(realDt), $"Tick({realDt})은 0 이상 유한값이어야 한다.");
            }

            float frozenDt = Math.Min(HitStopRemaining, realDt);
            HitStopRemaining -= frozenDt;

            World.Advance(realDt, frozenDt);
            Player.Advance(realDt, frozenDt);
            UI.Advance(realDt, frozenDt);
        }

        /// <summary>
        /// 모든 채널 배율을 1, dt 를 0 으로, 히트스톱을 해제한다. 씬(플레이) 시작 시 어댑터가 호출한다.
        /// HitStopExtended 구독은 건드리지 않는다 (구독자가 OnDisable 에서 해제).
        /// </summary>
        public static void Reset()
        {
            HitStopRemaining = 0f;
            World.Reset();
            Player.Reset();
            UI.Reset();
        }
    }
}
