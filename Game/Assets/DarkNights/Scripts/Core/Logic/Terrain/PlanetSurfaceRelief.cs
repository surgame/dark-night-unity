using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 规则地图的可复现地表轮廓；在洞穴雕刻前确定丘陵与低谷，不按飞船泊位整平。
    /// 只输出纯高度数组，不在生成完成后回填或裁剪空腔，最终坡形与背景仍沿既有流程处理。
    /// </summary>
    public static class PlanetSurfaceRelief
    {
        /// <summary>先生成统一地表高度，再雕刻洞穴与入口，避免在已有空腔上后贴悬空山体。</summary>
        public static int[] CreateProfile(TerrainGenerationSettings input)
        {
            var relief = input.Relief.CopyValidated();
            uint seed = TerrainRandom.Hash(input.Seed + "|surface-relief-v1");
            var result = new int[TerrainGenerationSettings.Width];
            for (int x = 0; x < result.Length; x++)
            {
                double wave = Wave(input.Surface, seed, x / relief.WavelengthCells);
                result[x] = Math.Clamp(TerrainRandom.Round(PlayableTerrain.CampRow -
                    (relief.BaseHeightCells + wave * relief.HeightCells * input.Amplitude)),
                    12, TerrainGenerationSettings.Height - 12);
            }
            return result;
        }

        private static double Wave(string kind, uint seed, double x)
        {
            double phase = (seed % 1024) / 1024.0 * Math.PI * 2;
            double main = Math.Sin(x * Math.PI * 2 + phase);
            double detail = (TerrainRandom.ValueNoise(seed, x * 3) * 2 - 1) * .22;
            switch (kind)
            {
                case "needles": return Math.Sign(main) * Math.Pow(Math.Abs(main), 2) + detail;
                case "terraces": return Math.Round(main * 3) / 3 + detail * .25;
                case "karst": return main * .6 + Math.Pow(Math.Max(0, main), 3) * .4 + detail;
                case "basin": return -Math.Cos(x * Math.PI + phase) * .75 + detail;
                case "broken": return main * .6 + Math.Sin(x * Math.PI * 5 + phase) * .28 + detail;
                default: return main * .8 + detail;
            }
        }
    }
}
