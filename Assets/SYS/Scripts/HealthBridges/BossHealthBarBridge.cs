// =============================================================================
//  BossHealthBarBridge.cs
//
//  DamageHandler(보스 HP 데이터) ↔ UnifiedHealthBarVisual(UI 렌더러) 연결 브릿지.
//  DamageHandler / UnifiedHealthBarVisual 양쪽 모두 수정하지 않습니다.
//
//  씬 설정:
//    1. UnifiedHealthBarVisual 이 붙어 있는 Image 오브젝트에 이 컴포넌트 추가
//    2. Inspector 에서 damageHandler → 보스 오브젝트 드래그
//                      healthBar     → 자기 자신(Image) 드래그
//
//  PlayerHealthBarBridge 와의 차이:
//    - Update() 폴링 없음. OnHPChanged 이벤트만 구독 (HP 변화 시에만 반응)
//    - OnDisable() 에서 이벤트 구독 해제 (보스 사망 / 페이즈 전환 시 안전 정리)
//    - 딜타임(BarrierBroken) 연출 연동 포함
// =============================================================================

using UnityEngine;
using BossSystem;

[DisallowMultipleComponent]
public class BossHealthBarBridge : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("연결 (필수)")]
    [Tooltip("보스 HP 데이터 소스")]
    [SerializeField] DamageHandler damageHandler;

    [Tooltip("UI 렌더러 (이 오브젝트 또는 자식)")]
    [SerializeField] UnifiedHealthBarVisual healthBar;

    [Header("셰이더 기능")]
    [Tooltip("고스트 HP 사용 여부")]
    [SerializeField] bool enableGhostFill = true;

    [Tooltip("외부 글로우 사용 여부")]
    [SerializeField] bool enableGlow = false;

    [Tooltip("내부 글로우 사용 여부")]
    [SerializeField] bool enableInnerGlow = false;

    [Tooltip("시머(광택선) 사용 여부")]
    [SerializeField] bool enableShimmer = false;

    [Header("연출")]
    [Tooltip("피격 흔들림 사용 여부")]
    [SerializeField] bool enableShake = true;

    [Tooltip("딜타임 시작 시 글로우 색상 강조 (SetGlow 가 ON 일 때만 유효)")]
    [SerializeField] bool enableDealTimeGlow = true;

    [Tooltip("딜타임 시작 시 흔들림 강도")]
    [SerializeField] Vector2 dealTimeShakeStrength = new Vector2(14f, 7f);

    [Tooltip("딜타임 시작 시 흔들림 지속 시간 (초)")]
    [SerializeField] float dealTimeShakeDuration = 0.4f;

    // ── 내부 변수 ─────────────────────────────────────────────────────────────

    float prevHP;
    float prevMaxHP;

    // ── Unity 생명주기 ────────────────────────────────────────────────────────

    void Awake()
    {
        // 자동 탐색: Inspector 연결을 빠뜨렸을 경우 대비
        if (damageHandler == null)
            damageHandler = GetComponentInParent<DamageHandler>();

        // UI 프리팹(BossBar_v2)처럼 보스와 떨어진 Canvas 에 있을 때: 씬에서 탐색
        if (damageHandler == null)
            damageHandler = FindFirstObjectByType<DamageHandler>();

        if (healthBar == null)
            healthBar = GetComponent<UnifiedHealthBarVisual>();

        if (damageHandler == null)
            Debug.LogError("[BossHealthBarBridge] DamageHandler 를 찾을 수 없습니다.", this);

        if (healthBar == null)
            Debug.LogError("[BossHealthBarBridge] UnifiedHealthBarVisual 를 찾을 수 없습니다.", this);
    }

    void OnEnable()
    {
        if (damageHandler == null) return;

        damageHandler.OnHPChanged      += HandleHPChanged;
        damageHandler.OnDeath          += HandleDeath;
        damageHandler.OnBarrierBroken  += HandleBarrierBroken;
        damageHandler.OnBarrierRestored += HandleBarrierRestored;
    }

    void OnDisable()
    {
        if (damageHandler == null) return;

        // 보스 사망 / 페이즈 전환 / 오브젝트 비활성화 시 누수 방지
        damageHandler.OnHPChanged      -= HandleHPChanged;
        damageHandler.OnDeath          -= HandleDeath;
        damageHandler.OnBarrierBroken  -= HandleBarrierBroken;
        damageHandler.OnBarrierRestored -= HandleBarrierRestored;
    }

    void Start()
    {
        if (!IsReady()) return;

        // 셰이더 기능 초기화
        healthBar.SetGhostFill(enableGhostFill);
        healthBar.SetGlow(enableGlow);
        healthBar.SetInnerGlow(enableInnerGlow);
        healthBar.SetShimmer(enableShimmer);

        // 초기 동기화
        prevHP    = damageHandler._currentHP;
        prevMaxHP = damageHandler.MaxHP;
        SyncFill(prevHP, prevMaxHP);
    }

    void Update()
    {
        if (!IsReady()) return;

        // ── K 키: 테스트 데미지 ───────────────────────────────────────────
        // 게임 뷰에서 K 키를 누르면 10 데미지를 줍니다.
        // 테스트 후 이 블록을 삭제하세요.
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log($"[Bridge] K 입력 — HP:{damageHandler._currentHP}/{damageHandler.MaxHP} " +
                      $"무적:{damageHandler.IsInvincible} 딜타임:{damageHandler.IsDealTimeActive}");
            damageHandler.TakeDamage(10f);
            Debug.Log($"[Bridge] 데미지 후 — HP:{damageHandler._currentHP}");
        }
        // ─────────────────────────────────────────────────────────────────

        float _currentHP = damageHandler._currentHP;
        float maxHP     = damageHandler.MaxHP;

        // HP 또는 MaxHP 둘 중 하나라도 바뀌면 UI 갱신
        // Inspector 에서 MaxHP 를 직접 수정해도 여기서 잡힘
        if (Mathf.Approximately(_currentHP, prevHP) &&
            Mathf.Approximately(maxHP,     prevMaxHP)) return;

        float damage = prevHP - _currentHP;
        if (enableShake && damage > 0f)
            healthBar.PlayShakeByDamage(damage, maxHP);

        SyncFill(_currentHP, maxHP);

        prevHP    = _currentHP;
        prevMaxHP = maxHP;
    }

    void SyncFill(float _currentHP, float maxHP)
    {
        float ratio = maxHP > 0f ? Mathf.Clamp01(_currentHP / maxHP) : 0f;
        healthBar.SetFill(ratio);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    /// <summary>HP 변화 시 자동 호출 (DamageHandler.OnHPChanged). Update() 와 이중으로 잡음.</summary>
    void HandleHPChanged(float _currentHP, float maxHP)
    {
        if (!IsReady()) return;
        // Update() 폴링과 중복되지만 이벤트가 먼저 오는 경우를 즉시 처리
        SyncFill(_currentHP, maxHP);
        prevHP    = _currentHP;
        prevMaxHP = maxHP;
    }

    /// <summary>보스 사망 시 자동 호출 (DamageHandler.OnDeath).</summary>
    void HandleDeath()
    {
        if (!IsReady()) return;

        healthBar.SetFill(0f);

        // 사망 시 글로우 / 시머 OFF
        healthBar.SetGlow(false);
        healthBar.SetShimmer(false);
        healthBar.SetGhostFill(false);
    }

    /// <summary>방어막 파훼(딜타임 시작) 시 자동 호출 (DamageHandler.OnBarrierBroken).</summary>
    void HandleBarrierBroken()
    {
        if (!IsReady()) return;

        // 딜타임 진입 강조 흔들림
        if (enableShake)
            healthBar.PlayShake(dealTimeShakeDuration, dealTimeShakeStrength);

        // 글로우 강조 (ON 상태일 때만)
        if (enableDealTimeGlow && enableGlow)
            healthBar.SetGlow(true);

        Debug.Log("[BossHealthBarBridge] 딜타임 시작 — 바 연출 적용.");
    }

    /// <summary>딜타임 종료(무적 복귀) 시 자동 호출 (DamageHandler.OnBarrierRestored).</summary>
    void HandleBarrierRestored()
    {
        if (!IsReady()) return;

        // 글로우를 원래 설정값으로 복귀
        if (enableDealTimeGlow && enableGlow)
            healthBar.SetGlow(enableGlow);

        Debug.Log("[BossHealthBarBridge] 딜타임 종료 — 바 연출 복귀.");
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    bool IsReady() => damageHandler != null && healthBar != null;

    // ── 공개 API (외부 호출용) ────────────────────────────────────────────────

    /// <summary>HP 바를 즉시 현재 HP 로 강제 동기화.</summary>
    public void ForceSync()
    {
        if (!IsReady()) return;
        healthBar.SetFill(damageHandler.HPRatio);
        prevHP = damageHandler._currentHP;
    }

    /// <summary>피격 흔들림 수동 재생.</summary>
    public void TriggerShake(float duration = 0.25f)
    {
        if (IsReady()) healthBar.PlayShake(duration);
    }
}
