using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════
// BreakableObject.cs
//
// 범용 파괴 가능 오브젝트 베이스 클래스.
// 오브젝트 풀링 사용을 전제로 하며, HP가 0 이하가 되어도
// Destroy / ObjectPool.Return을 직접 호출하지 않는다.
// 실제 파괴 처리(이펙트, 풀 반환 등)는 OnBroken()을 오버라이드하여 구현한다.
//
// 사용법:
//   1. 콜라이더가 있는 오브젝트에 부착 (Collider2D 필수)
//   2. _maxHP를 인스펙터에서 설정
//   3. 파괴 시 동작이 필요하면 하위 클래스에서 OnBroken() 오버라이드
//      - ObjectPool.Return(gameObject) 등 실제 처리는 오버라이드한 곳에서 수행
//   4. 풀에서 다시 꺼낼 때는 ResetState()를 호출하여 HP/파괴 플래그 초기화
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    [RequireComponent(typeof(Collider2D))]
    public class BreakableObject : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] protected float _maxHP = 3f;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        protected float _currentHP;
        protected bool _isBroken;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsBroken => _isBroken;

        // ─── Unity Lifecycle ───────────────────────────────────────────
        protected virtual void Awake()
        {
            ResetState();
        }

        // ─────────────────────────────────────────────────────────────────

        public virtual void TakeDamage(float damage)
        {
            if (_isBroken || damage <= 0f) return;

            _currentHP -= 1f;

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                _isBroken = true;
                OnBroken();
            }
        }

        // 파괴 처리 (오버라이드 지점)

        // HP가 0 이하가 되는 순간 단 한 번 호출된다.
        protected virtual void OnBroken() { }

        // 풀 재사용 대비 초기화

        // 오브젝트 풀에서 다시 꺼낼 때 호출하여 HP와 파괴 상태를 초기화한다.
        public virtual void ResetState()
        {
            _currentHP = _maxHP;
            _isBroken = false;
        }
    }
}