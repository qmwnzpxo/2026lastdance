using BossSystem;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// ScriptableObject 기반 패턴 베이스.
///
/// ── ScriptableObject 선택 이유 ───────────────────────────────────────
///   1. 패턴 데이터를 에셋으로 분리 → Inspector 직접 조정
///   2. 공유 인스턴스 → 런타임 메모리 절약
///   3. Addressable로 Hot-reload 가능
///
/// ⚠️ ScriptableObject는 공유 인스턴스이므로 인스턴스 필드에
///    실행 상태(타이머, 카운터)를 저장하면 안 된다.
///    모든 실행 상태는 로컬 변수 또는 BossContext로 관리할 것.
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
    /// 외부 진입점. preDelay 처리 후 OnExecuteAsync로 위임.
    ///
    /// [버그 수정] 기존 코드에서 switch(Random.Range(1,2))를 사용해
    /// OnExecuteAsync가 절대 호출되지 않는 치명적 버그가 있었다.
    /// Random.Range(int, int)는 max 미포함이므로 항상 1만 반환,
    /// case 0 진입 불가. switch 전체 제거 후 직접 await으로 수정.
    /// </summary>
    public async UniTask ExecuteAsync(BossContext context, CancellationToken ct)
    {
        if (preDelay > 0f)
            await UniTask.Delay(System.TimeSpan.FromSeconds(preDelay), cancellationToken: ct);

        Debug.Log("start OnExecuteAsync()");
        await OnExecuteAsync(context, ct);
    }

    /// <summary>
    /// 실제 패턴 로직 구현 추상 메서드.
    /// OperationCanceledException은 catch하지 말 것.
    /// UniTask 프레임워크에 취소 처리를 위임해야 한다.
    /// </summary>
    protected abstract UniTask OnExecuteAsync(BossContext context, CancellationToken ct);

    /// <summary>
    /// GC-safe 로그 헬퍼.
    /// BOSS_DEBUG 심볼 제거 시 프로덕션 빌드에서 컴파일 아웃.
    /// </summary>
    [System.Diagnostics.Conditional("BOSS_DEBUG")]
    protected void Log(string message) => Debug.Log($"[{PatternId}] {message}");
}