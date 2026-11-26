using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 기존 코루틴 패턴(BossAttackPatterns.ExecutePattern)을 IBossPattern 으로 감싼다.
    /// 1주 축소 계획(2026-11-26)으로 P2~P6 은 이 다리를 계속 쓴다 (T68 레거시 제거 철회).
    /// </summary>
    /// <remarks>
    /// 레거시 패턴은 내부에서 Time·WaitForSeconds 를 쓰므로 GameClock 히트스톱·슬로우를 따르지 않는다.
    /// </remarks>
    public sealed class LegacyPatternBridge : IBossPattern, IComboStepOutcomeSource
    {
        private readonly BossAttackPatterns _patterns;
        private readonly AttackPattern _pattern;
        private readonly float _timeoutSeconds;
        private readonly bool _chargeBrakeAfterPass;
        private readonly bool _howitzerDetachFall;

        /// <summary>레거시 패턴 하나를 감싼다.</summary>
        /// <param name="patterns">레거시 패턴 실행기. 코루틴도 여기서 돌린다 (ForceStopAllPatterns 로 함께 멈추게).</param>
        /// <param name="pattern">실행할 레거시 패턴.</param>
        /// <param name="timing">예고·반격 틈.</param>
        /// <param name="timeoutSeconds">이 시간(레거시와 같은 Time.deltaTime 기준) 안에 끝나지 않으면 강제 종료한다. 0보다 커야 한다.</param>
        /// <param name="chargeBrakeAfterPass">(C1 첫 단계) 돌진이 플레이어를 지나친 뒤 Brake 정지 (T57).</param>
        /// <param name="howitzerDetachFall">(C4 첫 단계) 곡사 발사가 끝나면 종료, 낙하는 따로 진행 (T57).</param>
        public LegacyPatternBridge(
            BossAttackPatterns patterns,
            AttackPattern pattern,
            PatternTiming timing,
            float timeoutSeconds,
            bool chargeBrakeAfterPass = false,
            bool howitzerDetachFall = false)
        {
            if (patterns == null)
            {
                throw new ArgumentNullException(nameof(patterns));
            }

            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds), timeoutSeconds, "0보다 큰 유한값이어야 한다.");
            }

            _patterns = patterns;
            _pattern = pattern;
            _timeoutSeconds = timeoutSeconds;
            _chargeBrakeAfterPass = chargeBrakeAfterPass;
            _howitzerDetachFall = howitzerDetachFall;
            Timing = timing;
            DebugName = chargeBrakeAfterPass ? $"Legacy:{pattern}+Brake"
                : howitzerDetachFall ? $"Legacy:{pattern}+FireOnly"
                : $"Legacy:{pattern}";
        }

        /// <inheritdoc />
        public ComboStepOutcome LastOutcome { get; private set; }

        /// <summary>같은 레거시 패턴·타이밍으로 콤보 옵션만 다른 다리를 만든다 (C1·C4 첫 단계, T57).</summary>
        public LegacyPatternBridge WithComboOptions(bool chargeBrakeAfterPass, bool howitzerDetachFall)
        {
            return new LegacyPatternBridge(
                _patterns, _pattern, Timing, _timeoutSeconds, chargeBrakeAfterPass, howitzerDetachFall);
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

            ct.ThrowIfCancellationRequested();
            LastOutcome = ComboStepOutcome.Completed;

            var handle = new RunHandle();
            _patterns.StartCoroutine(RunLegacy(boss, handle)); // guard-allow: 레거시 래핑
            float elapsed = 0f;

            // 인터럽트: 취소되는 그 순간(동기) 레거시 코루틴·관성·facing 잠금을 정리한다.
            // 다음 프레임에 정리하면 그 사이 시작된 다음 패턴까지 멈출 수 있다. 취소 예외는 그대로 올린다.
            using (ct.Register(StopLegacy))
            {
                while (!handle.IsDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    // 레거시 패턴은 WaitForSeconds(Time.deltaTime, maximumDeltaTime 상한)로 흐른다 → 같은 시계로 잰다.
                    // World 채널(unscaledDeltaTime, 상한 없음)로 재면 에디터 프레임 정지 때 정상 패턴도 타임아웃된다.
                    elapsed += Time.deltaTime; // guard-allow: 레거시 패턴과 같은 시계

                    if (elapsed > _timeoutSeconds)
                    {
                        GameLog.Warn(
                            $"[LegacyPatternBridge] {_pattern} 타임아웃({_timeoutSeconds:F1}초) → 강제 종료", boss);
                        StopLegacy();
                        break;
                    }
                }
            }

            LastOutcome = ReadOutcome();
            return PatternResult.Default;
        }

        /// <summary>돌진 결과 → 콤보 단계 결과. 명중이 벽보다 우선 (boss.md 8장 C1 예외 순서).</summary>
        private ComboStepOutcome ReadOutcome()
        {
            if (_pattern != AttackPattern.Charge || _patterns == null)
            {
                return ComboStepOutcome.Completed;
            }

            if (_patterns.LastChargeHitPlayer)
            {
                return ComboStepOutcome.HitPlayer;
            }

            return _patterns.LastChargeHitWallBeforePass ? ComboStepOutcome.HitWall : ComboStepOutcome.Completed;
        }

        private IEnumerator RunLegacy(BossController boss, RunHandle handle)
        {
            // 중첩 IEnumerator 는 같은 MonoBehaviour(_patterns) 위에서 이어 실행된다.
            // 반복 방지는 PatternSelector 가 맡는다 → 레거시 자체 대체는 끈다 (이중 적용 방지, T38).
            yield return _patterns.ExecutePattern(
                _pattern,
                boss,
                useRepeatGuard: false,
                chargeBrakeAfterPass: _chargeBrakeAfterPass,
                howitzerDetachFall: _howitzerDetachFall);
            handle.IsDone = true;
        }

        private void StopLegacy()
        {
            // 씬 종료 중이면 이미 파괴됐을 수 있다.
            if (_patterns != null)
            {
                _patterns.ForceStopAllPatterns();
            }
        }

        /// <summary>코루틴 완료 표시. async 메서드와 코루틴이 같은 값을 보게 참조 타입으로 둔다.</summary>
        private sealed class RunHandle
        {
            public bool IsDone;
        }
    }
}
