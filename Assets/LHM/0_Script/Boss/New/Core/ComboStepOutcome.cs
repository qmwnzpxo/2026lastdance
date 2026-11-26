namespace UnMachine.Core.Boss
{
    /// <summary>콤보 단계 하나의 결과. C1 예외 분기에 쓴다 (boss.md 8장).</summary>
    public enum ComboStepOutcome
    {
        /// <summary>정상 종료.</summary>
        Completed,

        /// <summary>돌진이 플레이어를 맞힘.</summary>
        HitPlayer,

        /// <summary>플레이어를 지나치기 전에 벽에 닿음.</summary>
        HitWall,
    }
}
