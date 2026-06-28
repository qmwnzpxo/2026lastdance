//using BossSystem;
//using Cysharp.Threading.Tasks;
//using System.Threading;
//using UnityEngine;

///// <summary>
///// ScriptableObject 기반 패턴 베이스.

//public abstract class BossPatternBase : ScriptableObject, IBossPattern
//{
//    [Header("Pattern Identity")]
//    [SerializeField] private string _patternId;
//    [SerializeField] private PatternType _patternType;

//    [Header("Common Settings")]
//    [Tooltip("패턴 실행 전 대기 시간 (초). 0이면 즉시 실행.")]
//    [SerializeField, Range(0f, 10f)] protected float preDelay = 0f;

//    public string PatternId => _patternId;
//    public PatternType Type => _patternType;

    
//    public async UniTask ExecuteAsync(BossContext context, CancellationToken ct)
//    {
//        if (preDelay > 0f)
//            await UniTask.Delay(System.TimeSpan.FromSeconds(preDelay), cancellationToken: ct);

//        Debug.Log("start OnExecuteAsync()");
//        await OnExecuteAsync(context, ct);
//    }

//    /// <summary>
//    /// 실제 패턴 로직 구현 추상 메서드.
//    /// OperationCanceledException은 catch하지 말 것.
//    /// UniTask 프레임워크에 취소 처리를 위임해야 한다.
//    /// </summary>
//    protected abstract UniTask OnExecuteAsync(BossContext context, CancellationToken ct);

//    /// <summary>
//    /// GC-safe 로그 헬퍼.
//    /// BOSS_DEBUG 심볼 제거 시 프로덕션 빌드에서 컴파일 아웃.
//    /// </summary>
//    [System.Diagnostics.Conditional("BOSS_DEBUG")]
//    protected void Log(string message) => Debug.Log($"[{PatternId}] {message}");
//}