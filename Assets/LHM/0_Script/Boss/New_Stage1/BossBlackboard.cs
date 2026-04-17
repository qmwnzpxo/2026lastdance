using Boss.Phase1;
using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // BossBlackboard  (수정 v3)
    // ── 변경 내역 ─────────────────────────────────────────────────────────
    //   [버그 수정] MonoBehaviour → 일반 class 로 변경
    //     - BossController에서 new BossBlackboard(...) 로 생성하고 있었는데
    //       MonoBehaviour를 new로 생성하면 Unity가 즉시 크래시.
    //     - MonoBehaviour는 AddComponent로만 생성 가능.
    //     - LateUpdate(FlushPendingRemovals)는 BossController.LateUpdate에서
    //       직접 호출하도록 변경.
    // ═══════════════════════════════════════════════════════════════════════
    public class BossBlackboard  // MonoBehaviour 제거!
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

        // ── 파괴 예약 큐 ───────────────────────────────────────────────────
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

        // ── [수정] BossController.LateUpdate()에서 호출 ───────────────────
        // MonoBehaviour가 아니므로 Unity 라이프사이클 메서드 없음.
        // BossController가 자신의 LateUpdate에서 이 메서드를 직접 호출한다.
        public void Tick()
        {
            FlushPendingRemovals();
        }

        private void FlushPendingRemovals()
        {
            if (_pendingRemove.Count == 0) return;

            while (_pendingRemove.Count > 0)
            {
                var core = _pendingRemove.Dequeue();

                // 👈 [추가] 리스트에서 제거하기 전에 반드시 이벤트를 구독 해제해야 합니다!
                if (core != null)
                {
                    core.OnCoreDestroyed -= HandleCoreDestroyed;
                }

                _aliveCores.Remove(core);
                Debug.Log($"[Blackboard] Core flushed. Alive={_aliveCores.Count}");
            }

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

        private void HandleCoreDestroyed(LaserCore core)
        {
            _pendingRemove.Enqueue(core);
            Debug.Log($"[Blackboard] Core queued for removal. Queue={_pendingRemove.Count}");
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
