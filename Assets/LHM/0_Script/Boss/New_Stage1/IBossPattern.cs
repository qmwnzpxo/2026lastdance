using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    /// <summary>
    /// 모든 보스 패턴이 구현해야 하는 핵심 계약(Contract).
    /// 전략 패턴(Strategy Pattern)의 인터페이스 역할.
    /// ScriptableObject 기반 구현체는 이 인터페이스를 통해 다형적으로 실행된다.
    /// </summary>
    public interface IBossPattern
    {
        /// <summary>패턴의 고유 식별자 (디버깅 및 로깅용)</summary>
        string PatternId { get; }

        /// <summary>
        /// 패턴 실행 진입점. CancellationToken을 받아 외부에서 즉시 중단 가능해야 한다.
        /// UniTask를 반환하여 호출자가 await / WhenAll / WhenAny로 조합할 수 있다.
        /// </summary>
        /// <param name="context">패턴이 참조할 보스 컨텍스트 (위치, HP 등)</param>
        /// <param name="ct">페이즈 전환 또는 보스 파괴 시 Signal이 전달되는 토큰</param>
        UniTask ExecuteAsync(BossContext context, CancellationToken ct);

        /// <summary>
        /// 패턴 타입 구분.
        /// - Persistent: 페이즈 내내 지속 실행 (방어막, 루프 레이저 등)
        /// - Sequential: 순서대로 한 번 실행 후 다음으로 넘어가는 패턴
        /// </summary>
        PatternType Type { get; }
    }

    public enum PatternType
    {
        Sequential,  // 순차 실행 후 완료 반환
        Persistent   // 취소 신호(CancellationToken)까지 무한 루프
    }

    /// <summary>
    /// ScriptableObject 기반 패턴 베이스 클래스.
    /// 공통 유틸리티(쿨다운, 로깅, GC-safe 캐싱)를 집중 관리.
    /// 
    /// ScriptableObject를 베이스로 선택한 이유:
    ///   1. 패턴 데이터를 에셋으로 분리 → Inspector에서 직접 조정 가능
    ///   2. 공유 인스턴스이므로 런타임 메모리 절약
    ///   3. Addressable/AssetBundle로 Hot-reload 가능
    /// 
    /// ⚠️ ScriptableObject는 공유 인스턴스이므로 인스턴스 필드에
    ///    실행 상태(타이머, 카운터 등)를 저장하면 안 된다.
    ///    모든 실행 상태는 로컬 변수 또는 BossContext를 통해 관리할 것.
    /// </summary>
    public abstract class BossPatternBase : ScriptableObject, IBossPattern
    {
        [Header("Pattern Identity")]
        [SerializeField] private string _patternId;
        [SerializeField] private PatternType _patternType;

        [Header("Common Settings")]
        [Tooltip("패턴 실행 전 대기 시간 (초). 0이면 즉시 실행.")]
        [SerializeField, Range(0f, 10f)] protected float preDelay = 0f;

        public string PatternId => _patternId;
        public PatternType Type => _patternType;

        /// <summary>
        /// 외부 진입점. 공통 preDelay 처리 후 OnExecuteAsync로 위임.
        /// sealed로 선언하여 하위 클래스가 실수로 오버라이드하는 것을 방지.
        /// </summary>
        public async UniTask ExecuteAsync(BossContext context, CancellationToken ct)
        {
            if (preDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(preDelay), cancellationToken: ct);

            await OnExecuteAsync(context, ct);
        }

        /// <summary>
        /// 실제 패턴 로직을 구현하는 추상 메서드.
        /// OperationCanceledException은 catch하지 말 것.
        /// UniTask 프레임워크가 CancellationToken 처리를 위임받으므로
        /// 예외를 잡으면 정상 취소 흐름이 깨진다.
        /// </summary>
        protected abstract UniTask OnExecuteAsync(BossContext context, CancellationToken ct);

        /// <summary>
        /// GC-safe 로그 헬퍼. string.Format 대신 사용하여 박싱/힙 할당 최소화.
        /// 프로덕션 빌드에서는 BOSS_DEBUG 심볼 제거로 완전히 컴파일 아웃.
        /// </summary>
        [System.Diagnostics.Conditional("BOSS_DEBUG")]
        protected void Log(string message)
        {
            Debug.Log($"[{PatternId}] {message}");
        }
    }
}
