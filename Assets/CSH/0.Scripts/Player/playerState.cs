using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    public float HP = 10;
    public float MaxHP = 10;
    public float stamina = 100f;
    public float maxStamina = 100f;
    [RuntimeOnly, SerializeField] public bool isDie = false;

    // ── 대쉬 게이지 ───────────────────────────
    [Header("대쉬 게이지")]
    [RuntimeOnly, SerializeField] private float dashGauge = 50f;
    [RuntimeOnly, SerializeField] private float maxDashGauge = 100f;
    private float dashCost = 1f;
    public float dashGaugePerAttack = 10f;

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
    [RuntimeOnly, SerializeField] public bool isDashing;
    [RuntimeOnly, SerializeField] public bool isTackling;
    [RuntimeOnly, SerializeField] public bool isTackleGroundRush;
    [RuntimeOnly, SerializeField] public bool isAttacking;
    [RuntimeOnly, SerializeField] public bool isHit;

    // ── 칩 설정 ─────────────────────────────
    [Header("칩 설정")]
    public PlayerChip currentChip = PlayerChip.pink;
    public HashSet<PlayerChip> ownedChips = new HashSet<PlayerChip>() { PlayerChip.pink }; // 보유한 칩 종류

    // ── 시간 설정 ─────────────────────────────
    public TimeCategory category = TimeCategory.Player;

    public Light2D hitLight;
    public Light2D hitWorldLight;
    CancellationTokenSource hitCts;
    //TrailRenderer moveTrail;

    // ── 이벤트 ─────────────────────────────
    //public event System.Action UseStamina;

    public event System.Action OnDashStopped;
    public void NotifyDashStopped() => OnDashStopped?.Invoke();


    public void Awake()
    {
        //if (hitLight == null)
        //    hitLight = transform.Find("Hit Light").GetComponent<Light2D>();
        //if (hitWorldLight == null)
        //    hitWorldLight = transform.Find("Hit World Light").GetComponent<Light2D>();
        InitState();
    }

    public void Update()
    {
        if(Input.GetKey(KeyCode.LeftShift)&&Input.GetKeyDown(KeyCode.G))
        {
            GetChips(PlayerChip.red);
            GetChips(PlayerChip.green);
            GetChips(PlayerChip.blue);
        }

        if (Input.GetKey(KeyCode.R))
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
        stamina = Mathf.Clamp(stamina, 0, 100);
    }

    /// <summary>게이지 소비. 잔량 부족 시 false 반환.</summary>
    public bool TryUseDashGauge(float amount)
    {
        if (dashGauge < amount) return false;
        dashGauge -= amount;
        return true;
    }

    /// <summary>
    /// 칩 획득. 중복 획득 방지 위해 HashSet으로 관리 
    /// </summary>
    public void GetChips(PlayerChip chip)
    {
        ownedChips.Add(chip);
    }

    public void InitState()
    {
        HP = MaxHP;
        dashGauge = maxDashGauge;
        stamina = maxStamina;
        GetChips(PlayerChip.pink);
    }

    public void Heal(float amount)
    {
        if(isDie) return;
        HP = Mathf.Min(HP + amount, MaxHP);
    }

    public void TakeDamage(float dmg)
    {
        if (isDashing)
            return;
        if(isHit || isDie) return;
        HP -= dmg;
        if (HP <= 0)
            Die();
        else
        {
            //HitstopManager.Instance.TriggerHitstop();
            StartImmunity();
        }
    }

    public void StartImmunity(float duration = 0.3f)
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
        //hitLight.enabled = true;
        //hitWorldLight.enabled = true;
        await CustomTime.WaitForScaledSeconds(duration, category, tk)
            .SuppressCancellationThrow();
        isHit = false;
        //hitLight.enabled = false;
        //hitWorldLight.enabled = false;
    }

    void Die()
    {
        Debug.Log("Player has died.");
        {
            //playerMove.CancelAllTask();

        }
    }

    public void Revive()
    {
        isDie = false;
        InitState();
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
    void OnDestroy() // 추가
    {
        hitCts?.Cancel();
        hitCts?.Dispose();
    }
}
