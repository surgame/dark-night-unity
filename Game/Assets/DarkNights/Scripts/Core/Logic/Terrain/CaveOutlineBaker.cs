using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>H5 有符号曼哈顿距离外轮廓移植；全局坐标噪声和有界采样边界保证任意分页等价于整图，输出只读表现占用。</summary>
    public static class CaveOutlineBaker
    {
        private static readonly int[] PatternB = { 0,0,1,1,2,2,1,1,0,0,-1,-1,-2,-2,-1,-1 };
        private static readonly int[] PatternC = { 0,1,2,2,1,0,-1,-2,-2,-1,0,1,1,0,-1,0 };
        public static byte[] Bake(Func<int, int, bool> source, int worldWidth, int worldHeight,
            int left, int top, int width, int height, CaveOutlineSettings settings, Action checkpoint = null)
        {
            if (source == null || settings == null || width < 1 || height < 1 || left < 0 || top < 0 ||
                left + width > worldWidth || top + height > worldHeight) throw new ArgumentException("外轮廓采样范围无效。");
            int halo = settings.Enabled ? settings.Reach + 2 : 0;
            int x0 = Math.Max(0, left - halo), y0 = Math.Max(0, top - halo);
            int w = Math.Min(worldWidth, left + width + halo) - x0, h = Math.Min(worldHeight, top + height + halo) - y0;
            var mask = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) mask[y * w + x] = source(x0 + x, y0 + y) ? (byte)1 : (byte)0;
            if (settings.Enabled)
            {
                var air = BackgroundPixelMath.Distance(mask, w, h, 0); var rock = BackgroundPixelMath.Distance(mask, w, h, 1);
                uint seed = BackgroundPixelMath.Seed(settings.Seed + "|" + settings.ModeKey);
                for (int y = 0; y < h; y++)
                {
                    checkpoint?.Invoke();
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x, distance = mask[i] != 0 ? air[i] : -rock[i];
                        double d = Displacement(x0 + x, y0 + y, settings, seed);
                        if (settings.Mode != CaveOutlineMode.Wave) d = Math.Floor(d / settings.Quantization + .5) * settings.Quantization;
                        mask[i] = distance + d >= 0 ? (byte)1 : (byte)0;
                    }
                }
            }
            var result = new byte[checked(width * height)];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int gx = left + x, gy = top + y, i = (gy - y0) * w + gx - x0;
                if (mask[i] == 0) continue;
                if (settings.Enabled && gx > 0 && gy > 0 && gx < worldWidth - 1 && gy < worldHeight - 1)
                {
                    int neighbors = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0) neighbors += mask[i + dy * w + dx];
                    if (neighbors <= 1) continue;
                }
                result[y * width + x] = 1;
            }
            return result;
        }
        private static double Displacement(int x, int y, CaveOutlineSettings p, uint seed)
        {
            double wave = Wave(x, y, p, seed);
            if (p.Mode == CaveOutlineMode.Wave) return wave;
            bool c = p.Mode == CaveOutlineMode.PixelC || p.Mode == CaveOutlineMode.HybridC;
            int[] pattern = c ? PatternC : PatternB;
            int phase = (int)((seed >> 3) % (uint)pattern.Length);
            int index = ((int)Math.Floor(x / Math.Max(p.Quantization, p.Wavelength / (double)pattern.Length)) + phase) % pattern.Length;
            double wobble = (BackgroundPixelMath.Noise(x / (p.Wavelength * 1.35), y / (p.Wavelength * 2.2), unchecked(seed + 151)) - .5) * .65;
            double pixel = Math.Floor((pattern[index] / 2.0 * .78 + wobble * .22) * p.Amplitude / p.Quantization + .5) * p.Quantization;
            return p.Mode == CaveOutlineMode.HybridB || p.Mode == CaveOutlineMode.HybridC ? wave * .72 + pixel * .48 : pixel;
        }
        private static double Wave(int x, int y, CaveOutlineSettings p, uint seed)
        {
            double u = x / (double)p.Wavelength, v = y / (p.Wavelength * 1.7);
            double a = (BackgroundPixelMath.Noise(u, v, seed) - .5) * 2;
            double b = (BackgroundPixelMath.Noise(u * .48 + 13.7, v * .52 + 7.1, unchecked(seed + 71)) - .5) * 2;
            return (a * .72 + b * .28) * p.Amplitude;
        }
    }
}
