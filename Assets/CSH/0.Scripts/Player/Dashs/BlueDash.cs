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
        if (state.TryUseDashGauge(state.DashCost))
        {
            // 마우스 방향을 대각선 2방향 중 하나로 스냅
            Vector2 rawDir = GetMouseDirection();
            Vector2 snappedDir = SnapToDiagonal(rawDir);

            dashDirection = snappedDir;

            Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
            RaycastHit2D groundHit = SolidPhysics.Raycast(
                origin, dashDirection, dashRange, playerMove.GroundLayer);
            dashTargetPosition = groundHit.collider != null
                ? groundHit.point
                : (Vector2)transform.position + dashDirection * dashRange;

            SetLineRenderer(transform.position, dashTargetPosition, true);

            _blueDashSecondInput = false;

            state.isGrounded = false;
            state.isOnCeiling = false;
            state.isTouchingLWall = false;
            state.isTouchingRWall = false;

            rb.gravityScale = 0f;
            cameraFollow.SetCameraDumping(cameraDumpingScale);
            dashHitEnemy = false;
            hitEnemiesInDash.Clear();

            state.isDashing = true;
            // 벽에 붙은 채(스프라이트 90° 회전) 시작하면 대시 내내 옆으로 누운 채 모션이 나옴 → 기본 자세로
            playerMove.ResetSpritePose();
            playerMove.SetFacingRight(dashDirection.x >= 0f);
            ani.BlueDash(); // 낙섬 모션
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

    // QTE 슬로우 요청 (TriggerBlueFall·CtsFree에서 즉시 해제하기 위해 보관)
    private CustomTimeManager.Handle _blueTimePlayer;
    private CustomTimeManager.Handle _blueTimeEnemy;

    private async UniTaskVoid BlueQTERoutine(CancellationToken ct)
    {
        var tmg = CustomTimeManager.Instance;

        // 이전 QTE 요청이 남아 있으면 정리 후 새로 요청 (플레이어 + 적 슬로우)
        ReleaseBlueTimeScale();
        var hPlayer = _blueTimePlayer = tmg.Request(TimeCategory.Player, 0.02f, tag: "BlueQTE");
        var hEnemy = _blueTimeEnemy = tmg.Request(TimeCategory.Enemy, 0.02f, tag: "BlueQTE");

        float elapsed = 0f;

        try
        {
            // 낙하 입력(우클릭/스페이스)은 PlayerDash.HandleDash에서 처리 → 여기선 슬로우 연출만
            while (elapsed < 1f && !ct.IsCancellationRequested)
            {
                tmg.SetScale(hPlayer, elapsed / 2);
                tmg.SetScale(hEnemy, elapsed / 2);
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);
            }
        }
        finally
        {
            // 슬로우 해제 (취소돼도 반드시). 지역 복사본이라 그 사이 시작된 새 QTE 요청은 건드리지 않음
            tmg.Release(ref hPlayer);
            tmg.Release(ref hEnemy);
        }
    }

    private void ReleaseBlueTimeScale()
    {
        var tmg = CustomTimeManager.Instance;
        if (tmg == null) return;
        tmg.Release(ref _blueTimePlayer);
        tmg.Release(ref _blueTimeEnemy);
    }

    /// <summary>우클릭/스페이스 입력으로 낙하 시작 (QTE 슬로우 중이면 즉시 해제)</summary>
    void TriggerBlueFall()
    {
        _blueAirAfterDash = false;
        _blueDashSecondInput = true; // 낙하 중 추가 대시 입력 차단 (BlueSlamLand에서 해제)

        _blueQTECts?.Cancel();
        ReleaseBlueTimeScale(); // 취소는 다음 프레임에 처리되므로 슬로우는 여기서 즉시 해제

        playerMove.ResetWallState();
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
        RaycastHit2D hit;
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
        hit = SolidPhysics.CircleCast(cur, GetComponent<CircleCollider2D>().radius, Vector2.down, step + 0.1f, playerMove.GroundLayer);

        if (hit.collider != null)
        {
          // 착지 지점에 딱 맞춰 이동 후 강타
          rb.MovePosition(new Vector2(cur.x, hit.point.y * 0.05f));
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