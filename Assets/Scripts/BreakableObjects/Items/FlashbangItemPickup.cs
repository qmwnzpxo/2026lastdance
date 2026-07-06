using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════
// FlashbangItemPickup.cs
//
// 섬광탄 아이템. PlayerState.TryAddFlashbang()을 호출하여
// 최대 보유 개수 미만이면 획득, 최대치면 획득하지 않고 필드에 남긴다.
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    public class FlashbangItemPickup : ItemPickupBase
    {
        protected override bool TryApplyEffect(PlayerState playerState)
        {
            return playerState.TryAddFlashbang();
        }
    }
}