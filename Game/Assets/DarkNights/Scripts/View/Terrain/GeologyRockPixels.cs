using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>塌方方粒的原生16像素画法与墙壁同系调色板；供首建纹理及落地裁切共用，避免坠落颗粒与清障外观换形换色。不读取场景或业务状态。</summary>
    public static class GeologyRockPixels
    {
        private static readonly Color32[] body = { new Color32(88, 64, 39, 255), new Color32(78, 56, 34, 255),
            new Color32(98, 72, 45, 255), new Color32(71, 51, 31, 255) };
        private static readonly Color32[] light = { new Color32(128, 96, 59, 255), new Color32(114, 83, 48, 255),
            new Color32(136, 103, 65, 255), new Color32(107, 78, 46, 255) };
        private static readonly Color32[] dark = { new Color32(54, 39, 25, 255), new Color32(48, 34, 22, 255),
            new Color32(59, 43, 26, 255), new Color32(42, 32, 21, 255) };

        public static Color32 Sample(int variant, int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16) return default;
            variant %= 4;
            if (variant == 3 && (x + y < 3 || x > 12 && y > 12)) return default;
            if (y == 15 || x == 0 && y > 11) return light[variant];
            if (y < 3 || x == 15 || x == 2 + y / 4 && y > 5 && y < 12) return dark[variant];
            if ((x * 17 + y * 23 + variant * 11) % 29 == 0) return light[variant];
            return body[variant];
        }

        public static Color32 Gravel(int x, int y) => (x * 13 + y * 7) % 11 == 0 ?
            new Color32(92, 67, 40, 255) : new Color32(54, 39, 24, 255);
    }
}
