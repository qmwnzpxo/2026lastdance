using UnityEngine;

namespace Unmachine.UIv5
{
    /// <summary>Figma "HUD v5" 토큰과 1:1.</summary>
    public static class V5Theme
    {
        public static readonly Color Ink      = Hex(0x0D0E11);
        public static readonly Color Graphite = Hex(0x1B1D22);
        public static readonly Color Slate    = Hex(0x2E323B);
        public static readonly Color Steel    = Hex(0x6E737D);
        public static readonly Color Mist     = Hex(0xB9BDC4);
        public static readonly Color Paper    = Hex(0xF4F4F1);
        public static readonly Color Signal   = Hex(0xFFE600);
        public static readonly Color Danger   = Hex(0xFF3B3B);
        public static readonly Color DangerHi = Hex(0xFF7B7B);
        public static readonly Color DangerLo = Hex(0x9C1020);

        public static readonly Color ChipPink  = Hex(0xFF4FA0);
        public static readonly Color ChipRed   = Hex(0xFF3B3B);
        public static readonly Color ChipGreen = Hex(0x3DFFA0);
        public static readonly Color ChipBlue  = Hex(0x3DA8FF);

        public static Color Chip(int chipIndex)
        {
            switch (chipIndex)
            {
                case 1: return ChipRed;
                case 2: return ChipGreen;
                case 3: return ChipBlue;
                default: return ChipPink;
            }
        }

        /// <summary>PlayerChip enum 순서(pink, red, green, blue) → 모드 이름.</summary>
        public static string ChipMode(int chipIndex)
        {
            switch (chipIndex)
            {
                case 1: return "프로모션";
                case 2: return "연섬";
                case 3: return "낙섬";
                default: return "일섬";
            }
        }

        public static Color Hex(int rgb, float a = 1f)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color A(Color c, float a) { c.a = a; return c; }

        // ── 이징 ──
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
