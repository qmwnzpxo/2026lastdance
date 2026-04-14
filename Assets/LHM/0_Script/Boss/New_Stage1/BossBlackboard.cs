using Boss.Phase1;
using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // BossBlackboard  (수정 v2)
    // ── 변경 내역 ─────────────────────────────────────────────────────────
    //   [수정 3] _aliveCores 리스트 순회 중 Remove 충돌 방지
    //     - _pendingRemove 큐 도입: HandleCoreDestroyed는 즉시 Remove 대신
    //       큐에 적재하고, FlushPendingRemovals()에서 일괄 처리.
    //     - FlushPendingRemovals는 LateUpdate 또는 명시적 호출로 실행.
    //     - IReadOnlyList 노출은 유지 (외부 순회는 읽기 전용으로 안전).
    // ═══════════════════════════════════════════════════════════════════════
    public class BossBlackboard : MonoBehaviour
    {
        // ── HP ────────────────────────────────────────────────────────────
        public float MaxHP     { get; private set; }
        public float CurrentHP { get; private set; }
        public float HPRatio   => MaxHP > 0f ? CurrentHP / MaxHP : 0f;

        // ── 무적 Reference Counter ────────────────────────────────────────
        private int _invincibleCount;
        public bool IsInvincible    => _invincibleCount > 0;
        public int  InvincibleStack => _invincibleCount;

        // ── 살아있는 LaserCore 목록 ────────────────────────────────────────
        public IReadOnlyList<LaserCore> AliveCores => _aliveCores;
        private readonly List<LaserCore> _aliveCores = new(4);

        // ── [수정 3] 파괴 예약 큐 ─────────────────────────────────────────
        // HandleCoreDestroyed가 LaserBeamManager.LateUpdate 순회 도중
        // 호출될 경우 _aliveCores 수정 → InvalidOperationException 발생.
        // 해결: 파괴된 코어를 큐에 쌓고, 순회가 끝난 뒤 일괄 제거.
        private readonly Queue<LaserCore> _pendingRemove = new(4);

        // ── 플레이어 정보 ──────────────────────────────────────────────────
        public Transform PlayerTransform { get; private set; }
        public Vector3   PlayerPosition  => PlayerTransform != null
                                            ? PlayerTransform.position : Vector3.zero;
        public Vector3   PlayerVelocity  { get; private set; }

        private Vector3 _prevPlayerPosition;
        private bool    _prevPositionInitialized;

        // ── 이벤트 ────────────────────────────────────────────────────────
        public event System.Action<float, float> OnHPChanged;
        public event System.Action OnDeath;
        public event System.Action OnBarrierBroken;
        public event System.Action OnCoreListChanged;

        // ─────────────────────────────────────────────────────────────────

        public BossBlackboard(float maxHP, Transform playerTransform)
        {
            MaxHP           = maxHP;
            CurrentHP       = maxHP;
            PlayerTransform = playerTransform;
        }

        // ── [수정 3] LateUpdate에서 예약된 제거 일괄 처리 ─────────────────
        // LaserBeamManager.LateUpdate가 _aliveCores를 순회한 뒤이므로
        // 이 시점에 Remove를 수행해도 ConcurrentModification 없음.
        private void LateUpdate()
        {
            FlushPendingRemovals();
        }

        /// <summary>
        /// 파괴 예약 큐를 비워 _aliveCores에서 안전하게 제거.
        /// LaserBeamManager.LateUpdate 이후 호출 보장.
        /// </summary>
        private void FlushPendingRemovals()
        {
            if (_pendingRemove.Count == 0) return;

            while (_pendingRemove.Count > 0)
            {
                var core = _pendingRemove.Dequeue();
                _aliveCores.Remove(core);
                Debug.Log($"[Blackboard] Core flushed. Alive={_aliveCores.Count}");
            }

            // 제거 후 이벤트 일괄 발행 (순회 완전 종료 후)
            OnCoreListChanged?.Invoke();

            if (_aliveCores.Count == 0)
                ReleaseInvincibility();
        }

        // ── 플레이어 속도 갱신 ────────────────────────────────────────────

        public void UpdatePlayerVelocity()
        {
            if (PlayerTransform == null) return;

            Vector3 current = PlayerTransform.position;
            if (!_prevPositionInitialized)
            {
                _prevPlayerPosition      = current;
                _prevPositionInitialized = true;
                PlayerVelocity           = Vector3.zero;
                return;
            }

            float dt        = Mathf.Max(Time.deltaTime, 0.001f);
            PlayerVelocity  = (current - _prevPlayerPosition) / dt;
            _prevPlayerPosition = current;
        }

        // ── 무적 API ──────────────────────────────────────────────────────

        public void AcquireInvincibility()
        {
            _invincibleCount++;
            Debug.Log($"[Blackboard] Invincible acquired. Stack={_invincibleCount}");
        }

        public void ReleaseInvincibility()
        {
            if (_invincibleCount <= 0)
            {
                Debug.LogWarning("[Blackboard] ReleaseInvincibility underflow.");
                return;
            }
            _invincibleCount--;
            Debug.Log($"[Blackboard] Invincible released. Stack={_invincibleCount}");

            if (_invincibleCount == 0)
                OnBarrierBroken?.Invoke();
        }

        public void ForceResetInvincibility() => _invincibleCount = 0;

        // ── 코어 관리 API ─────────────────────────────────────────────────

        public void RegisterCore(LaserCore core)
        {
            if (core == null || _aliveCores.Contains(core)) return;
            _aliveCores.Add(core);
            core.OnCoreDestroyed += HandleCoreDestroyed;
            OnCoreListChanged?.Invoke();
        }

        /// <summary>
        /// [수정 3] 즉시 Remove 대신 큐에 적재.
        /// LaserBeamManager.LateUpdate가 _aliveCores를 for 루프로 순회하는
        /// 동일 프레임에 Remove하면 인덱스 오류 또는 스킵이 발생한다.
        /// 큐 방식은 현재 순회가 끝난 뒤 LateUpdate 말미에 일괄 처리된다.
        /// </summary>
        private void HandleCoreDestroyed(LaserCore core)
        {
            _pendingRemove.Enqueue(core);
            Debug.Log($"[Blackboard] Core queued for removal. Queue={_pendingRemove.Count}");
            // 실제 Remove + 이벤트는 FlushPendingRemovals()에서 처리
        }

        // ── 데미지 / 회복 API ─────────────────────────────────────────────

        public void TakeDamage(float damage)
        {
            if (IsInvincible || CurrentHP <= 0f || damage <= 0f) return;

            CurrentHP = Mathf.Max(0f, CurrentHP - damage);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0f)
                OnDeath?.Invoke();
        }

        public void RecoverHP(float amount)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void RecoverHPByRatio(float ratio) =>
            RecoverHP(MaxHP * Mathf.Clamp01(ratio));
    }
}
