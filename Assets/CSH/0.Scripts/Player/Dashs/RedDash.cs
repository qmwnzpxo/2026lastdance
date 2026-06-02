using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;



/// <summary>
/// Red Dash 시스템 구현
/// 너무 복잡하고 길어서 함수만 따로 분리함
/// </summary>
public partial class PlayerDash : MonoBehaviour
{
    SpriteRenderer GhostSprite;
    SpriteRenderer playerSprite;
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
        // 기록 페이즈 - 본체 유령화 
        state.isRedDashRecording = true;
        _ghostMoveRecords.Clear();
        _ghostAttackRecords.Clear();

        // 시작 위치 저장 및 잔상 초기화
        Vector2 startPosition = transform.position;
        Ghost.transform.position = startPosition;

        GhostSprite.sprite = playerSprite.sprite;
        GhostSprite.color = new Color(1f, 1f, 1f, 0.4f); // 본체 색상의 반투명 잔상

        // 본체(유령 역할)를 붉고 반투명하게 변경
        Color originalColor = playerSprite.color;
        playerSprite.color = new Color(1f, 0.2f, 0.2f, 0.7f);

        // 타임 스케일 설정 (본체 1.2배 가속, 적 0.1배 초슬로우)
        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.1f);

        float recordTimer = 0f;
        float recordInterval = 1f / redRecordFPS;
        float nextRecordTime = 0f;

        while (recordTimer < redRecordDuration && !ct.IsCancellationRequested)
        {
            recordTimer += Time.unscaledDeltaTime;

            // 공격 기록 (데미지는 안들어가고 애니메이션과 방향만 기록)
            if (Input.GetMouseButtonDown(0))
            {
                _ghostAttackRecords.Add(new GhostAttackData
                {
                    timestamp = recordTimer,
                    rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(GetMouseDirection().y, GetMouseDirection().x) * Mathf.Rad2Deg)
                });
            }

            // 위치 기록 (지정된 프레임 간격마다)
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
                nextRecordTime += recordInterval;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 기록 종료: 고스트 파괴, 색상 복원, 시작 지점으로 강제 이동
        if (Ghost != null) Ghost.SetActive(false);
        playerSprite.color = originalColor;
        transform.position = startPosition;
        state.isRedDashRecording = false;

        if (ct.IsCancellationRequested) return;

        state.isRedDashPlaying = true;

        // 재생 시작 직후 0.08초 동안 무적
        state.StartImmunity(0.08f);

        // 재생 중에는 플레이어 물리엔진 간섭 차단
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.8f);

        float replayTimer = 0f;
        int moveIndex = 0;
        int attackIndex = 0;
        PlayerCombat combat = GetComponent<PlayerCombat>();

        while (replayTimer < redRecordDuration && !ct.IsCancellationRequested)
        {
            // 0.08초 이후 피격 시 강제 종료
            if (replayTimer > 0.08f && state.isHit)
            {
                Debug.Log("[PlayerDash] 프로-모션 중 피격됨");
                break;
            }

            replayTimer += Time.unscaledDeltaTime * 1.2f;

            // 위치 재생
            if (moveIndex < _ghostMoveRecords.Count - 1)
            {
                if (replayTimer >= _ghostMoveRecords[moveIndex + 1].timestamp)
                    moveIndex++;

                GhostMoveData targetData = _ghostMoveRecords[moveIndex];
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
                else
                {
                    ani.Run_off();
                    ani.Power_Run_off();
                }
            }

            // 공격 재생
            if (attackIndex < _ghostAttackRecords.Count)
            {
                if (replayTimer >= _ghostAttackRecords[attackIndex].timestamp)
                {
                    combat.TriggerRedDashAttack(_ghostAttackRecords[attackIndex].rotation);
                    attackIndex++;
                }
            }

            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
        }

        // 모든 과정 종료 후 상태 복구
        state.isRedDashPlaying = false;
        playerMove.RestoreGravity();
        ani.Run_off();
        ani.Power_Run_off();

        tmg.SetPlayerTimeScale(1f);
        tmg.SetEnemyTimeScale(1f);
    }
}