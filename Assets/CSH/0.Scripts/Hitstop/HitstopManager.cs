using UnityEngine;

/// <summary>
/// 히트스탑(타격 시 일시적 슬로우모션) 효과를 전역으로 관리하는 싱글톤 매니저.
/// CustomTimeManager에 "모든 카테고리 + 히트스탑 표시" 요청을 넣고, 해제는 예약 시간(실제 시간)으로 맡긴다.
/// </summary>
public class HitstopManager : PersistentSingleton<HitstopManager>
{
    // 진행 중 히트스탑 요청. 만료되면 CustomTimeManager가 알아서 지운다.
    private CustomTimeManager.Handle _handle;

    /// <summary>진행 중 히트스탑의 남은 시간 (실제 시간, 초). 없으면 0.</summary>
    public float RemainingTime
    {
        get
        {
            var mgr = CustomTimeManager.Instance;
            return mgr != null ? mgr.GetRemaining(_handle) : 0f;
        }
    }

    /// <summary>
    /// 히트스탑을 발동시키는 메서드. 피격 처리 코드에서 호출.
    /// 진행 중 히트스탑보다 먼저 끝나는 요청은 무시하고, 늦게 끝나는 요청은 그 요청(길이·배율)으로 교체한다 (Q25).
    /// </summary>
    /// <param name="duration">히트스탑 지속 시간 (실제 시간 기준, 초)</param>
    /// <param name="timeScale">히트스탑 중 게임 속도 배율. 0에 가까울수록 강한 정지감.</param>
    public void TriggerHitstop(float duration = 0.08f, float timeScale = 0.08f)
    {
        var mgr = CustomTimeManager.Instance;
        if (mgr == null || duration <= 0f) return;

        if (mgr.IsActive(_handle))
        {
            // 긴 쪽 유지: 진행 중인 히트스탑이 더 늦게 끝나면 짧은 새 요청은 무시
            if (duration <= mgr.GetRemaining(_handle)) return;

            mgr.SetScale(_handle, timeScale);
            mgr.SetDuration(_handle, duration);
        }
        else
        {
            _handle = mgr.RequestHitstop(timeScale, duration);
        }
    }

    // ── 프리셋 ───────────────────────
    public void TriggerLight() => TriggerHitstop(0.05f, 0.05f);
    public void TriggerMedium() => TriggerHitstop(0.08f, 0.02f);
    public void TriggerHeavy() => TriggerHitstop(0.12f, 0.01f);

    // ── 정리 ──────────────────────────────────────────────────
    void OnDestroy()
    {
        CustomTimeManager.Instance?.Release(ref _handle);
    }
}
