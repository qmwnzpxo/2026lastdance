namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 상태별 받는 피해 배율 (boss.md 4장 "받는 피해 배율"). 〔임시값: 배율 3단계〕
    /// 상태는 하나만 활성이라 배율이 겹치지 않는다.
    /// </summary>
    public readonly struct BossDamageScale
    {
        /// <summary>배율을 만든다. 모두 0 이상 유한값.</summary>
        /// <param name="normal">기본 배율. spec: ×1.0</param>
        /// <param name="vulnerable">Punishable·Stunned 배율. spec: ×1.2 (결정 #12)</param>
        /// <param name="groggy">Groggy 배율. spec: ×1.5</param>
        public BossDamageScale(float normal, float vulnerable, float groggy)
        {
            Normal = BossFlowMachine.RequireNonNegativeFinite(normal, nameof(normal));
            Vulnerable = BossFlowMachine.RequireNonNegativeFinite(vulnerable, nameof(vulnerable));
            Groggy = BossFlowMachine.RequireNonNegativeFinite(groggy, nameof(groggy));
        }

        /// <summary>기본 배율.</summary>
        public float Normal { get; }

        /// <summary>Punishable·Stunned 배율.</summary>
        public float Vulnerable { get; }

        /// <summary>Groggy 배율.</summary>
        public float Groggy { get; }

        /// <summary>
        /// 상태에 맞는 배율. 무적 상태(PhaseTransition·Dead)도 기본 배율을 돌려준다 — 무적은 BossController(T33)가 막는다.
        /// </summary>
        public float ForState(BossFlowState state)
        {
            switch (state)
            {
                case BossFlowState.Punishable:
                case BossFlowState.Stunned:
                    return Vulnerable;
                case BossFlowState.Groggy:
                    return Groggy;
                default:
                    return Normal;
            }
        }
    }
}
