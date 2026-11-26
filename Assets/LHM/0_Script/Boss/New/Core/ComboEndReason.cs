namespace UnMachine.Core.Boss
{
    /// <summary>콤보가 끝난 이유 (boss.md 8장).</summary>
    public enum ComboEndReason
    {
        /// <summary>아직 진행 중이거나 시작 전.</summary>
        None,

        /// <summary>모든 단계 완료 → 콤보 반격 틈 (C3 는 긴 틈).</summary>
        Completed,

        /// <summary>C1: 돌진 명중 → 나머지 생략, P2 반격 틈.</summary>
        ChargeHitPlayer,

        /// <summary>C1: 지나치기 전 벽 → Stunned.</summary>
        WallStun,
    }
}
