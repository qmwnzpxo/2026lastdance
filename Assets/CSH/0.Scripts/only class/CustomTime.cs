using Cysharp.Threading.Tasks;
using System.Threading;

public static class CustomTime
{
    /// <summary>
    /// UniTask.WaitForSeconds 대체.
    /// CustomTimeManager의 카테고리 배율이 적용된 시간으로 대기한다.
    /// 배율이 0이면 ct가 취소될 때까지 무한 대기 (정지 상태 표현).
    /// </summary>
    public static async UniTask WaitForScaledSeconds(
        float duration, TimeCategory cat, CancellationToken ct)
    {
        var mgr = CustomTimeManager.Instance;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;

            // 배율이 0이면 elapsed가 증가하지 않아 자연스럽게 정지
            elapsed += mgr.GetFixedDeltaTime(cat);
        }
    }
    public static async UniTask WaitForScaledSeconds(float duration, TimeCategory cat, CancellationTokenSource cts)
    {
        await WaitForScaledSeconds(duration, cat, cts.Token);
    }

    /// <summary>
    /// 매 FixedUpdate마다 스케일된 deltaTime을 누적하여 반환.
    /// 이동 루프 내부에서 elapsed 계산 시 사용.
    /// </summary>
    public static float ScaledFixedDeltaTime(this TimeCategory cat)
        => CustomTimeManager.Instance.GetFixedDeltaTime(cat);

    public static float ScaledDeltaTime(this TimeCategory cat)
        => CustomTimeManager.Instance.GetDeltaTime(cat);
}