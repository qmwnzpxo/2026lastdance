using UnityEngine;
using Boss.Phase1; // IProjectileDamage

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // PanelHealth.cs — 판넬 / 드론 오브젝트 HP 컴포넌트
    //
    // 사용처:
    //   · Phase1PanelPattern 의 판넬 프리팹
    //   · Phase2RapidLaserPattern 의 드론 프리팹
    //   · SniperPanelSystem 의 패널 프리팹
    //
    // 파훼 흐름:
    //   PlayerProjectile 충돌
    //       → TakeDamage()
    //       → HP ≤ 0 → Die()
    //       → OnPanelDestroyed(this) 이벤트
    //       → 패턴이 카운트 감소 → 전부 0 → 파훼 판정
    // ═══════════════════════════════════════════════════════════════════════
    [RequireComponent(typeof(Collider2D))]
    public class PanelHealth : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHP = 30f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private ParticleSystem _destroyVfxPrefab;

        // ── 이벤트 ────────────────────────────────────────────────────────
        /// <summary>HP가 0 이하가 되어 오브젝트가 파괴될 때 발생.</summary>
        public event System.Action<PanelHealth> OnPanelDestroyed;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        private float _currentHP;
        private bool  _isDead;

        // ── 프로퍼티 ─────────────────────────────────────────────────────
        public float HPRatio => _maxHP > 0f ? _currentHP / _maxHP : 0f;

        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake() => ResetHP();

        /// <summary>
        /// 오브젝트 풀에서 재사용할 때 호출.
        /// 패턴에서 Instantiate 직후 또는 Pool.Get 직후 수동 호출도 가능.
        /// </summary>
        public void ResetHP()
        {
            _currentHP      = _maxHP;
            _isDead         = false;
            OnPanelDestroyed = null;

            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
        }

        // ─────────────────────────────────────────────────────────────────
        // 데미지 API
        // ─────────────────────────────────────────────────────────────────

        public void TakeDamage(float damage)
        {
            if (_isDead || damage <= 0f) return;

            _currentHP -= damage;
            UpdateVisual();

            if (_currentHP <= 0f)
                Die();
        }

        // ─────────────────────────────────────────────────────────────────
        // 내부 로직
        // ─────────────────────────────────────────────────────────────────

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            if (_destroyVfxPrefab != null)
            {
                var vfx = Instantiate(_destroyVfxPrefab,
                              transform.position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration);
            }

            // 구독자(패턴 카운터 콜백)에게 파괴 알림
            OnPanelDestroyed?.Invoke(this);

            gameObject.SetActive(false); // 풀 반환 대신 비활성화 (패턴이 Destroy 처리)
        }

        private void UpdateVisual()
        {
            if (_spriteRenderer == null) return;
            _spriteRenderer.color = Color.Lerp(Color.red, Color.white, HPRatio);
        }

        // ─────────────────────────────────────────────────────────────────
        // 충돌 처리
        // ─────────────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDead) return;
            if (!string.Equals(other.tag, "PlayerProjectile",
                    System.StringComparison.Ordinal)) return;

            var proj = other.GetComponent<IProjectileDamage>();
            TakeDamage(proj?.Damage ?? 10f);
        }

        // ─────────────────────────────────────────────────────────────────
        // 에디터 시각화
        // ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.7f,
                $"HP: {_currentHP:F0}/{_maxHP:F0}");
        }
#endif
    }
}
