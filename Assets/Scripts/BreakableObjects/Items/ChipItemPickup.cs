using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════
// ChipItemPickup.cs
//
// 칩 아이템. 색상(PlayerChip)에 따라 프리팹이 다르므로
// ItemDropOnBreak에서 Instantiate/Get 직후 Init()으로 색상을 주입한다.
// Init() 없이 트리거된 경우(비정상 상태)에는 획득되지 않는다.
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    public class ChipItemPickup : ItemPickupBase
    {
        public PlayerChip _chip;
        public bool _initialized;

        /// <summary>드랍 직후 호출. 어떤 칩 색상인지 주입.</summary>
        public void Init(PlayerChip chip)
        {
            _chip = chip;
            _initialized = true;
        }

        protected override bool TryApplyEffect(PlayerState playerState)
        {
            if (!_initialized) return false;

            playerState.GetChips(_chip);
            _initialized = false;
            return true;
        }
    }
}