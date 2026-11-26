using System;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 선택 단위 → 예고 신호 (색 + 깜빡임 횟수). 콤보는 첫 패턴의 신호. spec: boss.md 9장, 8장 콤보 표
    /// </summary>
    public readonly struct TelegraphSignal
    {
        private const int RedBlinkCount = 2;

        private TelegraphSignal(TelegraphColor color, int blinkCount)
        {
            Color = color;
            BlinkCount = blinkCount;
        }

        /// <summary>예고 색.</summary>
        public TelegraphColor Color { get; }

        /// <summary>깜빡임 횟수. 0 = 예고 내내 점등.</summary>
        public int BlinkCount { get; }

        /// <summary>단위의 예고 신호.</summary>
        public static TelegraphSignal For(SelectionUnit unit)
        {
            switch (unit)
            {
                case SelectionUnit.P2:
                case SelectionUnit.P3:
                case SelectionUnit.C1: // P2 → Brake → P3
                    return new TelegraphSignal(TelegraphColor.Red, RedBlinkCount);
                case SelectionUnit.P4:
                case SelectionUnit.P5:
                case SelectionUnit.P6:
                case SelectionUnit.C4: // P4 → P2
                    return new TelegraphSignal(TelegraphColor.Yellow, 0);
                case SelectionUnit.P1:
                case SelectionUnit.C2: // P1 → P5
                case SelectionUnit.C3: // P1 ×3
                    return new TelegraphSignal(TelegraphColor.White, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(unit), unit, "예고 신호 매핑 없음");
            }
        }

        /// <summary>
        /// 예고 시작 후 elapsed 초에 켜져 있는지. 깜빡임은 예고 길이를 (횟수 × 2) 칸으로 나눠 짝수 칸에 켠다.
        /// </summary>
        /// <param name="elapsed">예고 경과 시간(초).</param>
        /// <param name="duration">예고 전체 길이(초). 0 이하면 항상 켜짐.</param>
        public bool IsLit(float elapsed, float duration)
        {
            if (BlinkCount <= 0 || duration <= 0f)
            {
                return true;
            }

            int slots = BlinkCount * 2;
            float ratio = elapsed / duration;
            int slot = (int)(ratio * slots);
            if (slot < 0)
            {
                slot = 0;
            }

            return slot >= slots || slot % 2 == 0; // 끝(부동소수 오차)에서는 켜 둔다
        }
    }
}
