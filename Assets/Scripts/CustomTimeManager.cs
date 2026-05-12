using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public enum TimeCategory { Player, Enemy, Ghost }

public class CustomTimeManager : PersistentSingleton<CustomTimeManager>
{
    // ── 배율 저장 ─────────────────────────────────────────────
    private float _playerScale = 1f;
    private float _enemyScale = 1f;
    private float _ghostScale = 1f;

    // ── 히트스탑 배율 (HitstopManager 전용) ──────────────────
    private float _hitstopMultiplier = 1f;

    // ── 실제 적용값 ───────────────────────────────────────────
    public float PlayerTimeScale => _playerScale * _hitstopMultiplier;
    public float EnemyTimeScale => _enemyScale * _hitstopMultiplier;
    public float GhostTimeScale => _ghostScale * _hitstopMultiplier;

    // ── 스케일된 deltaTime 프로퍼티 ──────────────────────────
    public float PlayerDeltaTime => Time.unscaledDeltaTime * PlayerTimeScale;
    public float PlayerFixedDeltaTime => Time.fixedUnscaledDeltaTime * PlayerTimeScale;

    public float EnemyDeltaTime => Time.unscaledDeltaTime * EnemyTimeScale;

    public float EnemyFixedDeltaTime => Time.fixedUnscaledDeltaTime * EnemyTimeScale;

    public float GhostDeltaTime => Time.unscaledDeltaTime * GhostTimeScale;
    public float GhostFixedDeltaTime => Time.fixedUnscaledDeltaTime * GhostTimeScale;

    // 카테고리로 접근하는 버전 (UniTask 헬퍼용)
    public float GetDeltaTime(TimeCategory cat) => cat switch
    {
        TimeCategory.Player => PlayerDeltaTime,
        TimeCategory.Enemy => EnemyDeltaTime,
        TimeCategory.Ghost => GhostDeltaTime,
        _ => Time.unscaledDeltaTime
    };

    public float GetFixedDeltaTime(TimeCategory cat) => cat switch
    {
        TimeCategory.Player => PlayerFixedDeltaTime,
        TimeCategory.Enemy => EnemyFixedDeltaTime,
        TimeCategory.Ghost => GhostFixedDeltaTime,
        _ => Time.fixedUnscaledDeltaTime
    };

    public float GetTimeScale(TimeCategory cat) => cat switch
    {
        TimeCategory.Player => _playerScale,
        TimeCategory.Enemy => _enemyScale,
        TimeCategory.Ghost => _ghostScale,
        _ => 1f
    };

    // ── 이벤트 ───────────────────────────────────────────────
    public event Action<float> OnPlayerTimeScaleChanged;
    public event Action<float> OnEnemyTimeScaleChanged;
    public event Action<float> OnCloneTimeScaleChanged;

    // ── CTS ──────────────────────────────────────────────────
    private CancellationTokenSource _playerCts;
    private CancellationTokenSource _enemyCts;
    private CancellationTokenSource _ghostCts;

    // ─────────────────────────────────────────────────────────
    #region 외부 API
    /// <param name="scale">적용할 배율 (0 = 완전 정지, 1 = 기본)</param>
    /// <param name="duration">0이면 영구 변경, 양수면 duration초 후 1f로 자동 복구</param>
    public void SetPlayerTimeScale(float scale, float duration = 0f)
    {
        _playerCts = Restart(_playerCts);
        ApplyScale(ref _playerScale, scale, OnPlayerTimeScaleChanged);

        if (duration > 0f)
            RestoreAfter(TimeCategory.Player, duration, _playerCts.Token).Forget();
    }

    public void SetEnemyTimeScale(float scale, float duration = 0f)
    {
        _enemyCts = Restart(_enemyCts);
        ApplyScale(ref _enemyScale, scale, OnEnemyTimeScaleChanged);

        if (duration > 0f)
            RestoreAfter(TimeCategory.Enemy, duration, _enemyCts.Token).Forget();
    }

    public void SetGhostTimeScale(float scale, float duration = 0f)
    {
        _ghostCts = Restart(_ghostCts);
        ApplyScale(ref _ghostScale, scale, OnCloneTimeScaleChanged);

        if (duration > 0f)
            RestoreAfter(TimeCategory.Ghost, duration, _ghostCts.Token).Forget();
    }

    public void ResetAll()
    {
        SetPlayerTimeScale(1f);
        SetEnemyTimeScale(1f);
        SetGhostTimeScale(1f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region HitstopManager 전용 API

    /// <summary>
    /// 히트스탑 배율만 건드린다. 베이스 스케일은 유지.
    /// </summary>
    public void SetHitstopMultiplier(float multiplier)
    {
        //ApplyScale(ref _hitstopMultiplier, multiplier, OnPlayerTimeScaleChanged);

        _hitstopMultiplier = Mathf.Max(0f, multiplier);
        // 세 카테고리 모두 이벤트 발행 (hitstop은 전체에 영향)
        OnPlayerTimeScaleChanged?.Invoke(PlayerTimeScale);
        OnEnemyTimeScaleChanged?.Invoke(EnemyTimeScale);
        OnCloneTimeScaleChanged?.Invoke(GhostTimeScale);
    }

    public void ResetHitstopMultiplier() => SetHitstopMultiplier(1f);

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 내부
    private void ApplyScale(ref float field, float scale, Action<float> evt)
    {
        field = Mathf.Max(0f, scale);
        evt?.Invoke(field);
    }

    private CancellationTokenSource Restart(CancellationTokenSource old)
    {
        old?.Cancel();
        old?.Dispose();
        return new CancellationTokenSource();
    }

    private async UniTaskVoid RestoreAfter(
        TimeCategory cate, float duration, CancellationToken ct)
    {
        // duration은 실제 경과 시간 기준 (unscaled)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;
            elapsed += Time.fixedUnscaledDeltaTime;
        }

        // 복구
        switch (cate)
        {
            case TimeCategory.Player:
                SetPlayerTimeScale(1f); break;
            case TimeCategory.Enemy: 
                SetEnemyTimeScale(1f); break;
            case TimeCategory.Ghost: 
                SetGhostTimeScale(1f); break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    protected override void OnApplicationQuit()
    {
        _playerCts?.Cancel();
        _playerCts?.Dispose();
        _enemyCts?.Cancel();
        _enemyCts?.Dispose();
        _ghostCts?.Cancel();
        _ghostCts?.Dispose();
        base.OnApplicationQuit();
    }
}