using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>参考 HTML 的独立 32 位随机流和格点噪声；不消费营地 RNG，整数溢出语义固定。</summary>
    public sealed class TerrainRandom
    {
        private uint state;
        public TerrainRandom(string seed) { state = Hash(seed); }
        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5;
                uint t = (state ^ (state >> 15)) * (1 | state);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
        public static uint Hash(string text)
        {
            uint h = 2166136261;
            for (int i = 0; i < text.Length; i++)
            {
                h = unchecked((h ^ text[i]) * 16777619);
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            }
            return h;
        }
        public static double Noise(uint seed, int x, int y = 0)
        {
            unchecked
            {
                uint n = (uint)x * 374761393 ^ (uint)y * 668265263 ^ seed;
                n = (n ^ (n >> 13)) * 1274126177;
                return (n ^ (n >> 16)) / 4294967296.0;
            }
        }
        public static double ValueNoise(uint seed, double x)
        {
            int i = (int)Math.Floor(x); double t = x - i;
            return Lerp(Noise(seed, i), Noise(seed, i + 1), t * t * (3 - 2 * t));
        }
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static int Round(double value) => (int)Math.Floor(value + .5);
    }
}
