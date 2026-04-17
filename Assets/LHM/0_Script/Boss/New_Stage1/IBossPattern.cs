using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // IBossPattern.cs  — 전략 패턴 계약 + ScriptableObject 베이스
    //
    // ── 삭제된 중복 ────────────────────────────────────────────────────────
    //   Before: BossPhase1Controller.cs에 Phase1PanelPattern 중복 선언
    //           Phase2_Patterns.cs에 DestroyAfterDelayAsync 중복 구현
    //   After:  BossPatternBase 단일 위치. 모든 async util → LaserUtility.
    //
    // ── 버그 수정: ExecuteAsync switch 오류 ────────────────────────────────
    //   Before: Random.Range(1,2) → 항상 1 반환 → case 0 진입 불가
    //           → OnExecuteAsync 절대 호출 안됨 (치명적 버그)
    //   After:  switch 제거, 직접 await OnExecuteAsync 호출
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 보스 패턴이 구현해야 하는 핵심 계약.
    /// 전략 패턴(Strategy Pattern)의 인터페이스 역할.
    /// </summary>
    public interface IBossPattern
    {
        string PatternId { get; }

        /// <summary>
        /// 패턴 실행 진입점.
        /// CancellationToken을 받아 외부에서 즉시 중단 가능해야 한다.
        /// UniTask를 반환하여 WhenAll / WhenAny로 조합 가능.
        /// </summary>
        UniTask ExecuteAsync(BossContext context, CancellationToken ct);

        /// <summary>
        /// - Persistent : 페이즈 내내 WhenAll로 병렬 구동
        /// - Sequential : 순서대로 실행 후 완료 반환, 루프
        /// </summary>
        PatternType Type { get; }
    }

    public enum PatternType
    {
        Sequential,
        Persistent
    }

    
}
