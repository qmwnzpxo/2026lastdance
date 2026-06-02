using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Green Dash (초록 칩) 시스템 구현 - 3연속 돌진
/// </summary>
public partial class PlayerDash : MonoBehaviour
{
    void StartGreenDash()
    {
        if (_greenDashing || _greenComboStep > 0) return; // 이미 돌진 중이면 무시
        if (!state.TryUseDashGauge(state.DashCost)) return;

        _greenDashCts?.Cancel();
        _greenDashCts?.Dispose();
        _greenDashCts = new CancellationTokenSource();
        GreenDashComboRoutine(_greenDashCts.Token).Forget();
    }

    private async UniTaskVoid GreenDashComboRoutine(CancellationToken ct)
    {
        _greenComboStep = 0;
        _greenDashBuffered = false;

        try
        {
            while (_greenComboStep < greenDashCount && !ct.IsCancellationRequested)
            {
                // 1회 대쉬 수행
                await GreenDashSegment(ct);
                _greenComboStep++;

                if (_greenComboStep >= greenDashCount) break; // 마지막 대쉬 완료 → 종료

                playerMove.RestoreGravity();

                bool advanced = await WaitGreenWindow(ct);
                if (!advanced) break; // 입력 없음 → 콤보 종료(초기화)
            }
        }
        finally
        {
            EndGreenCombo();
        }
    }

    private async UniTask<bool> WaitGreenWindow(CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < _greenDashInterval)
        {
            if (ct.IsCancellationRequested) return false;
            if (_greenDashBuffered) return true; // 입력(또는 이동 중 버퍼된 입력) 감지

            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return false;

            elapsed += CustomTime.ScaledFixedDeltaTime(state.category);
        }
        return _greenDashBuffered;
    }

    private async UniTask GreenDashSegment(CancellationToken ct)
    {
        _greenDashing = true;
        _greenDashBuffered = false; // 이번 대쉬를 트리거한 입력 소모
        ani.Attack_flash();

        Vector2 dir = GetMouseDirection();
        cameraFollow.SetCameraDumping(cameraDumpingScale);

        Vector2 start = rb.position;
        Vector2 target = start + dir * greenDashRange;

        RaycastHit2D groundHit = Physics2D.Raycast(
            start + dir * 0.05f, dir, greenDashRange, playerMove.GroundLayer);
        if (groundHit.collider != null)
            target = groundHit.point;

        SetLineRenderer(start, target, true);

        rb.gravityScale = 0f;
        state.isDashing = true;
        rb.linearVelocity = Vector2.zero;

        HashSet<Collider2D> hitThisDash = new HashSet<Collider2D>();
        var timeManager = CustomTimeManager.Instance;
        float traveled = 0f;
        float dist = Vector2.Distance(start, target);

        while (!ct.IsCancellationRequested && traveled < dist)
        {
            Vector2 cur = rb.position;
            Vector2 next = Vector2.MoveTowards(cur, target, greenDashSpeed * timeManager.PlayerFixedDeltaTime);

            // 스윕 판정
            Vector2 sweepDir = next - cur;
            float sweepDist = sweepDir.magnitude;
            if (sweepDist > 0f)
            {
                var hits = Physics2D.BoxCastAll(
                    cur, attackBoxSize + Vector2.one * 0.1f, 0f,
                    sweepDir.normalized, sweepDist, enemyLayer);
                foreach (var h in hits)
                {
                    if (h.collider != null && !hitThisDash.Contains(h.collider))
                    {
                        hitThisDash.Add(h.collider);
                        ApplyDamage(h.collider, state.damage * dashDamage);
                        HitstopManager.Instance.TriggerHitstop(0.04f);
                    }
                }
            }

            rb.MovePosition(next);
            traveled += greenDashSpeed * timeManager.PlayerFixedDeltaTime;

            // 벽 충돌 시 돌진 중단
            Collider2D wallHit = Physics2D.OverlapBox(
                rb.position, dashBoxSize, 0f, playerMove.GroundLayer);
            if (wallHit != null) break;

            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) break;
        }

        state.isDashing = false;
        _greenDashing = false;
    }

    void EndGreenCombo()
    {
        _greenComboStep = 0;
        _greenDashBuffered = false;
        _greenDashing = false;

        state.isDashing = false;
        state.NotifyDashStopped();
        playerMove.RestoreGravity();
        cameraFollow.SetCameraDumping(originalCameraDumping);
        rb.linearVelocity = Vector2.up * 0.05f;
        if (line != null) line.enabled = false;
    }
}