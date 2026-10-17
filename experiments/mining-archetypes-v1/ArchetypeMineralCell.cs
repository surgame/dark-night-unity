namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 单个冻结矿格的种类与储量，用于同预算形态对比并转换为既有矿床蓝图。
    /// 不保存采掘进度、耐久、掉落或玩家发现状态，权威运行仍归正式地图。
    /// </summary>
    public sealed class ArchetypeMineralCell
    {
        public int Index { get; }
        public string Kind { get; }
        public int Amount { get; }
        public ArchetypeMineralCell(int index, string kind, int amount)
        {
            Index = index; Kind = kind; Amount = amount;
        }
    }
}
