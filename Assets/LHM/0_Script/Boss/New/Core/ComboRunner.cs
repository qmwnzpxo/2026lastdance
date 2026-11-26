using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 콤보 단계 진행기 (BACKLOG T55). Start → (현재 단계 실행 → ReportStep) 반복 → IsFinished.
    /// 단계 사이에는 반격 틈·휴식이 없다 (어댑터가 바로 다음 단계 실행). spec: boss.md 8장, 17장 #9·#10
    /// </summary>
    public sealed class ComboRunner
    {
        private ComboPlan _plan;

        /// <summary>현재 콤보. 시작 전 null.</summary>
        public ComboPlan Plan => _plan;

        /// <summary>현재 단계 번호 (0부터).</summary>
        public int StepIndex { get; private set; }

        /// <summary>끝난 이유. 진행 중이면 None.</summary>
        public ComboEndReason EndReason { get; private set; }

        /// <summary>진행 중인지.</summary>
        public bool IsRunning => _plan != null && EndReason == ComboEndReason.None;

        /// <summary>끝났는지.</summary>
        public bool IsFinished => EndReason != ComboEndReason.None;

        /// <summary>지금 실행할 단계. 진행 중이 아니면 InvalidOperationException.</summary>
        public SelectionUnit CurrentStep
        {
            get
            {
                if (!IsRunning)
                {
                    throw new InvalidOperationException("콤보가 진행 중이 아니다.");
                }

                return _plan.Steps[StepIndex];
            }
        }

        /// <summary>콤보를 시작한다 (첫 단계부터).</summary>
        public void Start(ComboPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            StepIndex = 0;
            EndReason = ComboEndReason.None;
        }

        /// <summary>현재 단계 결과를 알리고 다음 단계로 간다.</summary>
        /// <returns>다음 단계가 있으면 true, 콤보가 끝났으면 false (EndReason 확인).</returns>
        public bool ReportStep(ComboStepOutcome outcome)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("콤보가 진행 중이 아니다.");
            }

            // C1 예외: 첫 단계 돌진의 결과로 분기. spec: boss.md 8장 C1 예외 〔임시값〕
            if (_plan.HasChargeExceptions && StepIndex == 0 && _plan.Steps[0] == SelectionUnit.P2)
            {
                if (outcome == ComboStepOutcome.HitPlayer)
                {
                    EndReason = ComboEndReason.ChargeHitPlayer;
                    return false;
                }

                if (outcome == ComboStepOutcome.HitWall)
                {
                    EndReason = ComboEndReason.WallStun;
                    return false;
                }
            }

            StepIndex++;
            if (StepIndex >= _plan.Steps.Count)
            {
                EndReason = ComboEndReason.Completed;
                return false;
            }

            return true;
        }
    }
}
