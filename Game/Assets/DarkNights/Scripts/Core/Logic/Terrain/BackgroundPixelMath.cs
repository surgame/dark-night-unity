using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>与 H5 对齐的无符号哈希、负坐标噪声和曼哈顿距离纯计算；不使用运行世界随机数或平台字符串哈希。</summary>
    public static class BackgroundPixelMath
    {
        public static uint Seed(string value)
        {
            uint hash = 2166136261;
            foreach (char c in value) hash = unchecked((hash ^ c) * 16777619);
            return hash;
        }
        public static double Hash(int x, int y, uint seed)
        {
            uint a = unchecked((uint)x * 374761393 ^ (uint)y * 668265263 ^ seed * 1442695041);
            a = unchecked((a ^ (a >> 13)) * 1274126177);
            return (a ^ (a >> 16)) / 4294967296.0;
        }
        public static double Noise(double x, double y, uint seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            return (Hash(ix, iy, seed) * (1 - u) + Hash(ix + 1, iy, seed) * u) * (1 - v) +
                (Hash(ix, iy + 1, seed) * (1 - u) + Hash(ix + 1, iy + 1, seed) * u) * v;
        }
        public static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        public static byte Round(double value) => (byte)Math.Max(0, Math.Min(255, Math.Floor(value + .5)));
        public static ushort[] Distance(byte[] pixels, int width, int height, byte target)
        {
            if (pixels == null || pixels.Length != checked(width * height) || width < 1 || height < 1)
                throw new ArgumentException("距离场尺寸无效。");
            const int cap = 4095;
            var d = new ushort[pixels.Length];
            for (int i = 0; i < d.Length; i++) d[i] = pixels[i] == target ? (ushort)0 : (ushort)cap;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                d[i] = (ushort)Math.Min(d[i], Math.Min(x > 0 ? d[i - 1] + 1 : cap, y > 0 ? d[i - width] + 1 : cap));
            }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                d[i] = (ushort)Math.Min(d[i], Math.Min(x + 1 < width ? d[i + 1] + 1 : cap, y + 1 < height ? d[i + width] + 1 : cap));
            }
            return d;
        }
        public static void SoftenAlpha(byte[] rgba, int width, int height, int softness)
        {
            if (softness < 0 || softness > 4 || rgba.Length != checked(width * height * 4))
                throw new ArgumentException("柔边参数无效。");
            if (softness == 0) return;
            int padded = width + 2;
            var mask = new byte[padded * (height + 2)];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                mask[(y + 1) * padded + x + 1] = rgba[(y * width + x) * 4 + 3] == 0 ? (byte)0 : (byte)1;
            var distance = Distance(mask, padded, height + 2, 0);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                double t = Clamp(distance[(y + 1) * padded + x + 1] / (softness + 1.0));
                int k = (y * width + x) * 4 + 3;
                rgba[k] = Round(rgba[k] * t * t * (3 - 2 * t));
            }
        }
    }
}
