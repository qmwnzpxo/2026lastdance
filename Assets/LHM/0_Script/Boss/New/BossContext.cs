using UnityEngine;
using Cysharp.Threading.Tasks;   // 패키지 필요: com.cysharp.unitask (Package Manager 또는 UPM)
using System.Threading;

/// <summary>
/// 보스 전투 런타임 컨텍스트
/// BossController.InjectSceneDependencies() 에서 생성·주입되며,
/// SO 기반 공격 패턴(ScriptableObject)에서 ctx.BossAnimator.SetTrigger() 형태로 공유 사용
/// </summary>
public class BossContext
{
    // ─────────────────────────────────────────────────────
    // 공유 참조
    // ─────────────────────────────────────────────────────

    /// <summary>보스 Animator 참조 — SO 패턴에서 ctx.BossAnimator.SetTrigger("...") 로 사용</summary>
    public Animator BossAnimator { get; set; }

    /// <summary>보스 컨트롤러 참조</summary>
    public BossController Controller { get; set; }

    /// <summary>플레이어 Transform 참조</summary>
    public Transform Player { get; set; }

    // ─────────────────────────────────────────────────────
    // Animation Event Bridge (UniTask 기반)
    // 기존 UniTaskCompletionSource<bool> 패턴
    // (벽 이동/QTE 병렬 처리)과 동일한 스타일로 통일
    // ─────────────────────────────────────────────────────

    private UniTaskCompletionSource<bool> _animEventSource;

    /// <summary>
    /// 다음 OnAnimEventFired() 호출까지 비동기 대기
    /// CancellationToken(ct)으로 취소(페이즈 전환·보스 사망 등) 가능
    /// <code>
    /// // 사용 예시 (SO 패턴 내부)
    /// await ctx.WaitForAnimEvent(ct);
    /// </code>
    /// </summary>
    public async UniTask WaitForAnimEvent(CancellationToken ct)
    {
        _animEventSource = new UniTaskCompletionSource<bool>();
        await _animEventSource.Task.AttachExternalCancellation(ct);
    }

    /// <summary>
    /// [Animation Event] 애니메이션 이벤트 발동 시 호출 → WaitForAnimEvent 대기 해제
    /// 보스 GameObject 위의 컴포넌트(예: BossAnimEventRelay)에서
    /// ctx.OnAnimEventFired() 형태로 연결
    /// </summary>
    public void OnAnimEventFired()
    {
        _animEventSource?.TrySetResult(true);
    }

    /// <summary>
    /// BossContext 초기화 팩토리 메서드
    /// BossController.InjectSceneDependencies() 에서 호출
    /// </summary>
    public static BossContext Create(BossController controller, Animator animator, Transform player)
    {
        return new BossContext
        {
            Controller   = controller,
            BossAnimator = animator,
            Player       = player
        };
    }
}
