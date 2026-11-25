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

    // 되감기용 락다운 스냅샷
    private readonly List<(LockdownRoom room, LockdownRoom.Snapshot snap)> lockdownSnapshots = new();
    [Header("빨강 칩 - 되감기 카메라")]
    [SerializeField] private float rewindCameraDuration = 0.4f;

    // 재생 구간 물리 정지용 백업
    private RigidbodyType2D _savedBodyType;
    private bool _savedFullKinematicContacts;
    private bool _physicsFrozen;
    private readonly List<Collider2D> _overlapResults = new List<Collider2D>(8);

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
        CaptureLockdownSnapshots();

        var tmg = CustomTimeManager.Instance;
        tmg.SetPlayerTimeScale(1.2f);
        tmg.SetEnemyTimeScale(0.1f);

        float recordTimer = 0f;
        float recordInterval = 1f / redRecordFPS;
        float nextRecordTime = 0f;

        if (combat == null)
            combat = GetComponent<PlayerCombat>();

        // ── 공격 입력 기록용 로컬 함수 (클로저로 recordTimer 직접 참조) ──
        void RecordAttack(Quaternion rot)
        {
            eventRecords.Add(new GhostEventData
            {
                timestamp = recordTimer,
                eventType = GhostEventType.Attack,
                rotation = rot
            });
            Debug.Log($"[RedDash] 공격 기록됨: t={recordTimer:F2}");
        }

        combat.OnAttackInput += RecordAttack;
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
            rb.position = startPosition;
            rb.linearVelocity = Vector2.zero;
            state.isRedDashRecording = false;
            playerSprite.color = originalColor;

            if (ct.IsCancellationRequested) return;

            // 녹화 중 발동된 락다운 되돌리기 (재생 중 경로를 따라 재진입하면 다시 발동)
            RestoreLockdownSnapshots();


            // 재생 페이즈
            await PlaybackRoutine(ct);
        }
        finally
        {
            // CTS 취소든 정상 완료든 반드시 구독 해제
            combat.OnAttackInput -= RecordAttack;
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

        // 기록 추적 구간: 물리 정지 (벽·게이트에 막히지 않고 기록 경로 그대로 이동)
        FreezePhysicsForPlayback();

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
                    rb.MovePosition(SampleRecordedPosition(moveIndex, replayTimer));
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
            // 물리 재개 전, 벽 안에 끼어 있으면 기록상 마지막 안전 위치로 되돌림
            ResolveWallOverlap(moveIndex);
            RestorePhysicsAfterPlayback();

            state.isRedDashPlaying = false;
            playerMove.RestoreGravity();
            ani.Run_off();
            ani.Power_Run_off();

            tmg.SetPlayerTimeScale(1f);
            tmg.SetEnemyTimeScale(1f);
        }
    }

    // ──── 재생 구간 물리 정지 / 벽 끼임 복구 ──────────
    #region
    private void FreezePhysicsForPlayback()
    {
        if (_physicsFrozen) return;
        _physicsFrozen = true;

        _savedBodyType = rb.bodyType;
        _savedFullKinematicContacts = rb.useFullKinematicContacts;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        // Kinematic은 기본적으로 Static과 접촉 보고를 안 하므로,
        // 락다운 트리거 등 OnTrigger 이벤트를 유지하기 위해 켠다 (충돌 반응은 없음)
        rb.useFullKinematicContacts = true;
    }

    private void RestorePhysicsAfterPlayback()
    {
        if (!_physicsFrozen) return;
        _physicsFrozen = false;
        if (rb == null) return;

        rb.bodyType = _savedBodyType;
        rb.useFullKinematicContacts = _savedFullKinematicContacts;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    // 기록 프레임 사이를 보간해서 재생 위치를 구한다
    private Vector2 SampleRecordedPosition(int index, float time)
    {
        var a = moveRecords[index];
        if (index + 1 >= moveRecords.Count) return a.position;

        var b = moveRecords[index + 1];
        float t = Mathf.InverseLerp(a.timestamp, b.timestamp, time);
        return Vector2.Lerp(a.position, b.position, t);
    }

    // 현재 위치가 지형(Static 콜라이더)과 겹치면 기록을 거슬러 올라가 안전한 위치로 이동
    private void ResolveWallOverlap(int moveIndex)
    {
        if (rb == null || moveRecords.Count == 0) return;
        if (!IsBlockedAt(rb.position)) return;

        int last = Mathf.Min(moveIndex, moveRecords.Count - 1);
        for (int i = last; i >= 0; i--)
        {
            Vector2 p = moveRecords[i].position;
            if (IsBlockedAt(p)) continue;

            rb.position = p;
            transform.position = p;
            Debug.Log($"[RedDash] 벽 끼임 복구: index={i}, pos={p}");
            return;
        }

        Debug.LogWarning("[RedDash] 벽 끼임 복구 실패: 안전한 기록 위치가 없음");
    }

    private bool IsBlockedAt(Vector2 pos)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

        Vector2 scale = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        const float shrink = 0.9f; // 벽에 붙어 있는 상태(벽 슬라이드)는 끼임으로 보지 않음

        _overlapResults.Clear();
        if (col is CircleCollider2D c)
            Physics2D.OverlapCircle(pos + c.offset * scale,
                c.radius * Mathf.Max(scale.x, scale.y) * shrink, filter, _overlapResults);
        else if (col is BoxCollider2D b)
            Physics2D.OverlapBox(pos + b.offset * scale, b.size * scale * shrink, 0f, filter, _overlapResults);
        else if (col is CapsuleCollider2D cap)
            Physics2D.OverlapCapsule(pos + cap.offset * scale, cap.size * scale * shrink,
                cap.direction, 0f, filter, _overlapResults);
        else
            Physics2D.OverlapPoint(pos, filter, _overlapResults);

        foreach (var hit in _overlapResults)
        {
            if (hit == null) continue;
            var body = hit.attachedRigidbody;
            if (body == rb) continue;                                         // 자기 자신
            if (body != null && body.bodyType != RigidbodyType2D.Static) continue; // 적 등 움직이는 대상 제외
            return true;
        }
        return false;
    }
    #endregion

    // ──── 락다운 스냅샷 ─────────────────────────────
    #region
    private void CaptureLockdownSnapshots()
    {
        lockdownSnapshots.Clear();
        var rooms = FindObjectsByType<LockdownRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var room in rooms)
            lockdownSnapshots.Add((room, room.CaptureSnapshot()));
    }

    private void RestoreLockdownSnapshots()
    {
        foreach (var (room, snap) in lockdownSnapshots)
        {
            if (room == null) continue;
            room.RestoreSnapshot(snap, rewindCameraDuration);
        }
        lockdownSnapshots.Clear();
    }
    #endregion

    // ──── 적 스냅샷 ─────────────────────────────────
    #region
    private void CaptureEnemySnapshots()
    {
        var controllers = EnemyManager.Instance.Enemies;
        for (int i = 0; i < controllers.Count; i++)
        {
            var ctrl = controllers[i];
            if (ctrl == null) continue;
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
        // 풀에서 대여 중인 EnemyBullet 전부 반환
        EnemyBulletPool.Instance?.ReturnAll();
    }

    #endregion
}