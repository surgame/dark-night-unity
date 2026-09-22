using System;
using static DarkNights.Core.Logic.Terrain.BackgroundPixelMath;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>只读轮廓的任意矩形着色；中层使用完整邻域计算内侧柔边，裁页不引入人工透明边或跨页接缝。</summary>
    public static class BackgroundPageBaker
    {
        private static readonly double[][] Shades = { new[] { .66, .82, .98 }, new[] { .44, .58, .74 }, new[] { .28, .37, .47 } };
        // H5 materialPalette()[4] with warm_rock and coherence=1; fixed author sRGB, decoded once by Unity.
        private static readonly byte[] BaseColor = { 92, 70, 50 };
        public static byte[][] Bake(ICaveBackgroundLayout source, int left, int top, int width, int height,
            int softness = 2, Action checkpoint = null)
        {
            if (width < 1 || height < 1 || width > source.Width || height > source.Height || softness < 0 || softness > 4)
                throw new ArgumentException("背景页面尺寸或柔边无效。");
            var layers = new byte[3][];
            for (int layer = 0; layer < 3; layer++)
            {
                var rgba = layers[layer] = new byte[checked(width * height * 4)];
                int add = layer == 0 ? 631 : layer == 1 ? 621 : 611;
                uint seed = unchecked(source.LayoutSeed + (uint)add);
                for (int y = 0; y < height; y++)
                {
                    checkpoint?.Invoke();
                    for (int x = 0; x < width; x++)
                    {
                        int wx = left + x, wy = top + y, k = (y * width + x) * 4;
                        if (!source.Solid(layer, wx, wy)) continue;
                        double n1 = Noise(wx / (14.0 + add % 5 * 3), wy / (22.0 + add % 7 * 2), seed);
                        double n2 = Noise(wx / (39.0 + add % 3 * 7), wy / (12.0 + add % 4 * 5), seed + 73);
                        double mix = n1 * .44 + n2 * .36 + Hash(wx >> 1, wy >> 1, seed + 191) * .20;
                        int tone = mix > .68 ? 2 : mix > .38 ? 1 : 0;
                        for (int c = 0; c < 3; c++) rgba[k + c] = Round(BaseColor[c] * Shades[layer][tone]);
                        double alpha = layer == 0 ? 255 : layer == 1 ? 242 : 235;
                        if (layer == 1 && softness > 0)
                        {
                            int distance = InteriorDistance(source, wx, wy, softness + 1);
                            double t = Clamp(distance / (softness + 1.0));
                            alpha *= t * t * (3 - 2 * t);
                        }
                        rgba[k + 3] = Round(alpha);
                    }
                }
            }
            return layers;
        }
        private static int InteriorDistance(ICaveBackgroundLayout source, int x, int y, int cap)
        {
            for (int d = 1; d < cap; d++) for (int dx = -d; dx <= d; dx++)
            {
                int dy = d - Math.Abs(dx);
                if (!source.Solid(1, x + dx, y + dy) || !source.Solid(1, x + dx, y - dy)) return d;
            }
            return cap;
        }
    }
}
