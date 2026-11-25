namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 보스 행동 상태 (boss.md 5장). Idle~Resting = 기본 루프, Stunned 이후 = 인터럽트 상태.
    /// </summary>
    /// <remarks>
    /// BossController 의 BossState(Animator 정수값)와는 별개 enum 이다. 새 값은 끝에만 추가한다.
    /// 〔임시값: 상태명 PhaseTransition〕 boss.md 5장 주석.
    /// </remarks>
    public enum BossFlowState
    {
        /// <summary>패턴 선택 대기. 외부가 Begin 을 호출하면 Telegraph 로.</summary>
        Idle,

        /// <summary>패턴 예고. 최소 시간 보장 (boss.md 9장).</summary>
        Telegraph,

        /// <summary>패턴 실행. 외부가 NotifyAttackEnded 를 호출할 때까지 유지.</summary>
        Attacking,

        /// <summary>반격 틈. 받는 피해 취약 배율.</summary>
        Punishable,

        /// <summary>휴식. 1페이즈·2페이즈 시간이 다르다 (boss.md 5장·8장).</summary>
        Resting,

        /// <summary>벽 충돌 기절. 받는 피해 취약 배율, 끝나면 Resting.</summary>
        Stunned,

        /// <summary>그로기. 받는 피해 그로기 배율, 끝나면 Resting (보류된 전환이 있으면 PhaseTransition).</summary>
        Groggy,

        /// <summary>페이즈 전환 연출. 끝나면 2페이즈 Idle. 무적은 BossController(T33).</summary>
        PhaseTransition,

        /// <summary>사망. 끝나지 않는다.</summary>
        Dead,
    }
}
