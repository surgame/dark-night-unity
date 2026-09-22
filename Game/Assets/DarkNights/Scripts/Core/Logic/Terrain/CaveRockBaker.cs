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
            int left, int top, int width, int height, Action checkpoint = null, int stoneSize = 5, CaveOutlineSettings outline = null, CaveMaskField modified = null)
        {
            if (stoneSize < 2 || stoneSize > 12 || solid == null || worldWidth < 1 || worldHeight < 1 || width < 1 || height < 1 ||
                left < 0 || top < 0 || left + width > worldWidth || top + height > worldHeight)
                throw new ArgumentException("岩壁采样矩形越界。");
            if (modified != null && (modified.Width != worldWidth || modified.Height != worldHeight))
                throw new ArgumentException("修饰轮廓与世界尺寸不一致。");
            int x0 = Math.Max(0, left - DistanceCap), y0 = Math.Max(0, top - DistanceCap);
            int w = Math.Min(worldWidth, left + width + DistanceCap) - x0;
            int h = Math.Min(worldHeight, top + height + DistanceCap) - y0;
            var mask = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) mask[y * w + x] = (modified != null ? modified.Solid(x0 + x, y0 + y) : solid(x0 + x, y0 + y)) ? (byte)1 : (byte)0;
            if (modified == null && outline != null) mask = CaveOutlineBaker.Bake(solid, worldWidth, worldHeight, x0, y0, w, h, outline, checkpoint);
            var distances = BackgroundPixelMath.Distance(mask, w, h, 0);
            uint hash = BackgroundPixelMath.Seed(seed);
            var baseTones = new CaveRockToneField(left, top, width, height, hash, stoneSize);
            var grains = modified?.Grains;
            var fineTones = new CaveRockToneField[grains?.Count ?? 0];
            for (int i = 0; i < fineTones.Length; i++)
                fineTones[i] = new CaveRockToneField(left, top, width, height, hash, grains[i].StoneSize);
            var result = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < width; x++)
                {
                    int gx = left + x, gy = top + y, k = (y * width + x) * 4;
                    int depth = Math.Min(DistanceCap, (int)distances[(gy - y0) * w + gx - x0]);
                    if (depth == 0) continue;
                    int tone = baseTones.Tone(gx, gy);
                    double exposure = (At(gx, gy - 1) ? 0 : 1) + (At(gx, gy - 2) ? 0 : .55) +
                        (At(gx - 1, gy - 1) ? 0 : .25) + (At(gx + 1, gy - 1) ? 0 : .25);
                    double shade = (.22 + .88 * Math.Exp(-(depth - 1) / 1.9 / 7)) *
                        (.94 + (BackgroundPixelMath.Hash(gx, gy, unchecked(hash + 777)) - .5) * .08) *
                        (depth == 1 ? 1.14 : depth == 2 ? 1.06 : 1) *
                        (1 + Math.Min(exposure, .8) * .18 * Math.Exp(-(depth - 1) / 3.5));
                    for (int c = 0; c < 3; c++) result[k + c] = BackgroundPixelMath.Round(Palette[tone, c] * shade);
                    for (int i = 0; i < fineTones.Length; i++)
                    {
                        var grain = grains[i]; int p = gy * worldWidth + gx;
                        double weight = grain.Weight(p);
                        if (weight <= 0) continue;
                        int fineTone = fineTones[i].Tone(gx, gy);
                        if (grain.Tone(p) > 0) fineTone = (int)Math.Floor(fineTone * .55 + (grain.Tone(p) - 1) * .45 + .5);
                        for (int c = 0; c < 3; c++) result[k + c] = BackgroundPixelMath.Round(result[k + c] * (1 - weight) +
                            BackgroundPixelMath.Round(Palette[fineTone, c] * shade) * weight);
                    }
                    result[k + 3] = 255;
                }
            }
            return result;
            bool At(int x, int y) => x >= 0 && y >= 0 && x >= x0 && y >= y0 && x < x0 + w && y < y0 + h && mask[(y - y0) * w + x - x0] != 0;
        }

        internal static CaveRockFacet Create(int x, int y, uint seed, double stoneSize)
        {
            uint s = unchecked(seed + 316);
            double v = BackgroundPixelMath.Hash(x, y, s), v2 = BackgroundPixelMath.Hash(x, y, unchecked(s + 155));
            double individual = 2 + Math.Floor(BackgroundPixelMath.Hash(x, y, unchecked(s + 331)) * 4);
            double regional = 2.6 + BackgroundPixelMath.Noise((x + .5) * stoneSize / 30, (y + .5) * stoneSize / 30, unchecked(s + 665)) * 1.6;
            return new CaveRockFacet((x + .2 + v * .62) * stoneSize, (y + .2 + v2 * .64) * stoneSize, (int)Math.Floor(individual * .45 + regional * .55 + .5));
        }
    }
}
