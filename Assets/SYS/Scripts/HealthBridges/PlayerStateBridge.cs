// =============================================================================
//  PlayerHealthBarBridge.cs
//
//  PlayerState ↔ UnifiedHealthBarVisual 연결 브릿지.
//  HP / 스태미나 / 대쉬게이지 세 가지 바를 각각 독립적으로 연결합니다.
//
//  씬 설정:
//    1. 아무 오브젝트(예: UI Manager)에 이 컴포넌트 추가
//    2. Inspector 에서 playerState → 플레이어 드래그
//    3. 필요한 바만 연결 (연결 안 한 슬롯은 자동으로 무시)
//
//  바를 하나만 쓰고 싶으면:
//    hpBar 만 연결하고 staminaBar / dashBar 는 비워두면 됩니다.
// =============================================================================

using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStateBridge : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("데이터 소스 (필수)")]
    [SerializeField] PlayerState playerState;

    [Header("HP 바")]
    [SerializeField] UnifiedHealthBarVisual hpBar;
    [SerializeField] bool hpGhostFill = true;
    [SerializeField] bool hpGlow      = false;
    [SerializeField] bool hpInnerGlow = false;
    [SerializeField] bool hpShimmer   = false;
    [SerializeField] bool hpShake     = true;

    [Header("스태미나 바")]
    [SerializeField] UnifiedHealthBarVisual staminaBar;
    [SerializeField] bool staminaGhostFill = false;
    [SerializeField] bool staminaGlow      = false;
    [SerializeField] bool staminaShimmer   = false;

    [Header("대쉬 게이지 바")]
    [SerializeField] UnifiedHealthBarVisual dashBar;
    [SerializeField] bool dashGhostFill = false;
    [SerializeField] bool dashGlow      = false;
    [SerializeField] bool dashShimmer   = false;

    // ── 내부 변수 ─────────────────────────────────────────────────────────────

    float prevHP      = -1f;
    float prevStamina = -1f;
    float prevDash    = -1f;

    // ── Unity 생명주기 ────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerState == null)
            playerState = FindFirstObjectByType<PlayerState>();

        if (playerState == null)
            Debug.LogError("[PlayerHealthBarBridge] PlayerState 를 찾을 수 없습니다.", this);
    }

    void Start()
    {
        if (playerState == null) return;

        // HP 바 초기화
        if (hpBar != null)
        {
            hpBar.SetGhostFill(hpGhostFill);
            hpBar.SetGlow(hpGlow);
            hpBar.SetInnerGlow(hpInnerGlow);
            hpBar.SetShimmer(hpShimmer);
            SyncHP();
            prevHP = playerState.currentHP;
        }

        // 스태미나 바 초기화
        if (staminaBar != null)
        {
            staminaBar.SetGhostFill(staminaGhostFill);
            staminaBar.SetGlow(staminaGlow);
            staminaBar.SetShimmer(staminaShimmer);
            SyncStamina();
            prevStamina = playerState.stamina;
        }

        // 대쉬 게이지 바 초기화
        if (dashBar != null)
        {
            dashBar.SetGhostFill(dashGhostFill);
            dashBar.SetGlow(dashGlow);
            dashBar.SetShimmer(dashShimmer);
            SyncDash();
            prevDash = playerState.DashGauge;
        }
    }

    void Update()
    {
        if (playerState == null) return;

        // ── HP ────────────────────────────────────────────────────────────────
        if (hpBar != null)
        {
            float hp = playerState.currentHP;
            if (!Mathf.Approximately(hp, prevHP))
            {
                float delta = prevHP - hp;
                SyncHP();
                if (hpShake && delta > 0f)
                    hpBar.PlayShakeByDamage(delta, playerState.maxHP);
                prevHP = hp;
            }
        }

        // ── 스태미나 ──────────────────────────────────────────────────────────
        if (staminaBar != null)
        {
            float stamina = playerState.stamina;
            if (!Mathf.Approximately(stamina, prevStamina))
            {
                SyncStamina();
                prevStamina = stamina;
            }
        }

        // ── 대쉬 게이지 ───────────────────────────────────────────────────────
        if (dashBar != null)
        {
            float dash = playerState.DashGauge;
            if (!Mathf.Approximately(dash, prevDash))
            {
                SyncDash();
                prevDash = dash;
            }
        }
    }

    // ── 동기화 헬퍼 ───────────────────────────────────────────────────────────

    void SyncHP()
    {
        float ratio = playerState.maxHP > 0f
            ? Mathf.Clamp01(playerState.currentHP / playerState.maxHP)
            : 0f;
        hpBar.SetFill(ratio);
    }

    void SyncStamina()
    {
        float ratio = playerState.maxStamina > 0f
            ? Mathf.Clamp01(playerState.stamina / playerState.maxStamina)
            : 0f;
        staminaBar.SetFill(ratio);
    }

    void SyncDash()
    {
        float ratio = playerState.MaxDashGauge > 0f
            ? Mathf.Clamp01(playerState.DashGauge / playerState.MaxDashGauge)
            : 0f;
        dashBar.SetFill(ratio);
    }

    // ── 공개 API ──────────────────────────────────────────────────────────────

    /// <summary>모든 바를 즉시 강제 동기화.</summary>
    public void ForceSync()
    {
        if (playerState == null) return;
        if (hpBar      != null) { SyncHP();      prevHP      = playerState.currentHP; }
        if (staminaBar != null) { SyncStamina(); prevStamina = playerState.stamina; }
        if (dashBar    != null) { SyncDash();    prevDash    = playerState.DashGauge; }
    }

    /// <summary>HP 바 흔들림 수동 재생.</summary>
    public void TriggerHpShake(float duration = 0.25f)
    {
        if (hpBar != null) hpBar.PlayShake(duration);
    }
}
