using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>矿格的有界会话 wire 值；收包后立即冻结，不持有业务状态或通过网络字段授予采集权限。</summary>
    [MemoryPackable]
    public partial struct MineralCellWire
    {
        public int U { get; set; }
        public int V { get; set; }
        public int Capacity { get; set; }
        public int Remaining { get; set; }
        public int Durability { get; set; }
        public ulong ContentVersion { get; set; }
        public ulong ForegroundContentVersion { get; set; }
        public static MineralCellWire From(MineralCellViewData cell) => new MineralCellWire
        { U = cell.U, V = cell.V, Capacity = cell.Capacity, Remaining = cell.Remaining, Durability = cell.Durability,
            ContentVersion = cell.ContentVersion, ForegroundContentVersion = cell.ForegroundContentVersion };
        public MineralCellViewData Freeze() => new MineralCellViewData(U, V, Capacity, Remaining, Durability, ContentVersion, ForegroundContentVersion);
    }
}
