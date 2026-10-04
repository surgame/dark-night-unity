using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>矿床状态内部的单格值；仅所属 YYGC Behaviour 在同步事务草稿里修改，内容版本只在耗尽时推进。</summary>
    [MemoryPackable]
    public partial struct MineralCellState
    {
        public int U { get; internal set; }
        public int V { get; internal set; }
        public int Capacity { get; internal set; }
        public int Remaining { get; internal set; }
        public int Durability { get; internal set; }
        public ulong ContentVersion { get; internal set; }

        internal MineralCellViewData Freeze(ulong foregroundVersion = 0) => new MineralCellViewData(U, V, Capacity, Remaining, Durability, ContentVersion, foregroundVersion);
        internal static MineralCellState From(MineralCellViewData cell) => new MineralCellState
        { U = cell.U, V = cell.V, Capacity = cell.Capacity, Remaining = cell.Remaining, Durability = cell.Durability, ContentVersion = cell.ContentVersion };
    }
}
