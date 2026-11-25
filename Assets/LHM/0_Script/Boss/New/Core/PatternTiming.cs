namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 패턴 하나의 기본 타이밍 (boss.md 6장 패턴별 값). 1페이즈 기준 값을 넣는다.
    /// 최소 예고 보장과 2페이즈 반격 틈 배율은 BossFlowMachine 이 적용한다.
    /// </summary>
    public readonly struct PatternTiming
    {
        /// <summary>타이밍을 만든다. 두 값 모두 0 이상 유한값(초).</summary>
        /// <param name="telegraphSeconds">예고 길이. 최소값보다 짧으면 최소값이 적용된다.</param>
        /// <param name="punishableSeconds">패턴이 끝난 뒤 반격 틈 길이. 0이면 반격 틈 없이 휴식으로.</param>
        public PatternTiming(float telegraphSeconds, float punishableSeconds)
        {
            TelegraphSeconds = BossFlowMachine.RequireNonNegativeFinite(telegraphSeconds, nameof(telegraphSeconds));
            PunishableSeconds = BossFlowMachine.RequireNonNegativeFinite(punishableSeconds, nameof(punishableSeconds));
        }

        /// <summary>요청한 예고 길이(초).</summary>
        public float TelegraphSeconds { get; }

        /// <summary>기본 반격 틈 길이(초, 1페이즈 기준).</summary>
        public float PunishableSeconds { get; }
    }
}
