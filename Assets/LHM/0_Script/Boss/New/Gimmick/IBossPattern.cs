using System.Threading;
using Cysharp.Threading.Tasks;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 보스 패턴 하나. BossFlowDriver 가 Telegraph 길이·반격 틈을 Timing 에서 읽고,
    /// Attacking 상태에서 RunAsync 를 실행한 뒤 결과를 BossFlowMachine.NotifyAttackEnded 로 넘긴다.
    /// spec: boss.md 5장 흐름, BACKLOG Q19 C
    /// </summary>
    /// <remarks>
    /// 인자는 지금 BossController 하나다. 패턴 전용 컨텍스트 타입은 실제 패턴을 구현할 때(T37 이후) 만든다.
    /// 구 스테이지의 BossSystem.IBossPattern 과는 별개 타입이다 (네임스페이스가 다름).
    /// </remarks>
    public interface IBossPattern
    {
        /// <summary>디버그 표시용 이름.</summary>
        string DebugName { get; }

        /// <summary>예고·반격 틈 (1페이즈 기준, 2페이즈 배율은 상태 머신이 적용).</summary>
        PatternTiming Timing { get; }

        /// <summary>
        /// 패턴을 실행한다. 인터럽트(Stunned·Groggy·사망 등)로 끊기면 ct 가 취소된다.
        /// 취소되면 OperationCanceledException 으로 끝내고, 패턴이 남긴 상태(코루틴·속도 등)는 정리한다.
        /// </summary>
        /// <returns>패턴 결과 (반격 틈 덮어쓰기 등).</returns>
        UniTask<PatternResult> RunAsync(BossController boss, CancellationToken ct);
    }
}
