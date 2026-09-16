using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>地图生成的可序列化输入；独立于营地规则，生成前校验并冻结，不作为运行状态。</summary>
    [Serializable]
    public sealed class TerrainGenerationSettings
    {
        public string Seed = "GREYPINE-1616";
        public string Surface = "needles";
        public bool OrganicCaves;
        public double Amplitude = 1;
        public double OreDensity = 1;
        public const int GeneratorVersion = 1;
        public const int Width = 320;
        public const int Height = 192;
        public static readonly string[] SurfaceNames = { "needles", "terraces", "karst", "basin", "rolling", "broken" };

        public TerrainGenerationSettings CopyValidated()
        {
            if (string.IsNullOrWhiteSpace(Seed) || Seed.Length > 80) throw new ArgumentException("种子长度必须为 1–80。");
            if (Array.IndexOf(SurfaceNames, Surface) < 0) throw new ArgumentException("未知地表算法。");
            if (double.IsNaN(Amplitude) || Amplitude < .3 || Amplitude > 1.6 ||
                double.IsNaN(OreDensity) || OreDensity < .2 || OreDensity > 2)
                throw new ArgumentOutOfRangeException(nameof(Amplitude), "地表起伏或矿脉密度越界。");
            return new TerrainGenerationSettings { Seed = Seed, Surface = Surface, OrganicCaves = OrganicCaves,
                Amplitude = Amplitude, OreDensity = OreDensity };
        }
    }
}
