using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>冻结背景的洞口装配；复用现有三层 RGBA 与掩码轮廓，额外基础后壁完整覆盖地下，不拥有可编辑格子或深度渐隐。</summary>
    public sealed class CaveEntranceBackdrop
    {
        private readonly ICaveBackgroundLayout layout;
        private readonly int[] tops;
        private readonly bool empty;
        public CaveEntranceBackdrop(BackgroundBakeDescriptor reference, ICaveBackgroundLayout layout,
            int rimReach = 128, int rearDepth = 80, Action checkpoint = null)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (reference == null || rimReach < 0 || rimReach > 512 || rearDepth < 16 || rearDepth > 192)
                throw new ArgumentException("洞口背景定位参数无效。");
            ushort[] skyline = TerrainVisualCoordinates.SurfaceSkyline(reference);
            if (layout.Width != skyline.Length || layout.Height != reference.Height * 8)
                throw new ArgumentException("现有背景与初始地图尺寸不一致。");
            int bottom = layout.Height;
            var anchors = new int[skyline.Length / 128 + 1];
            bool any = false;
            for (int section = 0; section < anchors.Length; section++)
            {
                int center = Math.Min(skyline.Length - 1, section * 128), anchor = bottom;
                for (int x = Math.Max(0, center - rimReach); x <= Math.Min(skyline.Length - 1, center + rimReach); x++)
                    anchor = Math.Min(anchor, skyline[x]);
                anchors[section] = anchor; any |= anchor < bottom;
            }
            empty = !any; tops = new int[layout.Width];
            for (int x = 0; x < tops.Length; x++)
            {
                checkpoint?.Invoke();
                int section = x / 128, offset = x % 128;
                int rim = (anchors[section] * (128 - offset) + anchors[section + 1] * offset) / 128;
                int cap = Math.Min(bottom, rim + rearDepth);
                tops[x] = cap;
                // 优先沿用已有岩片的真实轮廓。空隙深处由原掩码图集拼接完整基础墙。
                for (int y = Math.Max(layout.Top, rim); y < cap; y++)
                    if (layout.Solid(0, x, y) || layout.Solid(1, x, y) || layout.Solid(2, x, y))
                    { tops[x] = y; break; }
            }
        }

        public byte[][] Bake(int left, int top, int width, int height, int softness = 2, Action checkpoint = null)
        {
            if (left < 0 || top < 0 || width < 1 || height < 1 || left + width > layout.Width || top + height > layout.Height)
                throw new ArgumentException("洞口背景分页范围无效。");
            var layers = new byte[4][];
            if (empty)
            {
                for (int i = 0; i < 4; i++) layers[i] = new byte[checked(width * height * 4)];
                return layers;
            }
            byte[][] original = BackgroundPageBaker.Bake(layout, left, top, width, height, softness, checkpoint);
            for (int i = 0; i < 3; i++) layers[i] = original[i];
            layers[3] = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < width; x++)
                {
                    int wx = left + x, wy = top + y, k = (y * width + x) * 4;
                    if (!Covered(wx, wy)) continue;
                    // 原 CaveStrata 地下底板的噪声尺度、色阶与调色范围；没有随深度变色。
                    double n = BackgroundPixelMath.Noise(wx / 26.0, wy / 23.0, 17) * .8 +
                        BackgroundPixelMath.Noise(wx / 8.0 + 91, wy / 10.0 + 91, 17) * .2;
                    double shade = n > .66 ? .73 : n > .36 ? .63 : .53;
                    layers[3][k] = BackgroundPixelMath.Round((38 + (70 - 38) * shade) * .87);
                    layers[3][k + 1] = BackgroundPixelMath.Round((29 + (53 - 29) * shade) * .87);
                    layers[3][k + 2] = BackgroundPixelMath.Round((20 + (38 - 20) * shade) * .87);
                    layers[3][k + 3] = 255;
                }
            }
            return layers;
        }
        private bool Covered(int x, int y)
        {
            int gx = x / 8 * 8, gy = y / 8 * 8, next = Math.Min(gx + 8, tops.Length - 1);
            int mask = (gy >= tops[gx] ? 1 : 0) | (gy >= tops[next] ? 2 : 0) |
                (gy + 8 >= tops[gx] ? 4 : 0) | (gy + 8 >= tops[next] ? 8 : 0);
            int variant = Math.Min(3, (int)(BackgroundPixelMath.Hash(gx / 8, gy / 8, layout.LayoutSeed) * 4));
            return BackgroundMaskAtlas.Sample(variant, mask, x % 8, y % 8);
        }
    }
}
