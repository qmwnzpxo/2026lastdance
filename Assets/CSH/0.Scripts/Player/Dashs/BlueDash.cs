using Cysharp.Threading.Tasks;
using System.Collections.Generic;
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
            // 대시 경로 적 판정(SweepDashEnemies)의 시작점. 설정하지 않으면 이전 핑크 대시 시작점(또는 원점)부터 판정됨
            dashOrigin = rb.position;

            Vector2 origin = (Vector2)transform.position + dashDirection * 0.05f;
            RaycastHit2D groundHit = SolidPhysics.Raycast(
                origin, dashDirection, dashRange, playerMove.GroundLayer);
            // 벽·천장에 닿으면 표면에서 몸 반지름만큼 법선 방향으로 띄운 위치로 이동
            // (표면 좌표 그대로 가면 몸이 지형에 박혀, 이어지는 낙하가 그 자리를 바닥으로 인식함)
            GetBodyCircle(out _, out float bodyRadius);
            dashTargetPosition = groundHit.collider != null
                ? groundHit.point + groundHit.normal * (bodyRadius + BlueSkin)
                : (Vector2)transform.position + dashDirection * dashRange;

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

    private const float BlueSkin = 0.01f; // 지형 표면에서 살짝 띄우는 간격 (겹침 방지)
    private readonly HashSet<GameObject> _blueSlamTargets = new(); // 착지 강타 중복 판정 방지용

    /// <summary>
    /// 플레이어 몸(원형 콜라이더)의 월드 중심·반지름 (스케일·오프셋 반영).
    /// circleCol은 첫 번째 콜라이더가 원형일 때만 채워지므로, 없으면 원형 콜라이더를 직접 찾는다.
    /// </summary>
    private void GetBodyCircle(out Vector2 center, out float radius)
    {
        if (circleCol == null) circleCol = GetComponent<CircleCollider2D>();
        if (circleCol != null)
        {
            center = circleCol.bounds.center;
            radius = circleCol.bounds.extents.x;
        }
        else
        {
            center = rb.position;
            radius = 0.5f;
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
        var hPlayer = _blueTimePlayer = tmg.Request(TimeCategory.Player, blueQTEStartScale, tag: "BlueQTE");
        var hEnemy = _blueTimeEnemy = tmg.Request(TimeCategory.Enemy, blueQTEStartScale, tag: "BlueQTE");

        float elapsed = 0f;

        try
        {
            // 낙하 입력(우클릭/스페이스)은 PlayerDash.HandleDash에서 처리 → 여기선 슬로우 연출만
            // 시작 배율 → 1까지 선형 복귀 (끝날 때 배율이 튀지 않도록 1에서 끝냄)
            while (elapsed < blueQTEDuration && !ct.IsCancellationRequested)
            {
                float scale = Mathf.Lerp(blueQTEStartScale, 1f, elapsed / blueQTEDuration);
                tmg.SetScale(hPlayer, scale);
                tmg.SetScale(hEnemy, scale);
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

        // 고속 낙하 시 지형 관통 방지: 실제 콜라이더 크기(스케일·오프셋 반영)로 CircleCast
        GetBodyCircle(out Vector2 center, out float radius);
        Vector2 centerOffset = center - cur; // rb 위치 → 콜라이더 중심

        // 위를 향한 면(바닥·완만한 경사)만 착지로 인정 → 벽에 붙어 떨어질 때 벽을 바닥으로 오인하지 않음
        RaycastHit2D hit = SolidPhysics.CircleCastFloor(
            center, radius, Vector2.down, step + 0.1f, playerMove.GroundLayer);

        if (hit.collider != null)
        {
          // 착지 지점에 딱 맞춰 이동 후 강타
          // hit.centroid = 원이 바닥에 닿는 순간의 원 중심 → 콜라이더 오프셋만큼 되돌려 rb 위치로 변환
          Vector2 landPos = hit.centroid - centerOffset + Vector2.up * BlueSkin;
          rb.MovePosition(new Vector2(cur.x, landPos.y));
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

        // 범위 내 적 전체 데미지 (적 하나에 콜라이더가 여러 개여도 한 번만)
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, blueSlamRadius, enemyLayer);
        _blueSlamTargets.Clear();
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            // 같은 적 판별: 리지드바디가 있으면 그 오브젝트, 없으면 콜라이더 오브젝트 기준
            GameObject owner = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (!_blueSlamTargets.Add(owner)) continue;
            //    ApplyDamage(hit, state.damage * blueSlamDamage);
            OnDashHitEnemy(hit);
        }
        _blueSlamTargets.Clear();


        // 상태 복구
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.zero;
        _blueDashSecondInput = false;

        HitstopManager.Instance.TriggerHeavy();
    }
}