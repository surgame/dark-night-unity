using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>轮廓跟随点缀生成器的冻结参数；三个深度层独立控制密度、范围和贴边倾向，默认值保持 v16.1。</summary>
    public sealed class BackgroundContourSettings
    {
        public string Seed { get; }
        public int NearAmount { get; }
        public int NearWidth { get; }
        public int NearBlend { get; }
        public int MiddleAmount { get; }
        public int MiddleWidth { get; }
        public int MiddleBlend { get; }
        public int DeepAmount { get; }
        public int DeepWidth { get; }
        public int DeepBlend { get; }
        public string Identity => string.Join(",", "contour-follow-v1", Seed.Length + ":" + Seed, NearAmount, NearWidth, NearBlend,
            MiddleAmount, MiddleWidth, MiddleBlend, DeepAmount, DeepWidth, DeepBlend);
        public BackgroundContourSettings(string seed = "DECOR-0922", int nearAmount = 58, int nearWidth = 24, int nearBlend = 72,
            int middleAmount = 72, int middleWidth = 40, int middleBlend = 52, int deepAmount = 48, int deepWidth = 10, int deepBlend = 68)
        {
            if (string.IsNullOrWhiteSpace(seed) || seed.Length > 80) throw new ArgumentException("点缀种子无效。");
            foreach (int v in new[] { nearAmount, nearBlend, middleAmount, middleBlend, deepAmount, deepBlend })
                if (v < 0 || v > 100) throw new ArgumentException("点缀百分比必须在 0–100。");
            foreach (int v in new[] { nearWidth, middleWidth, deepWidth })
                if (v < 1 || v > 120) throw new ArgumentException("点缀范围必须在 1–120 原生像素。");
            Seed = seed; NearAmount = nearAmount; NearWidth = nearWidth; NearBlend = nearBlend;
            MiddleAmount = middleAmount; MiddleWidth = middleWidth; MiddleBlend = middleBlend;
            DeepAmount = deepAmount; DeepWidth = deepWidth; DeepBlend = deepBlend;
        }
    }
}
