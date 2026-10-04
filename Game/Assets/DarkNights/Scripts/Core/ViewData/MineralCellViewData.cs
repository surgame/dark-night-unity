namespace DarkNights.Core.ViewData
{
    /// <summary>单矿格的冻结值合同；容量和内容版本随稳定坐标保存，耐久变化不改变连接材料，不引用 YYGC 状态数组。</summary>
    public readonly struct MineralCellViewData
    {
        public int U { get; }
        public int V { get; }
        public int Capacity { get; }
        public int Remaining { get; }
        public int Durability { get; }
        public ulong ContentVersion { get; }
        public ulong ForegroundContentVersion { get; }

        public MineralCellViewData(int u, int v, int capacity, int remaining, int durability, ulong contentVersion, ulong foregroundContentVersion = 0)
        { U = u; V = v; Capacity = capacity; Remaining = remaining; Durability = durability; ContentVersion = contentVersion; ForegroundContentVersion = foregroundContentVersion; }
    }
}
