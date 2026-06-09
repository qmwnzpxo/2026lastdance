using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using CSHPlayer;

/// <summary>
/// Red Dash 시스템 구현
/// 너무 복잡하고 길어서 따로 분리함
/// </summary>
public partial class PlayerDash : MonoBehaviour
{
    SpriteRenderer playerSprite;
    SpriteRenderer ghostSprite;
    PlayerCombat combat;

    void StartRedDash()
    {
        if (!state.TryUseDashGauge(state.DashCost)) return;

        _redDashCts?.Cancel();
        _redDashCts?.Dispose();
        _redDashCts = new CancellationTokenSource();

        RedDashRoutine(_redDashCts.Token).Forget();
    }

    private async UniTaskVoid RedDashRoutine(CancellationToken ct)
    {
        // 본체 유령화 및 초기화
        state.isRedDashRecording = true;
        moveRecords.Clear();
        eventRecords.Clear();
        enemySnapshots.Clear();

        // 시작 위치 저장 및 잔상 초기화
        Vector2 startPosition = transform.position;
        Ghost.transform.position = startPosition;
        ghostSprite.sprite = playerSprite.sprite;
        ghostSprite.color = new Color(1f, 1f, 1f, 0.4f);

        // 본체(유령 역할)를 붉고 반투명하게 변경
        Color originalColor = playerSprite.color;
        playerSprite.color = new Color(1f, 0.3f, 0.3f, 0.7f);

        // 타임 스케일 설정 (본체 1.2배 가속, 적 0.1배 슬로우)
        CaptureEnemySnapshots();

        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.1f);

        float recordTimer = 0f;
        float recordInterval = 1f / redRecordFPS;
        float nextRecordTime = 0f;

        try
        {
            while (recordTimer < redRecordDuration && !ct.IsCancellationRequested)
            {
                recordTimer += Time.fixedUnscaledDeltaTime;

                // 이동 프레임 기록
                if (recordTimer >= nextRecordTime)
                {
                    bool running = Mathf.Abs(Input.GetAxis("Horizontal")) > 0f
                        || Mathf.Abs(rb.linearVelocityX) > 0.01f;
                    if (state.isWallSliding)
                        running = Input.GetAxisRaw("Vertical") != 0f;

                    moveRecords.Add(new GhostMoveData
                    {
                        timestamp = recordTimer,
                        position = transform.position,
                        flipX = playerSprite.flipX,
                        spriteScale = playerMove.SpriteTransform.localScale,
                        spriteRotation = playerMove.SpriteTransform.rotation,
                        isRunning = running,
                        isPowerRunning = state.isPowerRun,
                    });

                    nextRecordTime += recordInterval;
                }

                await UniTask.WaitForFixedUpdate(cancellationToken: ct);
            }



            if (Ghost != null) Ghost.SetActive(false);
            transform.position = startPosition;
            state.isRedDashRecording = false;
            playerSprite.color = originalColor;

            if (ct.IsCancellationRequested) return;


            // 재생 페이즈
            await PlaybackRoutine(ct);
        }
        finally
        {
            // CTS 취소든 정상 완료든 반드시 구독 해제
            state.isRedDashRecording = false;
            playerSprite.color = originalColor;
            Ghost.SetActive(false);
        }
    }

    private async UniTask PlaybackRoutine(CancellationToken ct)
    {
        state.isRedDashPlaying = true;
        state.StartImmunity(0.08f);

        ReturnAllEnemyBullets();
        RestoreEnemySnapshots();

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.8f);

        if (combat == null)
            combat = GetComponent<PlayerCombat>();

        float replayTimer = 0f;
        int moveIndex = 0;
        int eventIndex = 0;

        // 공격 기록을 큐로 변환해서 순서대로 소비
        var attackQueue = new Queue<GhostEventData>(eventRecords);

        try
        {
            while (replayTimer < redRecordDuration && !ct.IsCancellationRequested)
            {
                if (replayTimer > 0.08f && state.isHit) break;

                replayTimer += tmg.PlayerFixedDeltaTime;

                while (moveIndex < moveRecords.Count - 1
                    && replayTimer >= moveRecords[moveIndex + 1].timestamp)
                {
                    moveIndex++;
                }

                // 위치 재생
                if (moveIndex < moveRecords.Count)
                {
                    var m = moveRecords[moveIndex];
                    rb.MovePosition(Vector2.Lerp(rb.position, m.position, 0.5f));
                    playerSprite.flipX = m.flipX;
                    playerMove.SpriteTransform.localScale = m.spriteScale;
                    playerMove.SpriteTransform.rotation = m.spriteRotation;

                    if (m.isRunning)
                    {
                        ani.Run_on();
                        if (m.isPowerRunning) ani.Power_Run_on();
                        else ani.Power_Run_off();
                    }
                    else
                    {
                        ani.Run_off();
                        ani.Power_Run_off();
                    }
                }


                // 현재 재생 프레임 인덱스에 해당하는 이벤트가 있으면 즉시 실행
                while (eventIndex < eventRecords.Count
                && replayTimer >= eventRecords[eventIndex].timestamp)
                    {
                        var ev = eventRecords[eventIndex];
                        switch (ev.eventType)
                        {
                            case GhostEventType.Attack:
                                combat.TriggerRedDashAttack(ev.rotation);
                                break;

                            case GhostEventType.Jump:
                                ani.Jump_on();
                                break;

                            case GhostEventType.Item:
                            //  todo:아이템 이벤트 실행
                                break;
                        }
                        eventIndex++;
                    }

                await UniTask.WaitForFixedUpdate(cancellationToken: ct);
            }
        }
        finally
        {
            state.isRedDashPlaying = false;
            playerMove.RestoreGravity();
            ani.Run_off();
            ani.Power_Run_off();

            tmg.SetPlayerTimeScale(1f);
            tmg.SetEnemyTimeScale(1f);
        }
    }

    // ──── 적 스냅샷 ─────────────────────────────────
    #region
    private void CaptureEnemySnapshots()
    {
        var controllers = FindObjectsByType<EnemyBTController>(FindObjectsSortMode.None);
        foreach (var ctrl in controllers)
        {
            var bb = ctrl._BB;
            if (bb == null) continue;

            enemySnapshots.Add(new EnemySnapshotData
            {
                controller = ctrl,
                state = ctrl.GetComponent<EnemyState>(),
                position = ctrl.transform.position,
                hp = ctrl.GetComponent<EnemyState>()?.currentHp ?? 0f,
                isDetected = bb.isDetected,
                detectGauge = bb.detectGauge,
                chaseEndTimer = bb.chaseEndTimer,
                isStunned = bb.isStunned,
                lastAttackTime = bb.lastAttackTime,
            });
        }
    }
    private void RestoreEnemySnapshots()
    {
        foreach (var snap in enemySnapshots)
        {
            if (snap.controller == null || snap.state == null) continue;

            // 위치 복원
            snap.controller.transform.position = snap.position;

            // HP 복원
            snap.state.currentHp = snap.hp;

            // BT 재초기화 후 블랙보드 값 덮어쓰기
            snap.controller.requireInit = true;
            snap.controller.ReInit();

            var bb = snap.controller._BB;
            if (bb == null) continue;

            bb.isDetected = snap.isDetected;
            bb.detectGauge = snap.detectGauge;
            bb.chaseEndTimer = snap.chaseEndTimer;
            bb.isStunned = snap.isStunned;
            bb.lastAttackTime = snap.lastAttackTime;
        }
    }
    #endregion


    // ────────────────────────────────────────────────────
    #region 발사체 정리

    private void ReturnAllEnemyBullets()
    {
        // 씬에 활성화된 EnemyBullet 전부 풀 반환
        var bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in bullets)
        {
            if (bullet.gameObject.activeSelf)
                EnemyBulletPool.Instance.Return(bullet.poolKey, bullet.gameObject);
        }
    }

    #endregion
}