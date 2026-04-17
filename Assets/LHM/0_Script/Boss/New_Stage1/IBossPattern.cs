using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{

    public interface IBossPattern
    {
        string PatternId { get; }

       
        UniTask ExecuteAsync(BossContext context, CancellationToken ct);

        PatternType Type { get; }
    }

    public enum PatternType
    {
        Sequential,
        Persistent
    }

    public abstract class BossPatternBase : ScriptableObject, IBossPattern
    {
        [Header("Pattern Identity")]
        [SerializeField] private string      _patternId;
        [SerializeField] private PatternType _patternType;

        [Header("Common Settings")]
        [Tooltip("패턴 실행 전 대기 시간 (초). 0이면 즉시 실행.")]
        [SerializeField, Range(0f, 10f)] protected float preDelay = 0f;

        public string      PatternId => _patternId;
        public PatternType Type      => _patternType;


        public async UniTask ExecuteAsync(BossContext context, CancellationToken ct)
        {
            if (preDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(preDelay), cancellationToken: ct);

            Debug.Log("start OnExecuteAsync()");
            await OnExecuteAsync(context, ct);
        }


        protected abstract UniTask OnExecuteAsync(BossContext context, CancellationToken ct);

        [System.Diagnostics.Conditional("BOSS_DEBUG")]


        protected void Log(string message) => Debug.Log($"[{PatternId}] {message}");
    }
}
