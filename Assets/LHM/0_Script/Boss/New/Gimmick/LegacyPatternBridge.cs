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
    /// v2 패턴이 완성될 때까지만 쓰는 임시 다리다. (BACKLOG Q19 C, T68 에서 삭제)
    /// </summary>
    /// <remarks>
    /// 레거시 패턴은 내부에서 Time·WaitForSeconds 를 쓰므로 GameClock 히트스톱·슬로우를 따르지 않는다.
    /// </remarks>
    public sealed class LegacyPatternBridge : IBossPattern
    {
        private readonly BossAttackPatterns _patterns;
        private readonly AttackPattern _pattern;
        private readonly float _timeoutSeconds;

        /// <summary>레거시 패턴 하나를 감싼다.</summary>
        /// <param name="patterns">레거시 패턴 실행기. 코루틴도 여기서 돌린다 (ForceStopAllPatterns 로 함께 멈추게).</param>
        /// <param name="pattern">실행할 레거시 패턴.</param>
        /// <param name="timing">예고·반격 틈.</param>
        /// <param name="timeoutSeconds">이 시간(레거시와 같은 Time.deltaTime 기준) 안에 끝나지 않으면 강제 종료한다. 0보다 커야 한다.</param>
        public LegacyPatternBridge(
            BossAttackPatterns patterns,
            AttackPattern pattern,
            PatternTiming timing,
            float timeoutSeconds)
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
            Timing = timing;
            DebugName = $"Legacy:{pattern}";
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

            return PatternResult.Default;
        }

        private IEnumerator RunLegacy(BossController boss, RunHandle handle)
        {
            // 중첩 IEnumerator 는 같은 MonoBehaviour(_patterns) 위에서 이어 실행된다.
            yield return _patterns.ExecutePattern(_pattern, boss);
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
