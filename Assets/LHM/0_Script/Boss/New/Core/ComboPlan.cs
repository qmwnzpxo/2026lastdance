using System;
using System.Collections.Generic;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 콤보 구성 (boss.md 8장 콤보 표). 단계 = 단독 패턴 단위 목록. 단계 사이 Punishable·Resting 없음, 마지막에만 반격 틈.
    /// 구성은 기획 규칙이라 코드에 둔다 (튜닝 값인 반격 틈은 Driver 인스펙터).
    /// </summary>
    public sealed class ComboPlan
    {
        private static readonly ComboPlan C1Plan =
            new ComboPlan(SelectionUnit.C1, new[] { SelectionUnit.P2, SelectionUnit.P3 }, hasChargeExceptions: true);

        private static readonly ComboPlan C2Plan =
            new ComboPlan(SelectionUnit.C2, new[] { SelectionUnit.P1, SelectionUnit.P5 }, hasChargeExceptions: false);

        private static readonly ComboPlan C3Plan = new ComboPlan(
            SelectionUnit.C3, new[] { SelectionUnit.P1, SelectionUnit.P1, SelectionUnit.P1 }, hasChargeExceptions: false);

        private static readonly ComboPlan C4Plan =
            new ComboPlan(SelectionUnit.C4, new[] { SelectionUnit.P4, SelectionUnit.P2 }, hasChargeExceptions: false);

        private readonly SelectionUnit[] _steps;

        private ComboPlan(SelectionUnit combo, SelectionUnit[] steps, bool hasChargeExceptions)
        {
            Combo = combo;
            _steps = steps;
            HasChargeExceptions = hasChargeExceptions;
        }

        /// <summary>콤보 단위 (C1~C4).</summary>
        public SelectionUnit Combo { get; }

        /// <summary>단계 (단독 패턴 단위, 실행 순서).</summary>
        public IReadOnlyList<SelectionUnit> Steps => _steps;

        /// <summary>
        /// C1 예외 적용 여부: 첫 단계(P2) 명중 → 나머지 생략·P2 반격 틈, 지나치기 전 벽 → Stunned.
        /// 〔임시값〕 boss.md 8장 C1 예외 (Claude 추천, 확정 대기)
        /// </summary>
        public bool HasChargeExceptions { get; }

        /// <summary>콤보 단위의 구성을 찾는다. 단독 패턴(P1~P6)이면 false.</summary>
        public static bool TryGet(SelectionUnit combo, out ComboPlan plan)
        {
            switch (combo)
            {
                case SelectionUnit.C1: plan = C1Plan; return true;
                case SelectionUnit.C2: plan = C2Plan; return true;
                case SelectionUnit.C3: plan = C3Plan; return true;
                case SelectionUnit.C4: plan = C4Plan; return true;
                default: plan = null; return false;
            }
        }

        /// <summary>콤보 단위의 구성. 단독 패턴이면 ArgumentException.</summary>
        public static ComboPlan Get(SelectionUnit combo)
        {
            if (!TryGet(combo, out ComboPlan plan))
            {
                throw new ArgumentException($"{combo} 는 콤보가 아니다.", nameof(combo));
            }

            return plan;
        }
    }
}
