using System.Collections;
using UnityEngine;

namespace BossSystem
{
    public class DamageHandler : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 150f;

        [Header("Barrier")]
        [Tooltip("패턴 파훼 시 딜 타임 지속 시간 (초)")]
        [SerializeField] private float _dealTimeDuration = 5f;

        // ── 내부 상태 ────────────────────────────────────────────────────
        private float _currentHP;
        private int   _invincibilityCount;
        private Coroutine _dealTimeCoroutine;

        // ── 이벤트 ───────────────────────────────────────────────────────
        public event System.Action<float, float> OnHPChanged;
        public event System.Action OnDeath;

        /// <summary>
        /// 방어막이 파훼되어 딜타임이 시작될 때 발생.
        /// UI, VFX 등에서 구독하여 연출 처리.
        /// </summary>
        public event System.Action OnBarrierBroken;

        /// <summary>
        /// 딜타임이 종료되어 무적이 재개될 때 발생.
        /// </summary>
        public event System.Action OnBarrierRestored;

        // ── 프로퍼티 ─────────────────────────────────────────────────────
        public float CurrentHP   => _currentHP;
        public float MaxHP       => _maxHP;
        public float HPRatio     => _maxHP > 0f ? _currentHP / _maxHP : 0f;
        public bool  IsInvincible => _invincibilityCount > 0;

        /// <summary>현재 딜타임 진행 중 여부 (외부 UI/패턴 참조용).</summary>
        public bool IsDealTimeActive { get; private set; }

        private void Awake()
        {
            _currentHP = _maxHP;
        }

        // ── 무적 제어 API ────────────────────────────────────────────────

        /// <summary>무적 스택 +1. 반드시 짝이 되는 ReleaseInvincibility() 호출 필요.</summary>
        public void AcquireInvincibility()
        {
            _invincibilityCount++;
            Debug.Log($"[DamageHandler] Invincibility acquired. Stack={_invincibilityCount}");
        }

        /// <summary>무적 스택 -1. 0 미만으로 내려가지 않도록 보호.</summary>
        public void ReleaseInvincibility()
        {
            if (_invincibilityCount <= 0)
            {
                Debug.LogWarning("[DamageHandler] ReleaseInvincibility underflow.");
                return;
            }
            _invincibilityCount--;
            Debug.Log($"[DamageHandler] Invincibility released. Stack={_invincibilityCount}");
        }

        /// <summary>무적 스택을 강제 0으로 리셋. 페이즈 전환 시 비상 정리용.</summary>
        public void ForceResetInvincibility()
        {
            if (_invincibilityCount != 0)
                Debug.LogWarning($"[DamageHandler] Force reset from {_invincibilityCount} to 0.");
            _invincibilityCount = 0;
            IsDealTimeActive    = false;

            if (_dealTimeCoroutine != null)
            {
                StopCoroutine(_dealTimeCoroutine);
                _dealTimeCoroutine = null;
            }
        }

        // ── 딜타임 API ───────────────────────────────────────────────────

        /// <summary>
        /// 패턴 파훼 시 호출. 무적을 즉시 해제하고 _dealTimeDuration 초 후 재획득.
        ///
        /// 중복 호출 처리:
        ///   이미 딜타임 중이면 기존 코루틴을 취소하고 타이머를 재시작하여
        ///   연속 파훼 시에도 딜타임이 누적되도록 한다.
        /// </summary>
        public void BreakBarrier()
        {
            // 진행 중인 딜타임 코루틴 취소 (중복 파훼 → 타이머 리셋)
            if (_dealTimeCoroutine != null)
                StopCoroutine(_dealTimeCoroutine);

            _invincibilityCount = 0;
            IsDealTimeActive    = true;

            Debug.Log($"[DamageHandler] Barrier broken! Deal time {_dealTimeDuration}s starts.");
            OnBarrierBroken?.Invoke();

            _dealTimeCoroutine = StartCoroutine(DealTimeCoroutine());
        }

        private IEnumerator DealTimeCoroutine()
        {
            yield return new WaitForSeconds(_dealTimeDuration);

            IsDealTimeActive   = false;
            _dealTimeCoroutine = null;

            // 딜타임 종료 → 무적 1스택 재획득 (패턴이 다시 AcquireInvincibility 호출 전 보호)
            _invincibilityCount = 1;

            Debug.Log("[DamageHandler] Deal time ended. Invincibility restored.");
            OnBarrierRestored?.Invoke();
        }

        // ── 데미지 & 회복 API ────────────────────────────────────────────

        /// <summary>데미지 처리. 무적 중이거나 이미 사망 상태이면 무시.</summary>
        public void TakeDamage(float damage)
        {
            if (_currentHP <= 0f || IsInvincible || damage <= 0f) return;

            _currentHP = Mathf.Max(0f, _currentHP - damage);
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0f)
                OnDeath?.Invoke();
        }

        /// <summary>체력 회복. ratio = 0~1 (MaxHP 기준 비율).</summary>
        public void RecoverHP(float ratio)
        {
            float amount = _maxHP * Mathf.Clamp01(ratio);
            _currentHP   = Mathf.Min(_maxHP, _currentHP + amount);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
            Debug.Log($"[DamageHandler] HP recovered {amount:F1}. Current={_currentHP:F1}/{_maxHP}");
        }

        /// <summary>HP를 maxHP 기준 절대 비율로 설정.</summary>
        public void SetHP(float ratio)
        {
            _currentHP = _maxHP * Mathf.Clamp01(ratio);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        // ── IDisposable 무적 핸들 ────────────────────────────────────────

        /// <summary>
        /// using 블록으로 사용하면 패턴 종료 시 자동으로 Release 보장.
        ///
        /// 사용 예:
        ///   using var handle = damageHandler.CreateInvincibilityHandle();
        ///   await SomeAsyncWork(ct);
        /// </summary>
        public InvincibilityHandle CreateInvincibilityHandle()
        {
            AcquireInvincibility();
            return new InvincibilityHandle(this);
        }

        public readonly struct InvincibilityHandle : System.IDisposable
        {
            private readonly DamageHandler _handler;
            internal InvincibilityHandle(DamageHandler h) => _handler = h;
            public void Dispose() => _handler?.ReleaseInvincibility();
        }
    }
}
