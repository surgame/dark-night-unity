using System.Linq;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>矿床专用可靠会话投影；一床一对象，格数组立即复制为冻结值，字段变动随正式游戏协议升版。</summary>
    [MemoryPackable]
    public partial class MineralDepositWire
    {
        public int Id { get; set; }
        public float X { get; set; }
        public int Y { get; set; }
        public string RoomKind { get; set; }
        public string Rarity { get; set; }
        public string ResourceId { get; set; }
        public int MaximumDurability { get; set; }
        public int UnitsPerHarvest { get; set; }
        public int RequiredMiningLevel { get; set; }
        public MineralCellWire[] Cells { get; set; }

        public static MineralDepositWire From(MineralDepositViewData value) => new MineralDepositWire
        {
            Id = value.Id, X = value.X, Y = value.Y, RoomKind = value.RoomKind, Rarity = value.Rarity,
            ResourceId = value.ResourceId, MaximumDurability = value.MaximumDurability, UnitsPerHarvest = value.UnitsPerHarvest,
            RequiredMiningLevel = value.RequiredMiningLevel, Cells = value.Cells.Select(MineralCellWire.From).ToArray()
        };
        public MineralDepositViewData Freeze() => new MineralDepositViewData(Id, X, Y, RoomKind, Rarity,
            ResourceId, MaximumDurability, UnitsPerHarvest, RequiredMiningLevel, Cells?.Select(cell => cell.Freeze()).ToArray());
    }
}
