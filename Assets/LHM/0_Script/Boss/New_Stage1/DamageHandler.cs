using System.Threading;
using UnityEngine;

namespace BossSystem
{
    /// <summary>
    /// 보스의 데미지 수신 및 무적 상태를 관리하는 컴포넌트.
    /// 
    /// ── Reference Counter 방식의 무적 시스템 ──────────────────────────────
    /// 단순 bool 방식의 문제점:
    ///   패턴 A가 SetInvincible(true) → 패턴 B가 SetInvincible(true)
    ///   → 패턴 A가 먼저 SetInvincible(false) → 의도치 않게 무적 해제!
    /// 
    /// Reference Counter 방식:
    ///   AcquireInvincibility() 호출마다 카운터 +1
    ///   ReleaseInvincibility() 호출마다 카운터 -1
    ///   카운터 > 0 이면 무적. 0이 되어야 비로소 무적 해제.
    ///   여러 패턴이 독립적으로 무적을 요청해도 논리 오류 없음.
    /// ────────────────────────────────────────────────────────────────────
    /// 
    /// ⚠️ 모든 메서드는 Unity Main Thread에서만 호출할 것.
    ///    UniTask의 기본 스케줄러는 PlayerLoop이므로 정상 사용 시 보장됨.
    /// </summary>
    public class DamageHandler : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 150f;

        // ── 내부 상태 ────────────────────────────────────────────────────
        private float _currentHP;
        private int _invincibilityCount = 0; // Reference Counter

        // 외부 이벤트 — 구독자가 UI, VFX 등을 처리
        public event System.Action<float, float> OnHPChanged;  // (current, max)
        public event System.Action OnDeath;

        // ── 프로퍼티 ─────────────────────────────────────────────────────
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public float HPRatio => _maxHP > 0f ? _currentHP / _maxHP : 0f;

        /// <summary>
        /// 무적 여부. 카운터 > 0이면 데미지를 받지 않는다.
        /// 읽기 전용으로 노출하여 외부에서 직접 변경 불가.
        /// </summary>
        public bool IsInvincible => _invincibilityCount > 0;

        private void Awake()
        {
            _currentHP = _maxHP;
        }

        // ── 무적 제어 API ────────────────────────────────────────────────

        /// <summary>
        /// 무적 스택 +1. 반드시 짝이 되는 ReleaseInvincibility()를 호출해야 함.
        /// 패턴에서 try/finally 또는 IDisposable 래퍼로 사용을 권장.
        /// </summary>
        public void AcquireInvincibility()
        {
            // Interlocked.Increment 대신 단순 증가 사용.
            // UniTask의 PlayerLoop 스케줄러는 단일 스레드이므로 경쟁 조건 없음.
            _invincibilityCount++;
            Debug.Log($"[DamageHandler] Invincibility acquired. Stack: {_invincibilityCount}");
        }

        /// <summary>
        /// 무적 스택 -1. 0 미만으로 내려가지 않도록 보호 처리.
        /// </summary>
        public void ReleaseInvincibility()
        {
            if (_invincibilityCount <= 0)
            {
                Debug.LogWarning("[DamageHandler] ReleaseInvincibility called with count already 0. Check pattern symmetry.");
                return;
            }
            _invincibilityCount--;
            Debug.Log($"[DamageHandler] Invincibility released. Stack: {_invincibilityCount}");
        }

        /// <summary>
        /// 무적 스택을 강제로 0으로 리셋. 페이즈 전환 시 비상 정리용.
        /// 정상 흐름에서는 사용을 피할 것.
        /// </summary>
        public void ForceResetInvincibility()
        {
            if (_invincibilityCount != 0)
                Debug.LogWarning($"[DamageHandler] Force reset invincibility from {_invincibilityCount} to 0.");
            _invincibilityCount = 0;
        }

        // ── 데미지 & 회복 API ────────────────────────────────────────────

        /// <summary>
        /// 데미지 처리. 무적 중이거나 이미 사망 상태이면 무시.
        /// </summary>
        /// <param name="damage">양수 값이어야 함</param>
        public void TakeDamage(float damage)
        {
            if (_currentHP <= 0f || IsInvincible) return;
            if (damage <= 0f) return;

            _currentHP = Mathf.Max(0f, _currentHP - damage);
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0f)
                OnDeath?.Invoke();
        }

        /// <summary>
        /// 체력 회복. 0~maxHP 범위로 클램프.
        /// </summary>
        /// <param name="ratio">회복 비율 (0~1). 절대값이 아닌 비율로 받아 스케일 불변성 유지.</param>
        public void RecoverHP(float ratio)
        {
            float amount = _maxHP * Mathf.Clamp01(ratio);
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
            Debug.Log($"[DamageHandler] HP recovered by {amount:F1}. Current: {_currentHP:F1}/{_maxHP:F1}");
        }

        /// <summary>체력을 maxHP 기준 절대 비율로 직접 설정.</summary>
        public void SetHP(float ratio)
        {
            _currentHP = _maxHP * Mathf.Clamp01(ratio);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// IDisposable 패턴을 모방한 무적 핸들 구조체.
        /// using 블록으로 사용하면 패턴 종료 시 자동으로 Release 보장.
        /// 
        /// 사용 예:
        ///   using var handle = damageHandler.CreateInvincibilityHandle();
        ///   await SomeAsyncWork(ct);
        ///   // handle이 Dispose되면서 자동 Release
        /// </summary>
        public InvincibilityHandle CreateInvincibilityHandle()
        {
            AcquireInvincibility();
            return new InvincibilityHandle(this);
        }

        /// <summary>
        /// 무적 스택을 자동 관리하는 Disposable 핸들.
        /// 구조체로 선언하여 힙 할당을 피함.
        /// </summary>
        public readonly struct InvincibilityHandle : System.IDisposable
        {
            private readonly DamageHandler _handler;

            internal InvincibilityHandle(DamageHandler handler)
            {
                _handler = handler;
            }

            public void Dispose()
            {
                _handler?.ReleaseInvincibility();
            }
        }
    }
}
