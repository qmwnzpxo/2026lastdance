using UnityEngine;
using BossSystem;

// ═══════════════════════════════════════════════════════════════════════
// ItemPickupBase.cs
//
// 모든 필드 아이템 픽업의 공통 베이스.
// 태그 판정과 풀 반환은 여기서 공통 처리하고,
// 실제 효과 적용 및 성공 여부 판단은 하위 클래스가 TryApplyEffect()로 구현한다.
//
// TryApplyEffect가 false를 반환하면(예: 섬광탄 최대 보유) 아이템은
// 땅에 그대로 남아있고 풀로 반환되지 않는다.
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class ItemPickupBase : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            var playerState = other.GetComponent<PlayerState>();
            if (playerState == null) return;

            bool consumed = TryApplyEffect(playerState);
            if (!consumed) return;

            ObjectPool.Return(gameObject);
        }

        /// <summary>
        /// 실제 효과를 적용하고, 아이템이 소비되었는지 여부를 반환한다.
        /// false를 반환하면 아이템은 그대로 필드에 남는다.
        /// </summary>
        protected abstract bool TryApplyEffect(PlayerState playerState);
    }
}