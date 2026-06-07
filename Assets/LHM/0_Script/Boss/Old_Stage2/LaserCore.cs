using UnityEngine;
using BossSystem;

namespace Boss.Phase1
{
   
    public class LaserCore : MonoBehaviour, IPoolable, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 1f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private ParticleSystem _destroyVfx;
        [SerializeField] private ParticleSystem _destroyVfxPrefab;


        public event System.Action<LaserCore> OnCoreDestroyed;

        private float _currentHP;

       
        private bool _isDead;

        public Vector3 Position => transform.position;

        /// <summary>풀에서 꺼낼 때 / Blackboard 등록 전에 호출.</summary>
        public void Reset()
        {
            _currentHP = _maxHP;
            _isDead    = false;
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
            OnCoreDestroyed = null;
        }

        private void Awake()
        {
            _currentHP = _maxHP;
        }

        public void TakeDamage(float damage)
        {
            _currentHP -= damage;
            UpdateVisualHP();

            if (_currentHP <= 0f)
                Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true; // 이후 TakeDamage / Die 재진입 차단

             if (_destroyVfxPrefab != null)
            {
                var vfx = Instantiate(_destroyVfxPrefab, transform.position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration);
            }

            OnCoreDestroyed?.Invoke(this);

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
            if (_isDead) return;
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

    public interface IPoolable
    {
        void Reset();
    }
}
