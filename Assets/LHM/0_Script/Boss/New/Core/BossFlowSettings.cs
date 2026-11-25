namespace UnMachine.Core.Boss
{
    /// <summary>
    /// BossFlowMachine 수치 묶음. 어댑터가 BossFlowConfig(SO).ToSettings() 로 만든다 (Core 는 SO 를 모른다).
    /// 시간은 초, 모두 0 이상 유한값. 길이 0인 상태는 건너뛴다 (BossFlowMachine 참고).
    /// </summary>
    public readonly struct BossFlowSettings
    {
        /// <summary>수치 묶음을 만든다.</summary>
        /// <param name="restSecondsPhase1">1페이즈 휴식. spec: boss.md 5장 1.5</param>
        /// <param name="restSecondsPhase2">2페이즈 휴식. spec: boss.md 5장·8장 0.8</param>
        /// <param name="minTelegraphSeconds">최소 예고. spec: boss.md 9장 0.4</param>
        /// <param name="phase2PunishScale">2페이즈 반격 틈 배율. spec: boss.md 8장 ×0.8</param>
        /// <param name="stunnedSeconds">Stunned 길이. spec: boss.md 5장 1.5</param>
        /// <param name="groggySeconds">Groggy 길이. spec: boss.md 4장·5장 4</param>
        /// <param name="phaseTransitionSeconds">PhaseTransition 길이. 기획서 값 없음 〔임시값〕</param>
        /// <param name="damageScale">상태별 받는 피해 배율.</param>
        public BossFlowSettings(
            float restSecondsPhase1,
            float restSecondsPhase2,
            float minTelegraphSeconds,
            float phase2PunishScale,
            float stunnedSeconds,
            float groggySeconds,
            float phaseTransitionSeconds,
            BossDamageScale damageScale)
        {
            RestSecondsPhase1 = BossFlowMachine.RequireNonNegativeFinite(restSecondsPhase1, nameof(restSecondsPhase1));
            RestSecondsPhase2 = BossFlowMachine.RequireNonNegativeFinite(restSecondsPhase2, nameof(restSecondsPhase2));
            MinTelegraphSeconds =
                BossFlowMachine.RequireNonNegativeFinite(minTelegraphSeconds, nameof(minTelegraphSeconds));
            Phase2PunishScale = BossFlowMachine.RequireNonNegativeFinite(phase2PunishScale, nameof(phase2PunishScale));
            StunnedSeconds = BossFlowMachine.RequireNonNegativeFinite(stunnedSeconds, nameof(stunnedSeconds));
            GroggySeconds = BossFlowMachine.RequireNonNegativeFinite(groggySeconds, nameof(groggySeconds));
            PhaseTransitionSeconds =
                BossFlowMachine.RequireNonNegativeFinite(phaseTransitionSeconds, nameof(phaseTransitionSeconds));
            DamageScale = damageScale;
        }

        /// <summary>1페이즈 휴식(초).</summary>
        public float RestSecondsPhase1 { get; }

        /// <summary>2페이즈 휴식(초).</summary>
        public float RestSecondsPhase2 { get; }

        /// <summary>최소 예고(초).</summary>
        public float MinTelegraphSeconds { get; }

        /// <summary>2페이즈 반격 틈 배율.</summary>
        public float Phase2PunishScale { get; }

        /// <summary>Stunned 길이(초).</summary>
        public float StunnedSeconds { get; }

        /// <summary>Groggy 길이(초).</summary>
        public float GroggySeconds { get; }

        /// <summary>PhaseTransition 길이(초).</summary>
        public float PhaseTransitionSeconds { get; }

        /// <summary>상태별 받는 피해 배율.</summary>
        public BossDamageScale DamageScale { get; }
    }
}
