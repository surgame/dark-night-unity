using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>统一生成配方的矿物阶段输入；算法、种子、预算和侧巢全部随配置持久化，不读取窗口或本机预设。</summary>
    [Serializable]
    public sealed class TerrainMineralSettings
    {
        public const string BaseDeposits = "base-deposits";
        public const string WallVeins = "wall-veins";
        public const string RegionalVeins = "regional-veins";
        public static readonly string[] Algorithms = { BaseDeposits, WallVeins, RegionalVeins };
        public string Algorithm = BaseDeposits;
        public string Seed = "ORE-1";
        public int Revision;
        public int VeinCount = 8;
        public int Capacity = 180;
        public double EmptyShare = .18;
        public double ExposedShare = .35;
        public double RichShare = .35;
        public bool Nests = true;
        public string ContentKey => Seed + "|content-v1|" + Revision;

        public TerrainMineralSettings CopyValidated()
        {
            if (Array.IndexOf(Algorithms, Algorithm) < 0 || string.IsNullOrWhiteSpace(Seed) || Seed.Length > 80 ||
                Revision < 0 || VeinCount < 1 || VeinCount > 16 || Capacity < 96 || Capacity > 2000 ||
                !Range(EmptyShare, 0, .6) || !Range(ExposedShare, .1, .8) || !Range(RichShare, 0, .8))
                throw new ArgumentException("矿物算法、种子或参数越界。");
            return (TerrainMineralSettings)MemberwiseClone();
        }

        private static bool Range(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;
    }
}
