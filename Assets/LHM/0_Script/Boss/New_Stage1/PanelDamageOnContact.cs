using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // PanelDamageOnContact.cs
    //
    // 패널 B 프리팹에 붙이는 컴포넌트.
    // 플레이어와 콜라이더가 닿는 순간 데미지를 줌.
    //
    // 사용법:
    //   1. 패널 B 프리팹에 이 컴포넌트 추가
    //   2. _damage 값 설정
    //   3. 패널에 Collider2D (IsTrigger = true) 있는지 확인
    //   4. 플레이어 오브젝트 태그가 "Player"인지 확인
    // ═══════════════════════════════════════════════════════════════════════
    [RequireComponent(typeof(Collider2D))]
    public class PanelDamageOnContact : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("플레이어에게 닿을 때 줄 데미지")]
        [SerializeField] private float _damage = 20f;

        [Tooltip("true = OnTriggerEnter2D / false = OnCollisionEnter2D")]
        [SerializeField] private bool _useTrigger = true;

        // ── Trigger 방식 ──────────────────────────────────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_useTrigger) return;
            TryDamage(other.gameObject);
        }

        // ── Collision 방식 ────────────────────────────────────────────────
        private void OnCollisionEnter2D(Collision2D col)
        {
            if (_useTrigger) return;
            TryDamage(col.gameObject);
        }

        // ─────────────────────────────────────────────────────────────────

        private void TryDamage(GameObject target)
        {
            if (!target.CompareTag("Player")) return;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(_damage);
                Debug.Log($"[PanelDamageOnContact] {gameObject.name} → 플레이어 {_damage} 데미지");
            }
            else
            {
                Debug.LogWarning("[PanelDamageOnContact] 플레이어에 IDamageable 컴포넌트가 없습니다.");
            }
        }
    }
}
