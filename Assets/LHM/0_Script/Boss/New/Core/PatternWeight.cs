namespace UnMachine.Core.Boss
{
    /// <summary>선택 단위 하나와 그 가중치 (boss.md 11장 표의 한 칸).</summary>
    public readonly struct PatternWeight
    {
        /// <summary>가중치 항목을 만든다.</summary>
        /// <param name="unit">선택 단위.</param>
        /// <param name="weight">가중치. 0 이상 유한값 (0 = 후보에서 빠짐).</param>
        public PatternWeight(SelectionUnit unit, float weight)
        {
            Unit = unit;
            Weight = BossFlowMachine.RequireNonNegativeFinite(weight, nameof(weight));
        }

        /// <summary>선택 단위.</summary>
        public SelectionUnit Unit { get; }

        /// <summary>가중치.</summary>
        public float Weight { get; }

        /// <summary>디버그 표시용 "P3(5)" 형태.</summary>
        public override string ToString()
        {
            return $"{Unit}({Weight:0.##})";
        }
    }
}
