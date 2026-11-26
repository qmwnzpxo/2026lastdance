using UnityEngine;

namespace UnMachine.Boss
{
    /// <summary>
    /// 박스 범위 안의 플레이어에게 피해를 1회 준다 (P1 착지·충격파 공용). 할당 없이 미리 만든 버퍼를 쓴다.
    /// 판정 규칙은 레거시 OnStompImpact 와 같다: 레이어 필터 + "Player" 태그 + IDamageable.
    /// </summary>
    public sealed class BossHitBox
    {
        private const string PlayerTag = "Player";
        private const int BufferSize = 8;

        private readonly Collider2D[] _results = new Collider2D[BufferSize];
        private readonly IDamageable[] _damaged = new IDamageable[BufferSize];
        private ContactFilter2D _filter;

        /// <summary>플레이어 레이어로 만든다.</summary>
        public BossHitBox(LayerMask playerLayer)
        {
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(playerLayer);
        }

        /// <summary>범위 안 플레이어에게 피해. 같은 대상(콜라이더 여러 개)은 한 번만.</summary>
        /// <returns>피해를 준 대상 수.</returns>
        public int DamagePlayers(Vector2 center, Vector2 size, float damage)
        {
            int count = Physics2D.OverlapBox(center, size, 0f, _filter, _results);
            int damagedCount = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D target = _results[i];
                _results[i] = null;
                if (target == null || !target.CompareTag(PlayerTag)
                    || !target.TryGetComponent(out IDamageable damageable)
                    || Contains(damageable, damagedCount))
                {
                    continue;
                }

                damageable.TakeDamage(damage);
                _damaged[damagedCount++] = damageable;
            }

            System.Array.Clear(_damaged, 0, damagedCount);
            return damagedCount;
        }

        private bool Contains(IDamageable damageable, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_damaged[i], damageable))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
