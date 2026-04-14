using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;


/// <summary>
/// 플레이어 공유 데이터 허브.
/// PlayerMove / PlayerCombat / PlayerDash 모두 이 컴포넌트를 참조한다.
/// ※ 기존 PlayerState의 HP / TakeDamage 구현은 이 파일에 병합할 것.
/// </summary>
[DefaultExecutionOrder(-2)]
public class PlayerState : MonoBehaviour
{
    // ── 스탯 ──────────────────────────────────
    [Header("스탯")]
    public float damage = 10f;
    public float HP = 10;
    public float MaxHP = 10;
    [RuntimeOnly, SerializeField] public bool isDie = false;

    // ── 대쉬 게이지 ───────────────────────────
    [Header("대쉬 게이지")]
    [SerializeField] private float dashGauge = 50f;
    [SerializeField] private float maxDashGauge = 50f;
    public float dashGaugePerAttack = 0.3f;

    public float DashGauge => dashGauge;
    public float MaxDashGauge => maxDashGauge;

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

    // ── 액션 상태 (컴포넌트 간 상호 참조용) ──
    [Header("액션 상태")]
    [RuntimeOnly, SerializeField] public bool isDashing;
    [RuntimeOnly, SerializeField] public bool isTackling;
    [RuntimeOnly, SerializeField] public bool isTackleGroundRush;
    [RuntimeOnly, SerializeField] public bool isAttacking;
    [RuntimeOnly, SerializeField] public bool isHit;
    public Light2D hitLight;

    CancellationTokenSource hitCts;
    public Action Attack;


    // ── 게이지 메서드 ─────────────────────────
    public void AddDashGauge(float amount)
    {
        dashGauge = Mathf.Min(dashGauge + amount, maxDashGauge);
    }

    /// <summary>게이지 소비. 잔량 부족 시 false 반환.</summary>
    public bool TryUseDashGauge(float amount)
    {
        if (dashGauge < amount) return false;
        dashGauge -= amount;
        return true;
    }

    // ── HP / 피격 ─────────────────────────────
    // TODO: 기존 PlayerState의 HP, TakeDamage 등 구현을 여기에 병합
    public void TakeDamage(float dmg)
    {
        // 기존 구현 병합 위치
        HP -= damage;
        if (HP <= 0)
            Die();
        else
            Hited();
    }

    public void Hited()
    {
        if (isHit) return;

        hitCts?.Cancel();
        hitCts?.Dispose();
        hitCts=new CancellationTokenSource();

        Hitglow(hitCts.Token).Forget();
    }

    public async UniTaskVoid Hitglow(CancellationToken tk)
    {
        hitLight.enabled = true;
        var result = await UniTask.WaitForSeconds(0.3f, cancellationToken: tk)
            .SuppressCancellationThrow();
        if (!result)
        {
            hitLight.enabled = false;
        }
    }
    void Die()
    {
        Debug.Log("Player has died.");
        {
            //playerMove.CancelAllTask();

        }
    }
    void OnDestroy() // 추가
    {
        hitCts?.Cancel();
        hitCts?.Dispose();
    }
}
