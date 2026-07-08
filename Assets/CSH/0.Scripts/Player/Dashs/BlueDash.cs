using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// Blue Dash (파랑 칩) 시스템 구현 - 대각 대시 + 낙하 강타
/// </summary>
public partial class PlayerDash : MonoBehaviour
{
    void ExecuteBlueDash()
    {
        // 마우스 방향을 대각선 2방향 중 하나로 스냅
        Vector2 rawDir = GetMouseDirection();
        Vector2 snappedDir = SnapToDiagonal(rawDir);

        dashDirection = snappedDir;

        Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin, dashDirection, dashRange, playerMove.GroundLayer);
        dashTargetPosition = groundHit.collider != null
            ? groundHit.point
            : (Vector2)transform.position + dashDirection * dashRange;

        SetLineRenderer(transform.position, dashTargetPosition, true);

        _blueDashSecondInput = false;

        if (state.TryUseDashGauge(state.DashCost))
        {
            state.isGrounded = false;
            state.isOnCeiling = false;
            state.isTouchingLWall = false;
            state.isTouchingRWall = false;

            rb.gravityScale = 0f;
            cameraFollow.SetCameraDumping(cameraDumpingScale);
            dashHitEnemy = false;
            hitEnemiesInDash.Clear();

            state.isDashing = true;
            rb.linearVelocity = dashDirection * dashSpeed;
        }
    }

    /// <summary>입력 벡터를 가장 가까운 45° 대각선(상단 2방향)으로 스냅</summary>
    private Vector2 SnapToDiagonal(Vector2 dir)
    {
        // 대각선 4방향: (±√0.5, ±√0.5)
        float x = dir.x >= 0f ? 1f : -1f;
        float y = 1f;
        return new Vector2(x, y).normalized;
    }

    void StartBlueQTE()
    {
        _blueQTECts?.Cancel();
        _blueQTECts?.Dispose();
        _blueQTECts = new CancellationTokenSource();
        BlueQTERoutine(_blueQTECts.Token).Forget();
    }

    private async UniTaskVoid BlueQTERoutine(CancellationToken ct)
    {
        var tmg = CustomTimeManager.Instance;

        // 플레이어 + 적 슬로우
        tmg.SetPlayerTimeScale(0.02f);
        tmg.SetEnemyTimeScale(0.02f);

        float elapsed = 0f;
        bool activated = false;

        while (elapsed < 1f && !ct.IsCancellationRequested)
        {
            tmg.SetPlayerTimeScale(elapsed / 2);
            tmg.SetEnemyTimeScale(elapsed / 2);
            elapsed += Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                activated = true;
                break;
            }
            // Yield으로 update 프레임마다 대기 (슬로우 중에도 입력 체크 가능)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);
        }

        // 슬로우 즉시 해제
        tmg.SetPlayerTimeScale(1f);
        tmg.SetEnemyTimeScale(1f);

        if (activated && !ct.IsCancellationRequested)
            StartBlueFall();
    }

    void StartBlueFall()
    {
        _blueFallCts?.Cancel();
        _blueFallCts?.Dispose();
        _blueFallCts = new CancellationTokenSource();

        _blueFalling = true;
        Debug.Log($"blueFall start");
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        cameraFollow.SetCameraDumping(cameraDumpingScale);
        ani.BlueDash(); // 낙섬 모션

        BlueFallRoutine(_blueFallCts.Token).Forget();
    }

    private async UniTaskVoid BlueFallRoutine(CancellationToken ct)
    {
        var tmg = CustomTimeManager.Instance;
        try
        {
            while (!ct.IsCancellationRequested && _blueFalling)
            {
                float step = blueFallSpeed * tmg.PlayerFixedDeltaTime;
                Vector2 cur = rb.position;
                Vector2 next = cur + Vector2.down * step;
                /*
                // 고속 낙하 시 지형 관통 방지: BoxCast로 착지 감지
                RaycastHit2D hit = Physics2D.BoxCast(
                    cur,
                    playerMove.GroundCheck,
                    0f,
                    Vector2.down,
                    step + 0.1f,
                    playerMove.GroundLayer);
                */
                RaycastHit2D hit = Physics2D.CircleCast(cur, GetComponent<CircleCollider2D>().radius, Vector2.down, step + 0.1f, playerMove.GroundLayer);

                if (hit.collider != null)
                {
                    // 착지 지점에 딱 맞춰 이동 후 강타
                    rb.MovePosition(new Vector2(cur.x, hit.point.y * 0.5f));
                    await UniTask.WaitForFixedUpdate(cancellationToken: ct);
                    BlueSlamLand();
                    return;
                }

                rb.MovePosition(next);
                await UniTask.WaitForFixedUpdate(cancellationToken: ct);
            }
        }
        finally
        {
            _blueFalling = false;
        }
    }

    void BlueSlamLand()
    {
        _blueFalling = false;
        Debug.Log($"blueFall end");

        // 범위 내 적 전체 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, blueSlamRadius, enemyLayer);
        foreach (var hit in hits)
            //    ApplyDamage(hit, state.damage * blueSlamDamage);
            OnDashHitEnemy(hit);


        // 상태 복구
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.zero;
        _blueDashSecondInput = false;
        if (line != null) line.enabled = false;

        HitstopManager.Instance.TriggerHeavy();
    }
}