using System;
using System.Collections.Generic;
using UnityEngine;

public enum TimeCategory { Player, Enemy, Ghost }

/// <summary>
/// 카테고리별 시간 배율을 "요청 목록"으로 관리한다.
/// - 카테고리마다 요청을 여러 개 받고, 최종 배율 = 모든 요청 배율의 곱.
/// - 요청이 하나도 없으면 기본 배율(1)로 돌아온다.
/// - 해제 예약 시간은 실제 시간(Time.unscaledTime) 기준 절대 시각으로 저장한다.
/// - 히트스탑도 isHitstop 표시가 붙은 요청일 뿐이다 (모든 카테고리에 동시에 들어감).
///
/// 새 코드: Request / RequestAll 로 핸들을 받고, SetScale 로 갱신, Release 로 해제.
/// 기존 코드: SetPlayerTimeScale 등은 카테고리별 "레거시 요청" 하나를 덮어쓰는 방식으로 그대로 동작.
/// </summary>
[DefaultExecutionOrder(-1100)] // GameClockDriver(-1000)보다 먼저 만료 처리
public class CustomTimeManager : PersistentSingleton<CustomTimeManager>
{
    // ── 데이터 ───────────────────────────────────────────────
    public const float NoRelease = -1f;

    [Serializable]
    public struct TimeRequest
    {
        public int id;           // 핸들 id. RequestAll이면 여러 카테고리에 같은 id로 들어간다
        public string tag;       // 디버그용 이름 (누가 요청했는지)
        public float scale;
        public float releaseAt;  // Time.unscaledTime 기준 해제 시각. NoRelease(-1)면 직접 Release할 때까지 유지
        public bool isHitstop;

        public bool HasRelease => releaseAt >= 0f;
    }

    [Serializable]
    public struct TimeChannel
    {
        public TimeCategory category;
        public float scale;              // 최종 배율 (요청들의 곱, 캐시)
        public float releaseAt;          // 가장 늦게 끝나는 예약 해제 시각 (표시용). 예약 없으면 NoRelease
        public List<TimeRequest> requests;
    }

    /// <summary>요청 핸들. 기본값(default)은 무효 핸들.</summary>
    public readonly struct Handle
    {
        public readonly int Id;
        internal Handle(int id) { Id = id; }
        public bool IsValid => Id != 0;
    }

    private static readonly int CategoryCount = Enum.GetValues(typeof(TimeCategory)).Length;

    [Tooltip("런타임 확인용 (직접 수정하지 말 것)")]
    [SerializeField] private TimeChannel[] _channels;

    private int _nextId = 1;
    private Handle[] _legacyHandles;   // Set{Category}TimeScale 이 쓰는 카테고리별 요청
    private Handle _legacyHitstop;     // SetHitstopMultiplier 가 쓰는 요청
    private bool _hitstopActive;

    // ── 이벤트 ───────────────────────────────────────────────
    /// <summary>카테고리 최종 배율이 바뀔 때 (히트스탑 포함 값)</summary>
    public event Action<TimeCategory, float> OnTimeScaleChanged;
    public event Action<float> OnPlayerTimeScaleChanged;
    public event Action<float> OnEnemyTimeScaleChanged;
    public event Action<float> OnCloneTimeScaleChanged;
    public event Action<float> OnGhostTimeScaleChanged
    {
        add => OnCloneTimeScaleChanged += value;
        remove => OnCloneTimeScaleChanged -= value;
    }

    /// <summary>히트스탑 시작(true)/종료(false) 시 1회 발행. TimeScaleReceiver가 Rigidbody 정지/복원에 사용.</summary>
    public event Action<bool> OnHitstopStateChanged;

    // ─────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // 중복 인스턴스는 base에서 파괴됨
        EnsureInit();
    }

    private void EnsureInit()
    {
        if (_channels != null && _channels.Length == CategoryCount && _legacyHandles != null) return;

        _channels = new TimeChannel[CategoryCount];
        for (int i = 0; i < CategoryCount; i++)
        {
            _channels[i] = new TimeChannel
            {
                category = (TimeCategory)i,
                scale = 1f,
                releaseAt = NoRelease,
                requests = new List<TimeRequest>(4),
            };
        }
        _legacyHandles = new Handle[CategoryCount];
    }

    private void Update()
    {
        if (_channels == null) return;
        float now = Time.unscaledTime;
        for (int i = 0; i < _channels.Length; i++)
        {
            ref TimeChannel ch = ref _channels[i];
            if (!HasExpired(ch, now)) continue;

            ch.requests.RemoveAll(r => r.HasRelease && r.releaseAt <= now);
            Recalculate(i);
        }
        UpdateHitstopState();
    }

    // ─────────────────────────────────────────────────────────
    #region 읽기 API (기존 이름 유지)

    public float GetTimeScale(TimeCategory cat)
    {
        int i = (int)cat;
        return (uint)i < (uint)CategoryCount && _channels != null ? _channels[i].scale : 1f;
    }

    public float GetDeltaTime(TimeCategory cat) => Time.unscaledDeltaTime * GetTimeScale(cat);
    public float GetFixedDeltaTime(TimeCategory cat) => Time.fixedUnscaledDeltaTime * GetTimeScale(cat);

    public float PlayerTimeScale => GetTimeScale(TimeCategory.Player);
    public float EnemyTimeScale => GetTimeScale(TimeCategory.Enemy);
    public float GhostTimeScale => GetTimeScale(TimeCategory.Ghost);

    public float PlayerDeltaTime => GetDeltaTime(TimeCategory.Player);
    public float PlayerFixedDeltaTime => GetFixedDeltaTime(TimeCategory.Player);
    public float EnemyDeltaTime => GetDeltaTime(TimeCategory.Enemy);
    public float EnemyFixedDeltaTime => GetFixedDeltaTime(TimeCategory.Enemy);
    public float GhostDeltaTime => GetDeltaTime(TimeCategory.Ghost);
    public float GhostFixedDeltaTime => GetFixedDeltaTime(TimeCategory.Ghost);

    /// <summary>히트스탑 진행 중 여부</summary>
    public bool IsHitstopActive => _hitstopActive;

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 요청 API (새 방식)

    /// <param name="duration">0 이하면 Release할 때까지 유지, 양수면 실제 시간 duration초 뒤 자동 해제</param>
    public Handle Request(TimeCategory cat, float scale, float duration = 0f, string tag = null)
        => AddRequest(1 << (int)cat, scale, duration, tag, isHitstop: false);

    /// <summary>모든 카테고리에 같은 요청을 넣는다. 핸들 하나로 함께 해제된다.</summary>
    public Handle RequestAll(float scale, float duration = 0f, string tag = null)
        => AddRequest(~0, scale, duration, tag, isHitstop: false);

    /// <summary>히트스탑 요청 (모든 카테고리, IsHitstopActive/OnHitstopStateChanged에 반영)</summary>
    public Handle RequestHitstop(float scale, float duration, string tag = "Hitstop")
        => AddRequest(~0, scale, duration, tag, isHitstop: true);

    /// <summary>요청 배율 변경 (매 프레임 바꿔도 할당 없음). 이미 해제된 핸들이면 false.</summary>
    public bool SetScale(Handle h, float scale)
        => Modify(h, Field.Scale, Sanitize(scale));

    /// <summary>해제 예약 시각을 지금부터 duration초 뒤로 다시 잡는다. 0 이하면 예약 취소(무기한).</summary>
    public bool SetDuration(Handle h, float duration)
        => Modify(h, Field.ReleaseAt, ToReleaseAt(duration));

    public bool IsActive(Handle h) => h.IsValid && FindAny(h.Id, out _, out _);

    /// <summary>남은 시간(실제 초). 무기한이거나 없으면 0.</summary>
    public float GetRemaining(Handle h)
    {
        if (!h.IsValid || !FindAny(h.Id, out int c, out int idx)) return 0f;
        var r = _channels[c].requests[idx];
        return r.HasRelease ? Mathf.Max(0f, r.releaseAt - Time.unscaledTime) : 0f;
    }

    /// <summary>요청 해제. 핸들은 무효값으로 바뀐다.</summary>
    public void Release(ref Handle h)
    {
        if (!h.IsValid) return;
        int id = h.Id;
        h = default;
        if (_channels == null) return;

        for (int i = 0; i < _channels.Length; i++)
        {
            if (_channels[i].requests.RemoveAll(r => r.id == id) > 0)
                Recalculate(i);
        }
        UpdateHitstopState();
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 기존 API (호출부 수정 없이 동작하도록 유지)

    /// <param name="scale">적용할 배율 (0 = 완전 정지, 1 = 기본)</param>
    /// <param name="duration">0이면 영구 변경, 양수면 duration초(실제 시간) 후 자동 해제</param>
    public void SetPlayerTimeScale(float scale, float duration = 0f) => SetLegacy(TimeCategory.Player, scale, duration);
    public void SetEnemyTimeScale(float scale, float duration = 0f) => SetLegacy(TimeCategory.Enemy, scale, duration);
    public void SetGhostTimeScale(float scale, float duration = 0f) => SetLegacy(TimeCategory.Ghost, scale, duration);

    /// <summary>히트스탑을 제외한 모든 요청 해제</summary>
    public void ResetAll()
    {
        EnsureInit();
        for (int i = 0; i < _channels.Length; i++)
        {
            if (_channels[i].requests.RemoveAll(r => !r.isHitstop) > 0)
                Recalculate(i);
            _legacyHandles[i] = default;
        }
    }

    /// <summary>히트스탑 배율을 직접 지정 (1 이상이면 해제). HitstopManager 구버전 호환용.</summary>
    public void SetHitstopMultiplier(float multiplier)
    {
        if (multiplier >= 1f) { ResetHitstopMultiplier(); return; }
        if (!SetScale(_legacyHitstop, multiplier))
            _legacyHitstop = RequestHitstop(multiplier, 0f, "Hitstop(legacy)");
    }

    /// <summary>모든 히트스탑 요청 해제</summary>
    public void ResetHitstopMultiplier()
    {
        EnsureInit();
        _legacyHitstop = default;
        for (int i = 0; i < _channels.Length; i++)
        {
            if (_channels[i].requests.RemoveAll(r => r.isHitstop) > 0)
                Recalculate(i);
        }
        UpdateHitstopState();
    }

    private void SetLegacy(TimeCategory cat, float scale, float duration)
    {
        ref Handle h = ref LegacyHandle(cat);

        // 기존 의미: 1f 영구 설정 = 기본으로 복구
        if (Mathf.Approximately(scale, 1f) && duration <= 0f) { Release(ref h); return; }

        if (IsActive(h))
        {
            SetScale(h, scale);
            SetDuration(h, duration);
        }
        else
        {
            h = Request(cat, scale, duration, $"{cat}(legacy)");
        }
    }

    private ref Handle LegacyHandle(TimeCategory cat)
    {
        EnsureInit();
        return ref _legacyHandles[(int)cat];
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 내부

    private enum Field { Scale, ReleaseAt }

    private Handle AddRequest(int categoryMask, float scale, float duration, string tag, bool isHitstop)
    {
        EnsureInit();
        var req = new TimeRequest
        {
            id = _nextId++,
            tag = tag,
            scale = Sanitize(scale),
            releaseAt = ToReleaseAt(duration),
            isHitstop = isHitstop,
        };

        for (int i = 0; i < _channels.Length; i++)
        {
            if ((categoryMask & (1 << i)) == 0) continue;
            _channels[i].requests.Add(req);
            Recalculate(i);
        }
        if (isHitstop) UpdateHitstopState();
        return new Handle(req.id);
    }

    private bool Modify(Handle h, Field field, float value)
    {
        if (!h.IsValid || _channels == null) return false;
        bool found = false;
        bool hitstop = false;

        for (int c = 0; c < _channels.Length; c++)
        {
            var list = _channels[c].requests;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != h.Id) continue;
                var r = list[i];
                if (field == Field.Scale) r.scale = value;
                else r.releaseAt = value;
                list[i] = r; // struct라 다시 써 넣어야 반영됨
                found = true;
                hitstop |= r.isHitstop;
                Recalculate(c);
            }
        }
        if (hitstop) UpdateHitstopState();
        return found;
    }

    private bool FindAny(int id, out int channel, out int index)
    {
        if (_channels != null)
        {
            for (int c = 0; c < _channels.Length; c++)
            {
                int i = _channels[c].requests.FindIndex(r => r.id == id);
                if (i >= 0) { channel = c; index = i; return true; }
            }
        }
        channel = index = -1;
        return false;
    }

    /// <summary>요청 목록으로 최종 배율·표시용 해제 시각을 다시 계산하고, 바뀌었으면 이벤트 발행</summary>
    private void Recalculate(int i)
    {
        ref TimeChannel ch = ref _channels[i];
        float scale = 1f;
        float releaseAt = NoRelease;
        foreach (var r in ch.requests)
        {
            scale *= r.scale;
            if (r.HasRelease && r.releaseAt > releaseAt) releaseAt = r.releaseAt;
        }
        ch.releaseAt = releaseAt;

        if (ch.scale == scale) return;
        ch.scale = scale;
        RaiseScaleChanged(ch.category, scale);
    }

    private void RaiseScaleChanged(TimeCategory cat, float scale)
    {
        OnTimeScaleChanged?.Invoke(cat, scale);
        switch (cat)
        {
            case TimeCategory.Player: OnPlayerTimeScaleChanged?.Invoke(scale); break;
            case TimeCategory.Enemy: OnEnemyTimeScaleChanged?.Invoke(scale); break;
            case TimeCategory.Ghost: OnCloneTimeScaleChanged?.Invoke(scale); break;
        }
    }

    private void UpdateHitstopState()
    {
        if (_channels == null) return;
        bool active = false;
        foreach (var ch in _channels)
        {
            foreach (var r in ch.requests)
                if (r.isHitstop && r.scale < 1f) { active = true; break; }
            if (active) break;
        }

        if (active == _hitstopActive) return;
        _hitstopActive = active;
        OnHitstopStateChanged?.Invoke(active);
    }

    private static bool HasExpired(in TimeChannel ch, float now)
    {
        foreach (var r in ch.requests)
            if (r.HasRelease && r.releaseAt <= now) return true;
        return false;
    }

    private static float Sanitize(float scale)
        => float.IsNaN(scale) || scale < 0f ? 0f : scale;

    private static float ToReleaseAt(float duration)
        => duration > 0f ? Time.unscaledTime + duration : NoRelease;

    #endregion
}
