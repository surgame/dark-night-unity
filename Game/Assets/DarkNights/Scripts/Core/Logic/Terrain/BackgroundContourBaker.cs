using System;
using DarkNights.Core.Config.Terrain;
using static DarkNights.Core.Logic.Terrain.BackgroundPixelMath;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>H5 v16.1 三层轮廓逻辑的确定性移植；一次读取冻结参考，输出只读掩码网格供任意页面重建。</summary>
    public sealed class BackgroundContourBaker : ICaveBackgroundLayout
    {
        private readonly byte[][] grids;
        private readonly int gridWidth;
        private readonly int gridHeight;
        public int Width { get; }
        public int Height { get; }
        public int Top { get; }
        public uint LayoutSeed { get; }

        public static BackgroundContourBaker Build(BackgroundBakeDescriptor source, Action checkpoint = null, CaveOutlineSettings outline = null, BackgroundContourSettings settings = null)
        {
            var raw = TerrainVisualCoordinates.Rasterize(source);
            int w = source.Width * 8, h = source.Height * 8;
            var pixels = outline == null ? raw : CaveOutlineBaker.Bake((x, y) => raw[y * w + x] != 0, w, h, 0, 0, w, h, outline, checkpoint);
            return new BackgroundContourBaker(pixels, w, h, source.LayoutSeed, 43 * 8, checkpoint, settings);
        }

        public BackgroundContourBaker(byte[] occupancy, int width, int height, string seed, int top, Action checkpoint = null, BackgroundContourSettings settings = null)
        {
            if (width < 8 || height < 8 || width > 2560 || height > 1536 || width % 8 != 0 || height % 8 != 0 ||
                occupancy == null || occupancy.Length != width * height || top < 0 || top >= height)
                throw new ArgumentException("背景栅格尺寸无效。");
            settings = settings ?? new BackgroundContourSettings();
            Width = width; Height = height; Top = top;
            gridWidth = width / 8 + 1; gridHeight = height / 8 + 1;
            LayoutSeed = Seed(seed + "|" + settings.Seed + "|contour-follow-static-1");
            grids = new[] { new byte[gridWidth * gridHeight], new byte[gridWidth * gridHeight], new byte[gridWidth * gridHeight] };
            checkpoint?.Invoke();
            var distance = Distance(occupancy, width, height, 1);
            uint s = LayoutSeed;
            for (int gy = 0; gy < gridHeight; gy++)
            {
                checkpoint?.Invoke();
                for (int gx = 0; gx < gridWidth; gx++)
                {
                    int x = Math.Min(gx * 8, width - 1), y = Math.Min(gy * 8, height - 1), p = y * width + x;
                    if (y < top || occupancy[p] != 0 || distance[p] == 0) continue;
                    double d = distance[p], n1 = Noise(x / 34.0, y / 26.0, s + 11), n2 = Noise(x / 17.0, y / 41.0, s + 17);
                    double n3 = Noise(x / 62.0, y / 14.0, s + 23), diag = Noise((x + y) / 44.0, (y - x) / 51.0, s + 31);
                    double band = Noise(x / 95.0, y / 18.0, s + 37), edge = Clamp(1 - (d - 1) / (settings.NearWidth * .7));
                    double mid = Clamp(1 - Math.Abs(d - settings.MiddleWidth * .75) / (settings.MiddleWidth * 1.1)), inner = Clamp((d - 4) / (settings.DeepWidth * 2.4));
                    double midScore = n1 * .32 + n2 * .26 + diag * .18 + band * .10 + edge * .22 + mid * .20;
                    double deepScore = n1 * .20 + n2 * .24 + n3 * .24 + diag * .12 + inner * .22;
                    int at = gy * gridWidth + gx;
                    if (settings.DeepAmount > 0 && d >= 5.5 && d <= settings.DeepWidth * 3.2 && deepScore > .76 - settings.DeepAmount / 100.0 * .20 - settings.DeepBlend / 100.0 * .08) grids[2][at] = 1;
                    if (settings.MiddleAmount > 0 && d >= 1 && d <= settings.MiddleWidth * 1.9 && midScore > .66 - settings.MiddleAmount / 100.0 * .18 - settings.MiddleBlend / 100.0 * .08) grids[1][at] = 1;
                }
            }
            Smooth(2, 2); Cleanup(2); Smooth(1, 1); Cleanup(1);
            for (int y = 1; y < gridHeight - 1; y++) for (int x = 1; x < gridWidth - 1; x++)
                if (grids[2][y * gridWidth + x] != 0 && Hash(x, y, s + 101) > .28) grids[1][y * gridWidth + x] = 0;
            for (int gy = 1; gy < gridHeight - 1; gy++) for (int gx = 1; gx < gridWidth - 1; gx++)
            {
                int x = gx * 8, y = gy * 8, p = y * width + x, d = distance[p], at = gy * gridWidth + gx;
                if (y < top || occupancy[p] != 0 || d < 1 || d > settings.NearWidth * .7) continue;
                int adjMid = grids[1][at - 1] + grids[1][at + 1] + grids[1][at - gridWidth] + grids[1][at + gridWidth];
                int adjRock = occupancy[y * width + Math.Min(x + 8, width - 1)] + occupancy[y * width + x - 8] +
                    occupancy[Math.Min(y + 8, height - 1) * width + x] + occupancy[(y - 8) * width + x];
                double score = Noise(x / 22.0, y / 19.0, s + 201) * .42 + Noise(x / 11.0, y / 37.0, s + 203) * .18 +
                    (adjMid > 0 ? .18 : 0) + Clamp(1 - (d - 1) / 4.0) * .32 + (adjRock > 0 ? .18 : 0);
                if (settings.NearAmount > 0 && score > .72 - settings.NearAmount / 100.0 * .16 - settings.NearBlend / 100.0 * .06) grids[0][at] = 1;
            }
            Cleanup(0);
        }

        public bool Solid(int layer, int x, int y)
        {
            if (x < 0 || y < Top || x >= Width || y >= Height) return false;
            int gx = x / 8, gy = y / 8, at = gy * gridWidth + gx;
            byte[] grid = grids[layer];
            int mask = grid[at] | (grid[at + 1] << 1) | (grid[at + gridWidth] << 2) | (grid[at + gridWidth + 1] << 3);
            uint offset = layer == 0 ? 501u : layer == 1 ? 401u : 301u;
            int variant = (int)(Hash(gx, gy, LayoutSeed + offset + 35) * 4);
            return BackgroundMaskAtlas.Sample(variant, mask, x % 8, y % 8);
        }

        private void Smooth(int layer, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var previous = grids[layer]; var next = new byte[previous.Length];
                for (int y = 1; y < gridHeight - 1; y++) for (int x = 1; x < gridWidth - 1; x++)
                {
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0) n += previous[(y + dy) * gridWidth + x + dx];
                    next[y * gridWidth + x] = n >= 4 ? (byte)1 : (byte)0;
                }
                grids[layer] = next;
            }
        }
        private void Cleanup(int layer)
        {
            byte[] grid = grids[layer];
            for (int y = 1; y < gridHeight - 1; y++) for (int x = 1; x < gridWidth - 1; x++)
            {
                int i = y * gridWidth + x;
                if (grid[i - 1] + grid[i + 1] + grid[i - gridWidth] + grid[i + gridWidth] == 0) grid[i] = 0;
            }
        }
    }
}
