using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 콤보 하나를 패턴 하나로 실행한다 (BACKLOG T56). 예고는 콤보 시작 1회, 단계 패턴은 틈 없이 이어서 실행,
    /// 반격 틈은 마지막에만 (Timing). 상태 머신 입장에서는 Attacking 이 길어질 뿐이다. spec: boss.md 8장
    /// </summary>
    /// <remarks>
    /// 단계 결과는 <see cref="IComboStepOutcomeSource"/> 를 구현한 단계만 보고한다 (레거시 돌진, T57). 나머지는 Completed.
    /// C1 Brake·C4 발사 직후 돌진은 Driver 가 옵션을 켠 단계 패턴을 넘겨서 처리한다.
    /// 인터럽트로 ct 가 취소되면 진행 중 단계 패턴이 스스로 정리하고 취소 예외가 그대로 올라간다.
    /// </remarks>
    public sealed class ComboPattern : IBossPattern
    {
        private readonly ComboPlan _plan;
        private readonly IBossPattern[] _steps;
        private readonly float _chargeHitPunishSeconds;
        private readonly ComboRunner _runner = new ComboRunner();

        /// <summary>콤보 패턴을 만든다.</summary>
        /// <param name="plan">콤보 구성.</param>
        /// <param name="steps">단계별 실행 패턴 (plan.Steps 와 같은 길이, null 없음).</param>
        /// <param name="timing">예고(콤보 시작) · 반격 틈(마지막 단계 뒤).</param>
        public ComboPattern(ComboPlan plan, IBossPattern[] steps, PatternTiming timing)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (steps == null || steps.Length != plan.Steps.Count)
            {
                throw new ArgumentException($"{plan.Combo} 단계 수가 맞지 않는다.", nameof(steps));
            }

            _steps = (IBossPattern[])steps.Clone();
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] == null)
                {
                    throw new ArgumentException($"{plan.Combo} 단계 {i} 패턴이 null 이다.", nameof(steps));
                }
            }

            // C1 명중 시 P2 반격 틈 (boss.md 8장 C1 예외) — 첫 단계 패턴의 값
            _chargeHitPunishSeconds = _steps[0].Timing.PunishableSeconds;
            Timing = timing;
            DebugName = $"Combo:{plan.Combo}";
        }

        /// <inheritdoc />
        public string DebugName { get; }

        /// <inheritdoc />
        public PatternTiming Timing { get; }

        /// <inheritdoc />
        public async UniTask<PatternResult> RunAsync(BossController boss, CancellationToken ct)
        {
            if (boss == null)
            {
                throw new ArgumentNullException(nameof(boss));
            }

            _runner.Start(_plan);
            ComboStepOutcome outcome;
            do
            {
                ct.ThrowIfCancellationRequested();
                int index = _runner.StepIndex;
                GameLog.Info(
                    $"[Combo {_plan.Combo}] 단계 {index + 1}/{_steps.Length} {_runner.CurrentStep} " +
                    $"({_steps[index].DebugName})",
                    boss);

                // 단계 자체의 반격 틈 덮어쓰기는 무시한다 (단계 사이 틈 없음, 17장 #9)
                IBossPattern step = _steps[index];
                await step.RunAsync(boss, ct);
                outcome = step is IComboStepOutcomeSource source ? source.LastOutcome : ComboStepOutcome.Completed;
            }
            while (_runner.ReportStep(outcome));

            GameLog.Info($"[Combo {_plan.Combo}] 끝 ({_runner.EndReason})", boss);
            switch (_runner.EndReason)
            {
                case ComboEndReason.ChargeHitPlayer:
                case ComboEndReason.WallStun:
                    // 명중: P2 반격 틈으로 끝. 벽: 레거시 돌진이 이미 스턴(chargeStunDuration)을 했으므로
                    // 단독 P2 벽 충돌과 같게 P2 틈으로 끝낸다 (v2 Stunned 는 축소안에서 쓰지 않음, T61 메모)
                    return PatternResult.WithPunish(_chargeHitPunishSeconds);
                default:
                    return PatternResult.Default;
            }
        }
    }
}
