using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using CSHPlayer;

/// <summary>
/// Red Dash 시스템 구현
/// 너무 복잡하고 길어서 함수만 따로 분리함
/// </summary>
public partial class PlayerDash : MonoBehaviour
{
    SpriteRenderer playerSprite;
    
    SpriteRenderer ghostSprite;
    Rigidbody2D ghostRb;

    PlayerCombat combat;

    private int _currentMoveFrame = 0;

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
        _ghostMoveRecords.Clear();
        _ghostAttackRecords.Clear();
        _currentMoveFrame = 0;

        // 시작 위치 저장 및 잔상 초기화
        Vector2 startPosition = transform.position;
        Ghost.transform.position = startPosition;
        ghostSprite.sprite = playerSprite.sprite;
        ghostSprite.color = new Color(1f, 1f, 1f, 0.4f);

        // 본체(유령 역할)를 붉고 반투명하게 변경
        Color originalColor = playerSprite.color;
        playerSprite.color = new Color(1f, 0.2f, 0.2f, 0.7f);

        // 타임 스케일 설정 (본체 1.2배 가속, 적 0.1배 슬로우)
        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.1f);

        // 이벤트 구독 - 공격 입력이 들어올 때마다 회전값과 현재 위치 프레임 인덱스를 기록
        void OnAttack(Quaternion rot) =>
            _ghostAttackRecords.Add(new GhostAttackData
            {
                moveFrameIndex = _currentMoveFrame,
                rotation = rot
            });
        if(combat == null)
            combat = GetComponent<PlayerCombat>();
        combat.OnAttackInput += OnAttack;
        try
        {   // 기록 루프 
            float recordTimer = 0f;
            float nextRecordTime = 0f;
            float recordInterval = 1f / redRecordFPS;

            while (recordTimer < redRecordDuration && !ct.IsCancellationRequested)
            {
                recordTimer += Time.unscaledDeltaTime;

                if (recordTimer >= nextRecordTime)
                {
                    bool running = Mathf.Abs(Input.GetAxis("Horizontal")) > 0f
                        || Mathf.Abs(rb.linearVelocityX) > 0.01f;
                    if (state.isWallSliding)
                        running = Input.GetAxisRaw("Vertical") != 0f;

                    _ghostMoveRecords.Add(new GhostMoveData
                    {
                        timestamp = recordTimer,
                        position = transform.position,
                        flipX = playerSprite.flipX,
                        spriteScale = playerMove.SpriteTransform.localScale,
                        spriteRotation = playerMove.SpriteTransform.rotation,
                        isRunning = running,
                        isPowerRunning = state.isPowerRun
                    });
                    _currentMoveFrame++; // 위치 프레임 카운트 증가
                    nextRecordTime += recordInterval;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 기록 종료
            Ghost.SetActive(false);
            playerSprite.color = originalColor;
            transform.position = startPosition;
            state.isRedDashRecording = false;

            if (ct.IsCancellationRequested) return;

            // 재생 페이즈
            await PlaybackRoutine(ct);
        }
        finally
        {
            // CTS 취소든 정상 완료든 반드시 구독 해제
            combat.OnAttackInput -= OnAttack;
            state.isRedDashRecording = false;
            playerSprite.color = originalColor;
            Ghost.SetActive(false);
        }
    }
    private async UniTask PlaybackRoutine(CancellationToken ct)
    {
        state.isRedDashPlaying = true;
        state.StartImmunity(0.08f);

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.8f);

        if (combat == null)
            combat = GetComponent<PlayerCombat>();

        float replayTimer = 0f;
        int moveIndex = 0;
        // 공격 기록을 큐로 변환해서 순서대로 소비
        var attackQueue = new Queue<GhostAttackData>(_ghostAttackRecords);

        try
        {
            while (replayTimer < redRecordDuration && !ct.IsCancellationRequested)
            {
                if (replayTimer > 0.08f && state.isHit) break;

                replayTimer += tmg.PlayerFixedDeltaTime;

                // 위치 재생
                if (moveIndex < _ghostMoveRecords.Count - 1)
                {
                    // 다음 프레임 타임스탬프를 넘었으면 인덱스 진행
                    if (replayTimer >= _ghostMoveRecords[moveIndex + 1].timestamp)
                        moveIndex++;

                    var targetData = _ghostMoveRecords[moveIndex];
                    rb.MovePosition(Vector2.Lerp(rb.position, targetData.position, 0.5f));
                    playerSprite.flipX = targetData.flipX;
                    playerMove.SpriteTransform.localScale = targetData.spriteScale;
                    playerMove.SpriteTransform.rotation = targetData.spriteRotation;

                    if (targetData.isRunning)
                    {
                        ani.Run_on();
                        if (targetData.isPowerRunning) ani.Power_Run_on();
                        else ani.Power_Run_off();
                    }
                    else { ani.Run_off(); ani.Power_Run_off(); }
                }

                // 현재 재생 프레임 인덱스에 해당하는 공격이 있으면 즉시 실행
                while (attackQueue.Count > 0 &&
                       attackQueue.Peek().moveFrameIndex <= moveIndex)
                {
                    var atk = attackQueue.Dequeue();
                    combat.TriggerRedDashAttack(atk.rotation);
                }

                await CustomTime.WaitForScaledSeconds(tmg.PlayerFixedDeltaTime, TimeCategory.Player, ct);
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
}