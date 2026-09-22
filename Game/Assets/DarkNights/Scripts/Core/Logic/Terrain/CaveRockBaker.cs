using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>H5 v16.1 原生像素岩壁的纯计算移植；分面固定于世界像素，距离随只读轮廓更新，局部页带 25 像素采样边界。</summary>
    public static class CaveRockBaker
    {
        public const int DistanceCap = 25;
        private static readonly byte[,] Palette = { {27,21,15}, {38,29,20}, {53,41,29}, {70,53,38},
            {92,70,50}, {115,88,62}, {146,112,79}, {181,139,98} };

        public static byte[] Bake(Func<int, int, bool> solid, int worldWidth, int worldHeight, string seed,
            int left, int top, int width, int height, Action checkpoint = null, int stoneSize = 5, CaveOutlineSettings outline = null)
        {
            if (stoneSize < 2 || stoneSize > 12 || solid == null || worldWidth < 1 || worldHeight < 1 || width < 1 || height < 1 ||
                left < 0 || top < 0 || left + width > worldWidth || top + height > worldHeight)
                throw new ArgumentException("岩壁采样矩形越界。");
            int x0 = Math.Max(0, left - DistanceCap), y0 = Math.Max(0, top - DistanceCap);
            int w = Math.Min(worldWidth, left + width + DistanceCap) - x0;
            int h = Math.Min(worldHeight, top + height + DistanceCap) - y0;
            var mask = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) mask[y * w + x] = solid(x0 + x, y0 + y) ? (byte)1 : (byte)0;
            if (outline != null) mask = CaveOutlineBaker.Bake(solid, worldWidth, worldHeight, x0, y0, w, h, outline, checkpoint);
            var distances = BackgroundPixelMath.Distance(mask, w, h, 0);
            uint hash = BackgroundPixelMath.Seed(seed);
            int fx0 = left / stoneSize - 1, fy0 = top / stoneSize - 1;
            int fw = (left + width - 1) / stoneSize - fx0 + 2, fh = (top + height - 1) / stoneSize - fy0 + 2;
            var facets = new CaveRockFacet[fw * fh];
            for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++) facets[y * fw + x] = Create(x + fx0, y + fy0, hash, stoneSize);
            var result = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < width; x++)
                {
                    int gx = left + x, gy = top + y, k = (y * width + x) * 4;
                    int depth = Math.Min(DistanceCap, (int)distances[(gy - y0) * w + gx - x0]);
                    if (depth == 0) continue;
                    double first = double.MaxValue, second = double.MaxValue;
                    CaveRockFacet nearest = default;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        var f = facets[(gy / stoneSize + dy - fy0) * fw + gx / stoneSize + dx - fx0];
                        double d = (gx - f.X) * (gx - f.X) + (gy - f.Y) * (gy - f.Y) * 1.18;
                        if (d < first) { second = first; first = d; nearest = f; }
                        else if (d < second) second = d;
                    }
                    int tone = Math.Max(0, Math.Min(7, nearest.Tone + (gy < nearest.Y - 1 ? 1 : 0) - (gy > nearest.Y + 1 ? 1 : 0)));
                    if (Math.Sqrt(second) - Math.Sqrt(first) < .36) tone = 0;
                    double exposure = (At(gx, gy - 1) ? 0 : 1) + (At(gx, gy - 2) ? 0 : .55) +
                        (At(gx - 1, gy - 1) ? 0 : .25) + (At(gx + 1, gy - 1) ? 0 : .25);
                    double shade = (.22 + .88 * Math.Exp(-(depth - 1) / 1.9 / 7)) *
                        (.94 + (BackgroundPixelMath.Hash(gx, gy, unchecked(hash + 777)) - .5) * .08) *
                        (depth == 1 ? 1.14 : depth == 2 ? 1.06 : 1) *
                        (1 + Math.Min(exposure, .8) * .18 * Math.Exp(-(depth - 1) / 3.5));
                    for (int c = 0; c < 3; c++) result[k + c] = BackgroundPixelMath.Round(Palette[tone, c] * shade);
                    result[k + 3] = 255;
                }
            }
            return result;
            bool At(int x, int y) => x >= 0 && y >= 0 && x >= x0 && y >= y0 && x < x0 + w && y < y0 + h && mask[(y - y0) * w + x - x0] != 0;
        }

        private static CaveRockFacet Create(int x, int y, uint seed, int stoneSize)
        {
            uint s = unchecked(seed + 316);
            double v = BackgroundPixelMath.Hash(x, y, s), v2 = BackgroundPixelMath.Hash(x, y, unchecked(s + 155));
            double individual = 2 + Math.Floor(BackgroundPixelMath.Hash(x, y, unchecked(s + 331)) * 4);
            double regional = 2.6 + BackgroundPixelMath.Noise((x + .5) * stoneSize / 30, (y + .5) * stoneSize / 30, unchecked(s + 665)) * 1.6;
            return new CaveRockFacet((x + .2 + v * .62) * stoneSize, (y + .2 + v2 * .64) * stoneSize, (int)Math.Floor(individual * .45 + regional * .55 + .5));
        }
    }
}
