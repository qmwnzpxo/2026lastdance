using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 히트스탑(타격 시 일시적 슬로우모션) 효과를 전역으로 관리하는 싱글톤 매니저.
/// Time.timeScale을 일시적으로 낮춰 타격감을 연출하고,
/// UI/파티클은 unscaledTime을 사용해 히트스탑 영향을 받지 않도록 설계.
/// </summary>
// ── 싱글톤 ────────────────────────────────────────────────
// 씬 어디서든 HitstopManager.Instance 로 접근 가능
public class HitstopManager : PersistentSingleton<HitstopManager>
{
    // 현재 히트스탑이 진행 중인지 외부에서 확인할 때 사용
    // 예: 히트스탑 중엔 입력을 무시하고 싶을 때 if (HitstopManager.Instance.IsHitstop) 로 체크
    public bool IsHitstop { get; private set; }

    // ── 취소 토큰 ─────────────────────────────────────────────
    // UniTask의 비동기 작업을 중간에 취소하기 위한 토큰 소스.
    // 히트스탑 도중 새 히트스탑이 요청되면 이전 작업을 취소하고 새로 시작한다.
    private CancellationTokenSource _cts;

    /// <summary>
    /// 히트스탑을 발동시키는 메서드. 피격 처리 코드에서 호출.
    /// </summary>
    /// <param name="duration">히트스탑 지속 시간 (실제 시간 기준, 초). 기본값 0.08초.</param>
    /// <param name="timeScale">히트스탑 중 게임 속도 배율. 0에 가까울수록 강한 정지감. 기본값 0.05.</param>
    public void TriggerHitstop(float duration = 0.08f, float timeScale = 0.08f)
    {
        // 이미 히트스탑이 진행 중이라면 → 이전 작업을 취소하고 새 히트스탑으로 덮어쓴다.
        // Cancel() : 현재 실행 중인 HitstopRoutine에 "취소 신호"를 보낸다.
        // Dispose() : 토큰 소스가 사용한 메모리를 즉시 해제한다.
        _cts?.Cancel();
        _cts?.Dispose();

        // 새 취소 토큰 소스 생성 — 이번 히트스탑 전용
        _cts = new CancellationTokenSource();

        // UniTask 비동기 메서드 실행.
        // Forget() : 반환값(UniTask)을 사용하지 않겠다는 의미.
        //            await 없이 "발사 후 망각(fire-and-forget)" 방식으로 실행.
        //            내부적으로 예외는 UniTaskScheduler가 처리.
        HitstopRoutine(duration, timeScale, _cts.Token).Forget();
    }

    /// <summary>
    /// 실제 히트스탑 동작을 수행하는 비동기 메서드.
    /// UniTaskVoid : 반환값이 없는 UniTask. async void 대신 사용 (예외 처리가 더 안전).
    /// </summary>
    private async UniTaskVoid HitstopRoutine(float duration, float timeScale, CancellationToken ct)
    {
        // 히트스탑 시작 상태 기록
        IsHitstop = true;

        // 게임 전체 시간 배율을 낮춰 슬로우모션 효과 적용
        // Rigidbody, Animator 등 유니티 물리/애니메이션이 모두 이 값의 영향을 받는다.
        Time.timeScale = timeScale;

        try
        {
            // ignoreTimeScale: true → timeScale이 낮아도 실제(real) 시간 기준으로 대기 (게임은 느려지지만 타이머는 정상 속도로)
            // cancellationToken: ct → 외부에서 취소 신호가 오면 대기를 즉시 중단
            await UniTask.WaitForSeconds(duration, ignoreTimeScale: true, cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            // TriggerHitstop()이 다시 호출되어 _cts.Cancel()이 실행되면 여기로 온다.
            // 이건 "에러"가 아니라 의도된 덮어쓰기 흐름이므로 그냥 무시.
            // (새 TriggerHitstop이 timeScale을 다시 설정하기 때문에 여기서 복구 불필요)
        }
        finally
        {
            // 핵심 안전장치
            // try가 정상 완료되든, catch로 취소되든 finally는 "반드시" 실행된다.
            // 코루틴(StopCoroutine)은 이 보장이 없어서 timeScale이 낮은 채로
            // 게임이 굳어버리는 버그가 생길 수 있다. UniTask는 이를 구조적으로 방지.
            Time.timeScale = 1f;
            IsHitstop = false;
        }
    }

    // ── 정리 ──────────────────────────────────────────────────
    void OnDestroy()
    {
        // 오브젝트가 파괴될 때 (씬 전환, 게임 종료 등)
        // 진행 중인 비동기 작업을 취소하고 timeScale을 정상으로 복구
        _cts?.Cancel();
        _cts?.Dispose();
        Time.timeScale = 1f;
    }
}