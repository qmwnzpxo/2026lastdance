using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════
// HealItemPickup.cs
//
// 회복 아이템. 프리팹별로 _healAmount를 다르게 설정하여
// 회복량 구분(소/중/대)을 프리팹 단위로 처리한다.
// 항상 획득 가능하므로 TryApplyEffect는 항상 true를 반환한다.
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    public class HealItemPickup : ItemPickupBase
    {
        [Header("회복량")]
        [SerializeField] private float _healAmount = 20f;

        protected override bool TryApplyEffect(PlayerState playerState)
        {
            playerState.Heal(_healAmount);
            return true;
        }
    }
}