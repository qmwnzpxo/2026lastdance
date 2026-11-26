namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 패턴 선택기(PatternSelector)가 고르는 단위 (boss.md 11장). 단독 패턴 P1~P6, 콤보 C1~C4.
    /// 콤보는 단위 하나로 센다 — 반복 방지는 콤보 안의 패턴 반복을 세지 않는다 (17장 #10).
    /// </summary>
    /// <remarks>인스펙터에 직렬화되는 값이므로 새 값은 끝에만 추가한다.</remarks>
    public enum SelectionUnit
    {
        /// <summary>부스터 점프 내려찍기.</summary>
        P1,

        /// <summary>질주 돌진.</summary>
        P2,

        /// <summary>짓밟기.</summary>
        P3,

        /// <summary>포드 곡사 일제 사격.</summary>
        P4,

        /// <summary>포드 직사 연사.</summary>
        P5,

        /// <summary>포드 드론 사출 (2페이즈 전용).</summary>
        P6,

        /// <summary>콤보 P2 → Brake → P3.</summary>
        C1,

        /// <summary>콤보 P1 → P5.</summary>
        C2,

        /// <summary>콤보 P1 ×3.</summary>
        C3,

        /// <summary>콤보 P4 → P2.</summary>
        C4,
    }
}
