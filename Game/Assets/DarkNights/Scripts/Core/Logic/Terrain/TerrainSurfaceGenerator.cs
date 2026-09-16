using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>六种地表轮廓与地层填充；保持参考生成器的坐标、平整落点和边界材料语义。</summary>
    internal static class TerrainSurfaceGenerator
    {
        internal static void Fill(TerrainGenerationBuffer b, TerrainGenerationSettings s)
        {
            uint seed = TerrainRandom.Hash(s.Seed);
            int[,] peaks = { {113,39,14}, {153,-22,12}, {205,42,19}, {241,28,11}, {294,33,14} };
            for (int x = 0; x < TerrainGenerationBuffer.W; x++)
            {
                double n = TerrainRandom.ValueNoise(seed, x / 36.0) * 2 - 1;
                double n2 = TerrainRandom.ValueNoise(unchecked(seed + 41), x / 11.0) * 2 - 1, v = 70;
                switch (s.Surface)
                {
                    case "needles":
                        v += n * 6 + n2 * 2;
                        for (int p = 0; p < 5; p++) v -= peaks[p, 1] * Math.Exp(-Math.Pow((x - peaks[p, 0]) / (double)peaks[p, 2], 4));
                        break;
                    case "terraces": v += TerrainRandom.Round(n * 4) * 6 + n2 * 2; break;
                    case "karst": v += n * 10 - Math.Pow(Math.Max(0, Math.Sin(x * .115 + seed % 12)), 6) * 34; break;
                    case "basin": v += Math.Exp(-Math.Pow((x - 155) / 70.0, 2)) * 33 - n * 13; break;
                    case "rolling": v += n * 16 + n2 * 3; break;
                    case "broken": v += Math.Floor((n + 1) * 3) * 11 - 27 + n2 * 3; break;
                }
                b.Surface[x] = TerrainRandom.Round(Math.Clamp(70 + (v - 70) * s.Amplitude, 24, 106));
            }
            b.PadY[0] = 72; b.PadY[1] = b.Surface[179]; b.PadY[2] = b.Surface[272];
            for (int p = 0; p < 3; p++)
                for (int x = b.PadX[p] - b.PadRadius[p] - 6; x <= b.PadX[p] + b.PadRadius[p] + 6; x++)
                {
                    double blend = Math.Clamp((Math.Abs(x - b.PadX[p]) - b.PadRadius[p]) / 6.0, 0, 1);
                    b.Surface[x] = TerrainRandom.Round(TerrainRandom.Lerp(b.PadY[p], b.Surface[x], blend));
                }
            for (int y = 0; y < TerrainGenerationBuffer.H; y++)
                for (int x = 0; x < TerrainGenerationBuffer.W; x++)
                {
                    byte t = 0;
                    if (y >= b.Surface[x])
                    {
                        int depth = y - b.Surface[x]; t = (byte)(depth < 7 ? 1 : y < 112 ? 2 : 3);
                        if (depth < 2 && TerrainRandom.Noise(unchecked(seed + 7), x, y) > .62) t = 7;
                    }
                    if (x == 0 || x == TerrainGenerationBuffer.W - 1 || y >= TerrainGenerationBuffer.H - 3) t = 8;
                    b.Cells[y * TerrainGenerationBuffer.W + x] = t;
                }
        }
    }
}
