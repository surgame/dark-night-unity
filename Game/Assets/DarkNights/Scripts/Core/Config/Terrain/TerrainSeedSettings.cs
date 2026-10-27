using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 制作端地图种子选择策略；固定、随机和池内抽样共享同一生成配方。
    /// 只消费调用方提供的随机输入，不自行抽样或修改当前种子；实际结果由配方Seed和预制地图保存。
    /// </summary>
    [Serializable]
    public sealed class TerrainSeedSettings
    {
        public const string Fixed = "fixed";
        public const string Random = "random";
        public const string Pool = "pool";
        public static readonly string[] Modes = { Fixed, Random, Pool };
        public string Mode = Fixed;
        public string[] Seeds = Array.Empty<string>();

        public TerrainSeedSettings CopyValidated()
        {
            if (Array.IndexOf(Modes, Mode) < 0) throw new ArgumentException("未知地图种子策略。");
            var pool = Seeds ?? Array.Empty<string>();
            if (pool.Length > 128 || Mode == Pool && pool.Length == 0)
                throw new ArgumentException("种子池须为1–128项。");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string seed in pool)
            {
                ValidateSeed(seed);
                if (!seen.Add(seed)) throw new ArgumentException("种子池包含重复项：" + seed);
            }
            return new TerrainSeedSettings { Mode = Mode, Seeds = (string[])pool.Clone() };
        }

        /// <summary>一次显式选择；固定模式使用当前指定值，随机模式使用调用方新值，种子池只返回已有条目。</summary>
        public string Select(string fixedSeed, string randomSeed, uint selection)
        {
            var settings = CopyValidated();
            string result = settings.Mode == Fixed ? fixedSeed : settings.Mode == Random ? randomSeed :
                settings.Seeds[(int)(selection % (uint)settings.Seeds.Length)];
            ValidateSeed(result);
            return result;
        }

        public static void ValidateSeed(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed) || seed.Length > 80 || seed != seed.Trim())
                throw new ArgumentException("地图种子须为1–80字且不含首尾空白。");
        }
    }
}
