namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 패턴 실행 결과. 결과에 따라 반격 틈 길이가 달라지는 패턴을 위해 쓴다.
    /// 예: P2 돌진이 플레이어를 맞히면 0.6초 (boss.md 6장 P2), C3 마지막 착지만 긴 틈 (8장).
    /// </summary>
    public readonly struct PatternResult
    {
        private PatternResult(float punishOverrideSeconds)
        {
            HasPunishOverride = true;
            PunishOverrideSeconds = punishOverrideSeconds;
        }

        /// <summary>PatternTiming 의 기본 반격 틈을 그대로 쓴다.</summary>
        public static PatternResult Default => default;

        /// <summary>반격 틈 길이를 이 값으로 바꾼다 (1페이즈 기준, 0 이상 유한값). 0이면 반격 틈 없이 휴식으로.</summary>
        public static PatternResult WithPunish(float punishableSeconds)
        {
            return new PatternResult(
                BossFlowMachine.RequireNonNegativeFinite(punishableSeconds, nameof(punishableSeconds)));
        }

        /// <summary>반격 틈 길이를 덮어쓰는지.</summary>
        public bool HasPunishOverride { get; }

        /// <summary>덮어쓸 반격 틈 길이(초). HasPunishOverride 가 false 면 의미 없음.</summary>
        public float PunishOverrideSeconds { get; }

        /// <summary>이 결과에 적용할 반격 틈 길이(1페이즈 기준).</summary>
        internal float ResolvePunishSeconds(PatternTiming timing)
        {
            return HasPunishOverride ? PunishOverrideSeconds : timing.PunishableSeconds;
        }
    }
}
