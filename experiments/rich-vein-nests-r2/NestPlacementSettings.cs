using System;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 可插拔巢位步骤的冻结参数；本版概率和模板尺寸是离线试参，不是正式平衡或AI规则。
    /// 关闭时必须返回空巢位与空地形补丁，保留同一输入矿脉和地形。
    /// </summary>
    public sealed class NestPlacementSettings
    {
        public bool Enabled { get; }
        public int MinimumReserves => 18;
        public int MinimumWallCells => 6;
        public int MaximumNests => 8;
        public int MinimumSpacing => 12;
        public int SpawnQuota => 2;

        public NestPlacementSettings(bool enabled) { Enabled = enabled; }
        public double Probability(int tier)
        {
            if (tier < 0 || tier > 2) throw new ArgumentOutOfRangeException(nameof(tier));
            return tier == 0 ? .6 : tier == 1 ? .75 : .9;
        }
        public bool Eligible(VeinInput vein) => vein.Interior && vein.Capacity >= MinimumReserves &&
            vein.Cells.Count - 1 >= MinimumWallCells;
    }
}
