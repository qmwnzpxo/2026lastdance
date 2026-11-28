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

        // 타임 스케일 설정 (기록: 본체 RedRecordPlayerScale, 적 RedRecordEnemyScale)
        CaptureEnemySnapshots();
        CaptureLockdownSnapshots();

        var tmg = CustomTimeManager.Instance;
        ReleaseRedTimeScale(); // 이전 레드대시 요청이 남아 있으면 정리
        var hPlayer = _redTimePlayer = tmg.Request(TimeCategory.Player, RedRecordPlayerScale, tag: "RedDash");
        var hEnemy = _redTimeEnemy = tmg.Request(TimeCategory.Enemy, RedRecordEnemyScale, tag: "RedDash");

        // recordTimer: 플레이어 시간(배율 적용) 기준 타임스탬프 → 속도 1 기준 행동 시간
        // recordElapsed: 실시간 기준 기록 길이 제한
        float recordTimer = 0f;
        float recordElapsed = 0f;
        float recordInterval = 1f / redRecordFPS;
        float nextRecordTime = 0f;

        if (combat == null)
            combat = GetComponent<PlayerCombat>();

        // ── 공격 입력 기록용 로컬 함수 (클로저로 recordTimer 직접 참조) ──
        void RecordAttack(Quaternion rot, bool groundAttack)
        {
            eventRecords.Add(new GhostEventData
            {
                timestamp = recordTimer,
                eventType = GhostEventType.Attack,
                rotation = rot,
                groundAttack = groundAttack,
            });
            Debug.Log($"[RedDash] 공격 기록됨: t={recordTimer:F2}");
        }

        combat.OnAttackInput += RecordAttack;
        try
        {
            while (recordElapsed < redRecordDuration && !ct.IsCancellationRequested)
            {
                recordElapsed += Time.fixedUnscaledDeltaTime;
                recordTimer += tmg.PlayerFixedDeltaTime;

                // 이동 프레임 기록
                if (recordTimer >= nextRecordTime)
                {
                    bool running = Mathf.Abs(Input.GetAxis("Horizontal")) > 0f
                        || Mathf.Abs(rb.linearVelocityX) > 0.01f;
                    // 벽에서는 W/S뿐 아니라 A/D로도 오르내리므로 둘 다 이동으로 기록
                    // (PlayerMove.Ani_Run과 같은 기준. 이전엔 A/D 이동이 '정지'로 기록돼 재생 때 선 자세로 고정)
                    if (state.isWallSliding)
                        running = Input.GetAxisRaw("Vertical") != 0f
                               || Input.GetAxisRaw("Horizontal") != 0f;

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
            await PlaybackRoutine(ct, hPlayer, hEnemy, recordTimer);
        }
        finally
        {
            // 녹화 중 취소돼도 배율이 남지 않게 해제 (재생 끝에서 이미 해제됐으면 무시됨)
            // 지역 복사본이라 그 사이 시작된 새 레드대시 요청은 건드리지 않음
            tmg.Release(ref hPlayer);
            tmg.Release(ref hEnemy);

            // CTS 취소든 정상 완료든 반드시 구독 해제
            combat.OnAttackInput -= RecordAttack;
            state.isRedDashRecording = false;
            playerSprite.color = originalColor;
            Ghost.SetActive(false);
        }
    }

    // 레드대시 배율 요청 (CtsFree에서 즉시 해제하기 위해 보관)
    private CustomTimeManager.Handle _redTimePlayer;
    private CustomTimeManager.Handle _redTimeEnemy;

    private void ReleaseRedTimeScale()
    {
        var tmg = CustomTimeManager.Instance;
        if (tmg == null) return;
        tmg.Release(ref _redTimePlayer);
        tmg.Release(ref _redTimeEnemy);
    }

    private async UniTask PlaybackRoutine(CancellationToken ct,
        CustomTimeManager.Handle hPlayer, CustomTimeManager.Handle hEnemy, float recordLength)
    {
        state.isRedDashPlaying = true;
        state.StartImmunity(0.08f);

        // 재생 중엔 PlayerMove 입력 처리가 멈추므로, 기록 끝에 남아 있던 벽·등반 상태를 정리
        // (등반 상태가 남으면 재생이 끝난 뒤 등반 이동이 이어져 위로 솟구침)
        playerMove.ResetWallState();
        playerMove.CancelClimb();

        ReturnAllEnemyBullets();
        RestoreEnemySnapshots();

        // 기록 추적 구간: 물리 정지 (벽·게이트에 막히지 않고 기록 경로 그대로 이동)
        FreezePhysicsForPlayback();

        var tmg = CustomTimeManager.Instance;
        // 재생: 본체 RedPlaybackPlayerScale, 적 RedPlaybackEnemyScale
        tmg.SetScale(hPlayer, RedPlaybackPlayerScale);
        tmg.SetScale(hEnemy, RedPlaybackEnemyScale);

        // 공격 중엔 재생을 늦춤. 적도 같은 비율로 늦춰서 플레이어:적 비율 유지
        float slowRatio = RedPlaybackPlayerScale > 0f ? redPlaybackAttackScale / RedPlaybackPlayerScale : 1f;
        bool slowed = false;

        if (combat == null)
            combat = GetComponent<PlayerCombat>();

        float replayTimer = 0f;
        float replayElapsed = 0f; // 실시간 경과 (시작 무적 0.08초와 같은 기준)
        int moveIndex = 0;
        int eventIndex = 0;

        // 공격 기록을 큐로 변환해서 순서대로 소비
        var attackQueue = new Queue<GhostEventData>(eventRecords);

        try
        {
            while (replayTimer < recordLength && !ct.IsCancellationRequested)
            {
                // 시작 무적(StartImmunity 0.08초)도 isHit을 켜므로, 실시간 기준으로 그 구간은 피격 판정에서 제외
                // (replayTimer는 재생 배율로 빨리 흘러 첫 프레임에 0.08을 넘어버려 재생이 바로 끊겼음)
                if (replayElapsed > 0.08f && state.isHit) break;
                // 사망은 무적(isHit)을 걸지 않으므로 따로 확인. 안 끊으면 리스폰 위치를 재생이 다시 경로로 끌어옴
                if (state.isDead) break;

                // 공격 시작/종료 시점에만 배율 전환
                if (state.isAttacking != slowed)
                {
                    slowed = state.isAttacking;
                    float r = slowed ? slowRatio : 1f;
                    tmg.SetScale(hPlayer, RedPlaybackPlayerScale * r);
                    tmg.SetScale(hEnemy, RedPlaybackEnemyScale * r);
                }

                replayElapsed += Time.fixedUnscaledDeltaTime;
                // 기록과 같은 플레이어 시간 기준 → 속도 1 기준 행동을 RedPlaybackPlayerScale배 빠르게 재생
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

                    // 공격 중엔 Run을 꺼야 공격 모션으로 전이됨 (PlayerMove.Ani_Run과 같은 규칙)
                    if (m.isRunning && !state.isAttacking)
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
                                combat.TriggerRedDashAttack(ev.rotation, ev.groundAttack);
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

            tmg.Release(ref hPlayer);
            tmg.Release(ref hEnemy);
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

            var enemyRb = ctrl.GetComponent<Rigidbody2D>();
            var moveable = bb.moveable;
            enemySnapshots.Add(new EnemySnapshotData
            {
                controller = ctrl,
                state = ctrl.GetComponent<EnemyState>(),
                position = ctrl.transform.position,
                hasRigidbody = enemyRb != null,
                velocity = enemyRb != null ? enemyRb.linearVelocity : Vector2.zero,
                hp = ctrl.GetComponent<EnemyState>()?.currentHp ?? 0f,
                isDetected = bb.isDetected,
                detectGauge = bb.detectGauge,
                chaseEndTimer = bb.chaseEndTimer,
                isStunned = bb.isStunned,
                stunDuration = bb.stunDuration,
                stunElapsed = bb.stunElapsed,
                attackCooldownRemaining = bb.attackCooldownRemaining,
                hasMoveable = moveable != null,
                patrolDirection = moveable != null ? moveable.patrolDirection : 1,
                needsRetreat = moveable != null && moveable.needsRetreat,
            });
        }
    }
    private void RestoreEnemySnapshots()
    {
        foreach (var snap in enemySnapshots)
        {
            if (snap.controller == null || snap.state == null) continue;

            // 위치·속도 복원 (리지드바디 위치도 같이 맞춰야 다음 물리 스텝에서 되돌아가지 않음)
            snap.controller.transform.position = snap.position;
            if (snap.hasRigidbody && snap.controller.TryGetComponent(out Rigidbody2D enemyRb))
            {
                enemyRb.position = snap.position;
                enemyRb.linearVelocity = snap.velocity;
            }

            // BT 재초기화 후 블랙보드 값 덮어쓰기
            snap.controller.requireInit = true;
            snap.controller.ReInit();

            // HP 복원 — 반드시 ReInit 뒤에 (ReInit 안의 EnemyState.Init()이 currentHp를 maxHp로 덮어씀)
            snap.state.currentHp = snap.hp;

            var bb = snap.controller._BB;
            if (bb == null) continue;

            bb.isDetected = snap.isDetected;
            bb.detectGauge = snap.detectGauge;
            bb.chaseEndTimer = snap.chaseEndTimer;
            bb.isStunned = snap.isStunned;
            // 스턴 중이었다면 처음부터가 아니라 남은 시간만큼 이어서 (ActionStun이 stunElapsed부터 진행)
            bb.stunDuration = snap.stunDuration;
            bb.stunElapsed = snap.stunElapsed;
            bb.attackCooldownRemaining = snap.attackCooldownRemaining;

            // 순찰 방향·후퇴 필요 여부는 블랙보드가 아니라 컨트롤러(IMoveable)에 저장됨
            if (snap.hasMoveable && bb.moveable != null)
            {
                bb.moveable.patrolDirection = snap.patrolDirection;
                bb.moveable.needsRetreat = snap.needsRetreat;
            }
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