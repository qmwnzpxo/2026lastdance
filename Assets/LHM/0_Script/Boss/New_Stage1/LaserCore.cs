using UnityEngine;
using BossSystem;

namespace Boss.Phase1
{
    /// <summary>
    /// 360도 레이저 방어막을 구성하는 코어 오브젝트.
    ///
    /// [수정 v2]
    ///   1. _isDead 플래그로 이중 파괴/이벤트 방지 (Destroy 프레임 지연 대응)
    ///   2. IPoolable 인터페이스 구현 → ObjectPool에서 재사용 가능
    ///   3. Reset() 으로 풀 반환 후 상태 초기화
    /// </summary>
    public class LaserCore : MonoBehaviour, IPoolable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 30f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private ParticleSystem _destroyVfx;
        [SerializeField] private ParticleSystem _destroyVfxPrefab;


        public event System.Action<LaserCore> OnCoreDestroyed;

        private float _currentHP;

        // ── [수정 1] ──────────────────────────────────────────────────────
        // _isDead 플래그: Destroy()는 해당 프레임 말에 처리되므로
        // 그 사이에 TakeDamage / Die가 중복 호출될 수 있다.
        // bool 플래그로 입구를 막아 이벤트가 두 번 발생하는 버그 차단.
        private bool _isDead;

        public Vector3 Position => transform.position;

        /// <summary>풀에서 꺼낼 때 / Blackboard 등록 전에 호출.</summary>
        public void Reset()
        {
            _currentHP = _maxHP;
            _isDead    = false;
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;

            // 모든 구독자 제거: 이전 사용 시 등록된 핸들러 누수 방지
            OnCoreDestroyed = null;
        }

        private void Awake()
        {
            _currentHP = _maxHP;
        }

        public void TakeDamage(float damage)
        {
            // ── [수정 1] _isDead 이중 체크 ───────────────────────────────
            if (_isDead || damage <= 0f) return;

            _currentHP -= damage;
            UpdateVisualHP();

            if (_currentHP <= 0f)
                Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true; // 이후 TakeDamage / Die 재진입 차단

            // ── [수정] 자식 부모를 끊는 대신, 프리팹을 독립적으로 스폰 ──
             if (_destroyVfxPrefab != null)
            {
                // 1순위: 파티클도 ObjectPool로 관리한다면
                /*
                var vfxGo = ObjectPool.Get(_destroyVfxPrefab.gameObject, transform.position, Quaternion.identity);
                vfxGo.GetComponent<ParticleSystem>().Play();
                // (주의: 파티클 재생이 끝나면 풀로 반환하는 로직이 파티클 쪽에 있어야 합니다)
                */

                // 2순위: 파티클 풀링이 아직 없다면 Instantiate 후 지연 파괴
                var vfx = Instantiate(_destroyVfxPrefab, transform.position, Quaternion.identity);
                vfx.Play();
                // 파티클의 재생 시간만큼 기다렸다가 파괴 (메모리 누수 방지)
                Destroy(vfx.gameObject, vfx.main.duration);
            }

            OnCoreDestroyed?.Invoke(this);

            // 이제 본체는 아무런 미련 없이 풀로 깔끔하게 돌아갑니다.
            if (ObjectPool.HasPool(gameObject))
                ObjectPool.Return(gameObject);
            else
                Destroy(gameObject);
        }

        private void UpdateVisualHP()
        {
            if (_spriteRenderer == null) return;
            float ratio = _currentHP / _maxHP;
            _spriteRenderer.color = Color.Lerp(Color.red, Color.white, ratio);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDead) return; // ← [수정 1] 파괴 직후 트리거 이벤트 방어
            if (!string.Equals(other.tag, "PlayerProjectile",
                    System.StringComparison.Ordinal)) return;

            var proj = other.GetComponent<IProjectileDamage>();
            TakeDamage(proj?.Damage ?? 10f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"HP: {_currentHP:F0}/{_maxHP:F0}");
        }
#endif
    }

    public interface IProjectileDamage { float Damage { get; } }

    /// <summary>
    /// 풀링 대상 오브젝트가 구현해야 하는 인터페이스.
    /// ObjectPool이 Get/Return 시 호출한다.
    /// </summary>
    public interface IPoolable
    {
        void Reset();
    }
}
