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
        private readonly int originX, originY;
        public int Width { get; }
        public int Height { get; }
        public int Top { get; }
        public uint LayoutSeed { get; }

        public static BackgroundContourBaker Build(BackgroundBakeDescriptor source, Action checkpoint = null,
            CaveOutlineSettings outline = null, BackgroundContourSettings settings = null, int decorationStartRow = 43)
        {
            if (decorationStartRow < 0 || decorationStartRow >= source.Height)
                throw new ArgumentOutOfRangeException(nameof(decorationStartRow));
            var raw = TerrainVisualCoordinates.Rasterize(source);
            int w = source.Width * 8, h = source.Height * 8;
            var pixels = outline == null ? raw : CaveOutlineBaker.Bake((x, y) => raw[y * w + x] != 0, w, h, 0, 0, w, h, outline, checkpoint);
            return new BackgroundContourBaker(pixels, w, h, source.LayoutSeed, decorationStartRow * 8, checkpoint, settings);
        }

        /// <summary>仅栅格化指定窗口；全局噪声原点保留，调用方为边缘依赖预留邻域，不先生成整张像素占用图。</summary>
        public static BackgroundContourBaker BuildRegion(BackgroundBakeDescriptor source, int left, int top, int width, int height,
            Action checkpoint = null, CaveOutlineSettings outline = null, BackgroundContourSettings settings = null, int decorationStartRow = 43)
        {
            if (left < 0 || top < 0 || width < 8 || height < 8 || left % 8 != 0 || top % 8 != 0 ||
                left + width > source.Width * 8 || top + height > source.Height * 8)
                throw new ArgumentException("背景局部窗口无效。");
            bool Solid(int x, int y) => source.Material(x / 8, y / 8) != 0 &&
                TerrainShapeGeometry.Contains((TerrainCellShape)source.Shape(x / 8, y / 8), (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
            byte[] pixels;
            if (outline != null)
                pixels = CaveOutlineBaker.Bake(Solid, source.Width * 8, source.Height * 8, left, top, width, height, outline, checkpoint);
            else
            {
                pixels = new byte[width * height];
                for (int y = 0; y < height; y++)
                { checkpoint?.Invoke(); for (int x = 0; x < width; x++) pixels[y * width + x] = Solid(left + x, top + y) ? (byte)1 : (byte)0; }
            }
            return new BackgroundContourBaker(pixels, width, height, source.LayoutSeed,
                Math.Max(0, Math.Min(height - 1, decorationStartRow * 8 - top)), checkpoint, settings, left, top);
        }

        public BackgroundContourBaker(byte[] occupancy, int width, int height, string seed, int top, Action checkpoint = null,
            BackgroundContourSettings settings = null, int originX = 0, int originY = 0)
        {
            if (width < 8 || height < 8 || width > 2560 || height > 1536 || width % 8 != 0 || height % 8 != 0 ||
                occupancy == null || occupancy.Length != width * height || top < 0 || top >= height)
                throw new ArgumentException("背景栅格尺寸无效。");
            settings = settings ?? new BackgroundContourSettings();
            Width = width; Height = height; Top = top;
            this.originX = originX; this.originY = originY;
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
                    int wx = originX + x, wy = originY + y;
                    double d = distance[p], n1 = Noise(wx / 34.0, wy / 26.0, s + 11), n2 = Noise(wx / 17.0, wy / 41.0, s + 17);
                    double n3 = Noise(wx / 62.0, wy / 14.0, s + 23), diag = Noise((wx + wy) / 44.0, (wy - wx) / 51.0, s + 31);
                    double band = Noise(wx / 95.0, wy / 18.0, s + 37), edge = Clamp(1 - (d - 1) / (settings.NearWidth * .7));
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
                if (grids[2][y * gridWidth + x] != 0 && Hash(x + originX / 8, y + originY / 8, s + 101) > .28) grids[1][y * gridWidth + x] = 0;
            for (int gy = 1; gy < gridHeight - 1; gy++) for (int gx = 1; gx < gridWidth - 1; gx++)
            {
                int x = gx * 8, y = gy * 8, p = y * width + x, d = distance[p], at = gy * gridWidth + gx;
                if (y < top || occupancy[p] != 0 || d < 1 || d > settings.NearWidth * .7) continue;
                int adjMid = grids[1][at - 1] + grids[1][at + 1] + grids[1][at - gridWidth] + grids[1][at + gridWidth];
                int adjRock = occupancy[y * width + Math.Min(x + 8, width - 1)] + occupancy[y * width + x - 8] +
                    occupancy[Math.Min(y + 8, height - 1) * width + x] + occupancy[(y - 8) * width + x];
                double score = Noise((originX + x) / 22.0, (originY + y) / 19.0, s + 201) * .42 + Noise((originX + x) / 11.0, (originY + y) / 37.0, s + 203) * .18 +
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
            int variant = (int)(Hash(gx + originX / 8, gy + originY / 8, LayoutSeed + offset + 35) * 4);
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
