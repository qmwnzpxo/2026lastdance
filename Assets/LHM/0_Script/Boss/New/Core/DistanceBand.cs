namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 보스와 플레이어의 x 거리 구간 (boss.md 11장). 경계값은 PatternTableConfig 에서 받는다.
    /// </summary>
    public enum DistanceBand
    {
        /// <summary>근거리: x &lt; 근거리 상한 (spec 5).</summary>
        Near,

        /// <summary>중거리: 근거리 상한 ≤ x &lt; 원거리 하한.</summary>
        Mid,

        /// <summary>원거리: x ≥ 원거리 하한 (spec 20).</summary>
        Far,
    }
}
