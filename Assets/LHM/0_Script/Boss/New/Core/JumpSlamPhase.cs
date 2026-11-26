namespace UnMachine.Core.Boss
{
    /// <summary>P1 점프 내려찍기 진행 단계 (boss.md 6장 P1). 웅크림(0.5초)은 Telegraph 상태가 맡는다.</summary>
    public enum JumpSlamPhase
    {
        /// <summary>시작 전.</summary>
        Idle,

        /// <summary>도약: 바닥 → 화면 밖 높이.</summary>
        Rising,

        /// <summary>화면 밖, 그림자가 플레이어 x 를 따라감.</summary>
        Tracking,

        /// <summary>화면 밖, 그림자 고정 (마지막 회피 기회).</summary>
        Locked,

        /// <summary>그림자 위치로 낙하.</summary>
        Falling,

        /// <summary>착지 완료.</summary>
        Landed,
    }
}
