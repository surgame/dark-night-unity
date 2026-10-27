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
        public bool DisableValleySculpt;
        public double Amplitude = 1;
        public double OreDensity = 1;
        public string ResourceProfile = CaveExplorationProfile;
        public const string NaturalLayout = "natural-caves";
        public const string ConnectedRooms = "connected-rooms";
        public static readonly string[] Layouts = { ConnectedRooms, NaturalLayout };
        public string Layout = "";
        public CaveLayoutSettings Structure;
        public TerrainMineralSettings Minerals;
        // 仅供导入边界读取历史序列化输入，标准配方中恒为空。
        public CaveRuleSettings Rules;
        public const string NoiseSurface = "base-noise";
        public const string LandformSurface = "landform";
        // 空值仅用于读取旧配置；导入后固定为显式算法，不再以 Enabled 驱动生成。
        public string SurfaceAlgorithm = "";
        public bool UsesLandform => SurfaceAlgorithm == LandformSurface ||
            string.IsNullOrEmpty(SurfaceAlgorithm) && Relief?.Enabled == true;
        public SurfaceReliefSettings Relief = new SurfaceReliefSettings();
        public CaveRegionOverride[] RegionOverrides = Array.Empty<CaveRegionOverride>();
        public TerrainSeedSettings SeedSelection = new TerrainSeedSettings();
        [NonSerialized] public int[] SurfaceRows;
        public const int GeneratorVersion = 1;
        public const int Width = 320;
        public const int Height = 192;
        public const string CaveExplorationProfile = "cave-exploration";
        public const string GameplayResourceProfile = "dark-nights";
        public const string ReferenceResourceProfile = "reference";
        public static readonly string[] SurfaceNames = { "needles", "terraces", "karst", "basin", "rolling", "broken" };

        public TerrainGenerationSettings CopyValidated()
        {
            var copy = (TerrainGenerationSettings)MemberwiseClone();
            TerrainRecipeImport.Normalize(copy);
            copy.SurfaceAlgorithm = string.IsNullOrEmpty(copy.SurfaceAlgorithm) ?
                (copy.Relief?.Enabled == true ? LandformSurface : NoiseSurface) : copy.SurfaceAlgorithm;
            if (copy.SurfaceAlgorithm != NoiseSurface && copy.SurfaceAlgorithm != LandformSurface)
                throw new ArgumentException("未知地表轮廓算法。");
            TerrainSeedSettings.ValidateSeed(copy.Seed);
            if (Array.IndexOf(SurfaceNames, copy.Surface) < 0 || Array.IndexOf(Layouts, copy.Layout) < 0)
                throw new ArgumentException("未知地表或洞穴布局算法。");
            if (copy.ResourceProfile != GameplayResourceProfile && copy.ResourceProfile != ReferenceResourceProfile &&
                copy.ResourceProfile != CaveExplorationProfile) throw new ArgumentException("未知资源分布方案。");
            if (double.IsNaN(copy.Amplitude) || copy.Amplitude < .3 || copy.Amplitude > 1.6 ||
                double.IsNaN(copy.OreDensity) || copy.OreDensity < .2 || copy.OreDensity > 2)
                throw new ArgumentOutOfRangeException(nameof(Amplitude), "地表起伏或矿脉密度越界。");
            if ((copy.Layout != NaturalLayout || copy.Minerals.Algorithm != TerrainMineralSettings.BaseDeposits) &&
                copy.ResourceProfile != CaveExplorationProfile) throw new ArgumentException("所选布局／矿物算法需要洞穴资源方案。");
            if (copy.Minerals.Algorithm == TerrainMineralSettings.RegionalVeins && copy.Layout != ConnectedRooms)
                throw new ArgumentException("区域矿物算法需要连通洞室布局。");
            if (copy.SurfaceRows != null)
            {
                if (copy.SurfaceRows.Length != Width) throw new ArgumentException("派生地表输入宽度无效。");
                foreach (int row in copy.SurfaceRows)
                    if (row < 12 || row > Height - 12) throw new ArgumentException("派生地表输入越界。");
            }
            copy.SeedSelection = (copy.SeedSelection ?? new TerrainSeedSettings()).CopyValidated();
            if (copy.SeedSelection.Mode == TerrainSeedSettings.Pool &&
                Array.IndexOf(copy.SeedSelection.Seeds, copy.Seed) < 0)
                throw new ArgumentException("本次种子不在当前种子池内，请按策略抽取下一张地图。");
            copy.Relief = (copy.Relief ?? new SurfaceReliefSettings()).CopyValidated();
            copy.RegionOverrides = copy.CopyRegionOverrides();
            copy.SurfaceRows = copy.SurfaceRows == null ? null : (int[])copy.SurfaceRows.Clone();
            return copy;
        }

        private CaveRegionOverride[] CopyRegionOverrides()
        {
            var values = RegionOverrides ?? Array.Empty<CaveRegionOverride>();
            if (values.Length > 22) throw new ArgumentException("区域覆盖数量超过空间上限。");
            var seen = new System.Collections.Generic.HashSet<int>();
            return Array.ConvertAll(values, value =>
            {
                var copy = value?.CopyValidated() ?? throw new ArgumentException("区域覆盖不能为空。");
                if (!seen.Add(copy.Region)) throw new ArgumentException("同一区域只能有一份矿物覆盖。");
                return copy;
            });
        }

    }
}
