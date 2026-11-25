using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 보스 행동 상태 머신 (boss.md 5장).
    /// 기본 루프: Idle → Telegraph → Attacking → Punishable → Resting → Idle.
    /// 인터럽트 우선순위: Dead > PhaseTransition > Groggy > Stunned > 기본 루프.
    /// 시간은 Tick(dt) 로만 흐른다 (어댑터가 GameClock World dt 를 넣는다). 패턴 선택·실행은 외부가 한다.
    /// </summary>
    /// <remarks>
    /// 길이가 0인 시간 상태는 들어가지 않고 다음 상태로 넘어간다 (그 상태의 StateChanged 는 발생하지 않음).
    /// StateChanged 핸들러 안에서 Begin 을 호출해도 된다 (Idle 진입 즉시 다음 패턴). 이때 Tick 의 남은 dt 는
    /// 새 Telegraph 로 이어진다. 구독자가 여럿이면 중첩 호출로 이벤트 순서가 섞일 수 있으니 Begin 은 한 곳에서만 호출한다.
    /// </remarks>
    public sealed class BossFlowMachine
    {
        private const int Phase1 = 1;
        private const int Phase2 = 2;

        private readonly BossFlowSettings _settings;

        private PatternTiming _currentTiming;

        // 현재 패턴(마지막 Begin)이 Telegraph·Attacking 중 인터럽트로 끊겼는지. 늦게 온 NotifyAttackEnded 를 무시한다.
        private bool _isAttackInterrupted;

        // 페이즈 전환은 1회만 (boss.md 8장 "HP 50% 최초 하회, 1회").
        private bool _isPhaseTransitionRequested;

        /// <summary>상태 머신을 만든다. 수치는 BossFlowConfig.ToSettings() 로 받는다.</summary>
        public BossFlowMachine(BossFlowSettings settings)
        {
            _settings = settings;
        }

        /// <summary>상태가 바뀔 때 발생한다. 인자 = (이전 상태, 새 상태).</summary>
        public event Action<BossFlowState, BossFlowState> StateChanged;

        /// <summary>페이즈가 바뀔 때 발생한다 (SetPhase 또는 PhaseTransition 종료). 인자 = 새 페이즈.</summary>
        public event Action<int> PhaseChanged;

        /// <summary>현재 상태. 처음은 Idle.</summary>
        public BossFlowState State { get; private set; } = BossFlowState.Idle;

        /// <summary>현재 시간 상태의 남은 시간(초). Idle·Attacking·Dead 는 0. 디버그 표시용.</summary>
        public float StateRemaining { get; private set; }

        /// <summary>현재 페이즈 (1 또는 2).</summary>
        public int Phase { get; private set; } = Phase1;

        /// <summary>Groggy 중 전환 요청이 보류되어 있는지 (Groggy 가 끝나면 PhaseTransition).</summary>
        public bool IsPhaseTransitionPending { get; private set; }

        /// <summary>현재 상태의 받는 피해 배율 (boss.md 4장). 무적 판정은 하지 않는다 (T33).</summary>
        public float DamageMultiplier => _settings.DamageScale.ForState(State);

        /// <summary>
        /// 페이즈를 직접 바꾼다 (디버그·토글 Off 동기화용). 정상 흐름은 RequestPhaseTransition.
        /// 휴식 시간·반격 틈 배율은 그 상태에 들어가는 시점의 페이즈로 정해진다.
        /// </summary>
        /// <param name="phase">1 또는 2.</param>
        public void SetPhase(int phase)
        {
            if (phase != Phase1 && phase != Phase2)
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "페이즈는 1 또는 2여야 한다.");
            }

            ApplyPhase(phase);
        }

        /// <summary>
        /// 패턴을 시작한다 → Telegraph (길이 = max(예고, 최소 예고)). Idle 에서만 호출할 수 있다.
        /// </summary>
        /// <exception cref="InvalidOperationException">Idle 이 아닐 때.</exception>
        public void Begin(PatternTiming timing)
        {
            RequireState(BossFlowState.Idle, nameof(Begin));
            _currentTiming = timing;
            _isAttackInterrupted = false;

            // spec: boss.md 9장 "예고는 최소 0.4초 이상 보장"
            EnterTimed(BossFlowState.Telegraph, Math.Max(timing.TelegraphSeconds, _settings.MinTelegraphSeconds));
        }

        /// <summary>
        /// 패턴 실행이 끝났음을 알린다 → Punishable (2페이즈면 ×배율) → Resting.
        /// </summary>
        /// <returns>
        /// 적용했으면 true. 이번 패턴이 인터럽트로 이미 끊겼으면 무시하고 false.
        /// </returns>
        /// <exception cref="InvalidOperationException">Attacking 이 아니고, 인터럽트로 끊긴 패턴도 아닐 때.</exception>
        public bool NotifyAttackEnded(PatternResult result)
        {
            if (State != BossFlowState.Attacking)
            {
                if (_isAttackInterrupted)
                {
                    return false;
                }

                RequireState(BossFlowState.Attacking, nameof(NotifyAttackEnded));
            }

            float punishSeconds = result.ResolvePunishSeconds(_currentTiming);
            if (Phase == Phase2)
            {
                // spec: boss.md 8장 2페이즈 "반격 틈은 패턴별 값 ×0.8"
                punishSeconds *= _settings.Phase2PunishScale;
            }

            EnterTimed(BossFlowState.Punishable, punishSeconds);
            return true;
        }

        /// <summary>벽 충돌 기절 → Stunned → Resting. 기본 루프 상태에서만 받아들인다.</summary>
        /// <returns>Stunned 로 들어갔으면 true (길이 0이면 바로 Resting 이어도 true).</returns>
        public bool EnterStunned()
        {
            if (!IsBaseLoop(State))
            {
                return false;
            }

            MarkAttackInterrupted();
            EnterTimed(BossFlowState.Stunned, _settings.StunnedSeconds);
            return true;
        }

        /// <summary>그로기 → Groggy → Resting (보류된 전환이 있으면 PhaseTransition). Dead·PhaseTransition·Groggy 중이면 무시.</summary>
        /// <returns>Groggy 로 들어갔으면 true.</returns>
        public bool EnterGroggy()
        {
            if (State == BossFlowState.Dead
                || State == BossFlowState.PhaseTransition
                || State == BossFlowState.Groggy)
            {
                return false;
            }

            MarkAttackInterrupted();
            EnterTimed(BossFlowState.Groggy, _settings.GroggySeconds);
            return true;
        }

        /// <summary>
        /// 페이즈 전환을 요청한다 (1회만). Groggy 중이면 보류했다가 Groggy 가 끝난 직후 PhaseTransition.
        /// spec: boss.md 5장·8장 "Groggy 중이면 Groggy·QTE가 끝난 뒤"
        /// </summary>
        /// <returns>요청을 받아들였으면(즉시 진입 또는 보류) true. 이미 요청했거나 Dead 면 false.</returns>
        public bool RequestPhaseTransition()
        {
            if (State == BossFlowState.Dead || _isPhaseTransitionRequested)
            {
                return false;
            }

            _isPhaseTransitionRequested = true;

            if (State == BossFlowState.Groggy)
            {
                IsPhaseTransitionPending = true;
                return true;
            }

            EnterPhaseTransition();
            return true;
        }

        /// <summary>사망 → Dead. 이후 모든 요청은 무시되고 Tick 은 아무것도 하지 않는다.</summary>
        /// <returns>Dead 로 들어갔으면 true. 이미 Dead 면 false.</returns>
        public bool EnterDead()
        {
            if (State == BossFlowState.Dead)
            {
                return false;
            }

            MarkAttackInterrupted();
            IsPhaseTransitionPending = false;
            ChangeState(BossFlowState.Dead, 0f);
            return true;
        }

        /// <summary>
        /// 시간을 진행한다. 한 번에 여러 시간 상태가 끝나면 남은 dt 를 다음 시간 상태로 넘긴다
        /// (상태 길이가 프레임 단위로 늘어나지 않게). Idle·Attacking·Dead 에서는 시간이 쌓이지 않는다.
        /// </summary>
        /// <param name="dt">경과 시간(초, GameClock World 채널). 0 이상 유한값.</param>
        public void Tick(float dt)
        {
            float budget = RequireNonNegativeFinite(dt, nameof(dt));

            while (IsTimed(State))
            {
                if (budget < StateRemaining)
                {
                    StateRemaining -= budget;
                    return;
                }

                budget -= StateRemaining;
                StateRemaining = 0f;
                EnterAfter(State);
            }
        }

        /// <summary>0 이상 유한값인지 확인하고 그대로 돌려준다. 같은 어셈블리의 보스 Core 타입이 함께 쓴다.</summary>
        internal static float RequireNonNegativeFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "0 이상 유한값이어야 한다.");
            }

            return value;
        }

        private static bool IsBaseLoop(BossFlowState state)
        {
            return state == BossFlowState.Idle
                || state == BossFlowState.Telegraph
                || state == BossFlowState.Attacking
                || state == BossFlowState.Punishable
                || state == BossFlowState.Resting;
        }

        private static bool IsTimed(BossFlowState state)
        {
            return state == BossFlowState.Telegraph
                || state == BossFlowState.Punishable
                || state == BossFlowState.Resting
                || state == BossFlowState.Stunned
                || state == BossFlowState.Groggy
                || state == BossFlowState.PhaseTransition;
        }

        private void MarkAttackInterrupted()
        {
            if (State == BossFlowState.Telegraph || State == BossFlowState.Attacking)
            {
                _isAttackInterrupted = true;
            }
        }

        private void EnterPhaseTransition()
        {
            MarkAttackInterrupted();
            IsPhaseTransitionPending = false;
            EnterTimed(BossFlowState.PhaseTransition, _settings.PhaseTransitionSeconds);
        }

        private void EnterResting()
        {
            // spec: boss.md 5장 Resting "1페이즈 1.5초 / 2페이즈 0.8초"
            EnterTimed(
                BossFlowState.Resting,
                Phase == Phase2 ? _settings.RestSecondsPhase2 : _settings.RestSecondsPhase1);
        }

        /// <summary>시간 상태에 들어간다. 길이가 0이면 들어가지 않고 바로 다음 상태로.</summary>
        private void EnterTimed(BossFlowState state, float seconds)
        {
            if (seconds > 0f)
            {
                ChangeState(state, seconds);
                return;
            }

            EnterAfter(state);
        }

        /// <summary>finished 상태가 끝난(또는 건너뛴) 뒤의 상태로 들어간다.</summary>
        private void EnterAfter(BossFlowState finished)
        {
            switch (finished)
            {
                case BossFlowState.Telegraph:
                    ChangeState(BossFlowState.Attacking, 0f);
                    break;
                case BossFlowState.Punishable:
                case BossFlowState.Stunned:
                    EnterResting();
                    break;
                case BossFlowState.Groggy:
                    if (IsPhaseTransitionPending)
                    {
                        EnterPhaseTransition();
                    }
                    else
                    {
                        EnterResting();
                    }

                    break;
                case BossFlowState.PhaseTransition:
                    // 페이즈를 먼저 바꾼다 → Idle 핸들러에서 Begin 할 때 2페이즈 패턴·휴식이 적용된다.
                    ApplyPhase(Phase2);
                    ChangeState(BossFlowState.Idle, 0f);
                    break;
                case BossFlowState.Resting:
                    ChangeState(BossFlowState.Idle, 0f);
                    break;
                default:
                    throw new InvalidOperationException($"{finished} 은 시간 상태가 아니다.");
            }
        }

        private void ApplyPhase(int phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void ChangeState(BossFlowState next, float seconds)
        {
            BossFlowState previous = State;
            State = next;
            StateRemaining = seconds;

            // 상태 갱신을 끝낸 뒤 마지막에 알린다 → 핸들러 안의 Begin 이 갱신된 상태를 본다.
            StateChanged?.Invoke(previous, next);
        }

        private void RequireState(BossFlowState expected, string caller)
        {
            if (State != expected)
            {
                throw new InvalidOperationException($"{caller} 는 {expected} 상태에서만 호출할 수 있다 (현재 {State}).");
            }
        }
    }
}
