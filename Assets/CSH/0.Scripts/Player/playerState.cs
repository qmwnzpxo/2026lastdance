using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

public enum PlayerChip
{
    pink,    red,    green,    blue,
}
/// <summary>
/// 플레이어 공유 데이터 허브.
/// PlayerMove / PlayerCombat / PlayerDash 모두 이 컴포넌트를 참조한다.
/// ※ 기존 PlayerState의 HP / TakeDamage 구현은 이 파일에 병합할 것.
/// </summary>
[DefaultExecutionOrder(-2)]
[RequireComponent(typeof(PlayerMove), typeof(PlayerCombat), typeof(PlayerDash))]
[RequireComponent(typeof(TimeScaleReceiver))]
public class PlayerState : MonoBehaviour, IDamageable
{
    // ── 스탯 ──────────────────────────────────
    [Header("스탯")]
    public float damage = 1f;
    public float currentHP = 100f;
    public float maxHP = 100f;
    public float stamina = 100f;
    public float maxStamina = 100f;
    [RuntimeOnly, SerializeField] public bool isDead = false;
    private float invincibilityTime = 0.3f;

    // ── 대쉬 게이지 ───────────────────────────
    [Header("대쉬 게이지")]
    [RuntimeOnly, SerializeField] private float dashGauge = 50f;
    [RuntimeOnly, SerializeField] private float maxDashGauge = 100f;
    private float dashCost = 20f;
    [RuntimeOnly, SerializeField] public float dashGaugePerDash = 10f;

    [RuntimeOnly, SerializeField] public bool isExhausted = false;
    public float exhaustRecoverThreshold = 20f;

    // ── 소모품(섬광탄) 설정 ───────────────────
    [Header("소모품")]
    public int flashCount;
    private int flashMaxCount = 3;

    public float DashGauge => dashGauge;
    public float MaxDashGauge { get => maxDashGauge; set => maxDashGauge = value; }
    public float DashCost => dashCost;

    // ── 중력 설정 ─────────────────────────────
    [Header("중력 설정")]
    [EditorOnly, SerializeField] public float defaultGravityScale = 1.2f;
    [EditorOnly, SerializeField] public float ceilingGravityScale = -1.2f;

    // ── 이동 상태 (PlayerMove가 쓰고, PlayerDash가 읽음) ──
    [Header("이동 상태")]
    [RuntimeOnly, SerializeField] public bool isGrounded;
    [RuntimeOnly, SerializeField] public bool isOnCeiling;
    [RuntimeOnly, SerializeField] public bool isTouchingLWall;
    [RuntimeOnly, SerializeField] public bool isTouchingRWall;
    [RuntimeOnly, SerializeField] public bool isWallSliding;
    [RuntimeOnly, SerializeField] public bool isWallJumping;
    [RuntimeOnly, SerializeField] public bool isPowerRun;
    [RuntimeOnly, SerializeField] public bool dashBuffered;
    [RuntimeOnly, SerializeField] public bool attackBuffered;

    // ── 액션 상태 (컴포넌트 간 상호 참조용) ──
    [Header("액션 상태")]
    [RuntimeOnly, SerializeField] public bool isDashing = false;
    [RuntimeOnly, SerializeField] public bool isAttacking = false;
    [RuntimeOnly, SerializeField] public bool isHit = false;
    [RuntimeOnly, SerializeField] public bool isRedDashRecording = false;
    [RuntimeOnly, SerializeField] public bool isRedDashPlaying = false;
    // ── 칩 설정 ─────────────────────────────
    [Header("칩 설정")]
    public PlayerChip currentChip = PlayerChip.pink;
    public List<PlayerChip> ownedChips = new List<PlayerChip>() { PlayerChip.pink }; // 보유한 칩 종류

    // ── 시간 설정 ─────────────────────────────
    public TimeCategory category = TimeCategory.Player;

    //public Light2D hitLight;
    //public Light2D hitWorldLight;
    CancellationTokenSource hitCts;
    //TrailRenderer moveTrail;

    // ── 이벤트 ─────────────────────────────
    //public event System.Action UseStamina;

    public event System.Action OnDashStopped;
    public void NotifyDashStopped() => OnDashStopped?.Invoke();

    private PlayerAnimation playerAnimation;
    private CancellationTokenSource deathCts;
    private float deathAnimationDuration = 0.85f;


    public void Awake()
    {
        //if (hitLight == null)
        //    hitLight = transform.Find("Hit Light").GetComponent<Light2D>();
        //if (hitWorldLight == null)
        //    hitWorldLight = transform.Find("Hit World Light").GetComponent<Light2D>();
        InitState();

        if(playerAnimation == null)
            playerAnimation = GetComponent<PlayerAnimation>();
    }

    public void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.LeftShift)&&Input.GetKeyDown(KeyCode.G))
        {
            GetChips(PlayerChip.red);
            GetChips(PlayerChip.green);
            GetChips(PlayerChip.blue);
        }
#endif
        if(isDead)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            MoveNextChip();
        }
    }

    // ── 게이지 메서드 ─────────────────────────
    public void AddDashGauge(float amount)
    {
        dashGauge = Mathf.Min(dashGauge + amount, maxDashGauge);
    }

    public void ModifyGauge(float value)
    {
        stamina += value;
        stamina = Mathf.Clamp(stamina, 0, maxStamina);

        if (stamina <= 0f)
        {
            stamina = 0f;
            isExhausted = true;
        }
        else if (isExhausted && stamina >= exhaustRecoverThreshold)
        {
            isExhausted = false;
        }
    }

    // 게이지 소비. 잔량 부족 시 false 반환
    public bool TryUseDashGauge(float amount)
    {
        if (dashGauge < amount) return false;
        dashGauge -= amount;
        return true;
    }

    public void GetChips(PlayerChip chip)
    {
        if (ownedChips.Contains(chip)) return; // 이미 보유한 칩이면 무시
        ownedChips.Add(chip);
    }

    public void InitState()
    {
        currentHP = maxHP;
        dashGauge = maxDashGauge;
        stamina = maxStamina;
        isExhausted = false;
#if UNITY_EDITOR
        dashCost = 1f;
        dashGaugePerDash = 10f;
#endif
        GetChips(PlayerChip.pink);
    }

    public void TakeDamage(float dmg)
    {
        if (isDead || isHit ) return;
        if (isDashing || isRedDashRecording) return;
        currentHP -= dmg;
        if (currentHP <= 0)
            Die();
        else
        {
            //HitstopManager.Instance.TriggerHitstop();
            StartImmunity(invincibilityTime);
        }
    }

    public void StartImmunity(float duration)
    {
        //if (isHit) return;

        isHit = true;
        hitCts?.Cancel();
        hitCts?.Dispose();
        hitCts=new CancellationTokenSource();

        HitFeedBack(hitCts.Token, duration).Forget();
    }

    public async UniTaskVoid HitFeedBack(CancellationToken tk, float duration = 0.3f)
    {
        await CustomTime.WaitForScaledSeconds(duration, category, tk)
            .SuppressCancellationThrow();
        // 새 무적(StartImmunity 재호출)이 덮어써서 취소됐으면 isHit은 새 루틴이 관리 → 여기서 끄지 않음
        if (tk.IsCancellationRequested) return;
        isHit = false;
        
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("Player died!");

        isDashing = false;
        isAttacking = false;

        playerAnimation?.Death();

        deathCts?.Cancel();
        deathCts?.Dispose();
        deathCts = new CancellationTokenSource();

        RespawnRoutine(deathCts.Token).Forget();
    }
    private async UniTaskVoid RespawnRoutine(CancellationToken ct)
    {
        // 사망 애니메이션 재생 대기 (실시간 기준, timeScale 영향 없음)
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(deathAnimationDuration),
            ignoreTimeScale: true,
            cancellationToken: ct
        ).SuppressCancellationThrow();

        if (ct.IsCancellationRequested) return;

        Respawn();
    }

    private void Respawn()
    {
        var point = RespawnPoint.FindNearestLeft(transform.position.x);

        if (point != null)
        {
            transform.position = point.Position;
        }
        // point가 null이면 제자리 부활 (위치 변경 없음)

        currentHP = maxHP;
        stamina = maxStamina;
        dashGauge = maxDashGauge;

        isDead = false;
        Debug.Log("[PlayerState] 리스폰 완료.");
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        currentHP = Mathf.Min(currentHP + amount, maxHP);
        dashGauge += Mathf.Min(dashGauge + amount, maxDashGauge);
        Debug.Log($"Player healed {amount}. Current HP: {currentHP}/{maxHP}");
    }

    public float GetCurrentHP()
    {
        return currentHP;
    }

    public float GetMaxHP()
    {
        return maxHP;
    }

    // ── 구역 저장 (SectionSave) ───────────────
    /// <summary>현재 상태를 구역 저장용으로 복사한다.</summary>
    public SectionPlayerState CaptureSectionState()
    {
        return new SectionPlayerState
        {
            valid = true,
            hp = currentHP,
            stamina = stamina,
            dashGauge = dashGauge,
            flashCount = flashCount,
            ownedChips = new List<PlayerChip>(ownedChips),
            currentChip = currentChip,
        };
    }

    /// <summary>구역 저장 상태를 적용한다. 최대치는 현재 스탯 기준으로 잘라낸다.</summary>
    public void RestoreSectionState(SectionPlayerState s)
    {
        if (s == null || !s.valid) return;

        currentHP = Mathf.Clamp(s.hp, 1f, maxHP); // 0 HP로 저장됐어도 바로 죽지 않도록 최소 1
        dashGauge = Mathf.Clamp(s.dashGauge, 0f, maxDashGauge);
        flashCount = Mathf.Clamp(s.flashCount, 0, flashMaxCount);

        stamina = s.stamina;
        isExhausted = false;
        ModifyGauge(0f); // 범위 보정 + 탈진 상태 재계산

        ownedChips = new List<PlayerChip>(s.ownedChips);
        if (!ownedChips.Contains(PlayerChip.pink)) ownedChips.Insert(0, PlayerChip.pink);
        currentChip = ownedChips.Contains(s.currentChip) ? s.currentChip : PlayerChip.pink;
    }

    public bool TryAddFlashbang()
    {
        if (flashCount >= flashMaxCount) return false;
        flashCount++;
        return true;
    }

    public bool IsDead()
    {
        return isDead;
    }

    private void MoveNextChip()
    {
        PlayerChip? nextChip = ownedChips
            .Where(chip => chip > currentChip)
            .OrderBy(chip => chip)
            .Cast<PlayerChip?>()
            .FirstOrDefault();

        if (nextChip.HasValue)
        {
            currentChip = nextChip.Value;
        }
        else
        {
            // 더 큰 값이 없으면 가장 작은 칩으로
            currentChip = ownedChips.Min();
        }

        Debug.Log(currentChip);
    }
    void OnDestroy()
    {
        hitCts?.Cancel();
        hitCts?.Dispose();

        deathCts?.Cancel();
        deathCts?.Dispose();
    }
}
