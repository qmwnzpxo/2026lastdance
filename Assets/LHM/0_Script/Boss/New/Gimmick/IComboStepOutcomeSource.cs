using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 콤보 단계로 실행된 패턴이 마지막 실행 결과(명중·벽)를 알려준다. ComboPattern 이 C1 예외 분기에 쓴다. (T57)
    /// 구현하지 않은 패턴은 항상 Completed 로 본다.
    /// </summary>
    public interface IComboStepOutcomeSource
    {
        /// <summary>마지막 RunAsync 결과. RunAsync 가 정상 종료된 직후에만 의미 있다.</summary>
        ComboStepOutcome LastOutcome { get; }
    }
}
