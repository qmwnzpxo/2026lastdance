using System.Threading;
using Cysharp.Threading.Tasks;

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

    
}
