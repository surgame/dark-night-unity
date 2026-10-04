using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.ViewData
{
    /// <summary>单 YYGC 矿床对象的冻结投影；格数组复制为值集合，汇总存量仅由每格派生，不作为第二份权威状态。</summary>
    public sealed class MineralDepositViewData
    {
        public int Id { get; }
        public float X { get; }
        public int Y { get; }
        public string RoomKind { get; }
        public string Rarity { get; }
        public string ResourceId { get; }
        public string MineralKind => ResourceId;
        public int MaximumDurability { get; }
        public int UnitsPerHarvest { get; }
        public int RequiredMiningLevel { get; }
        public IReadOnlyList<MineralCellViewData> Cells { get; }
        public int Capacity => Cells.Sum(cell => cell.Capacity);
        public int Remaining => Cells.Sum(cell => cell.Remaining);
        public string Stage => Remaining == 0 ? "Depleted" : "Available";

        public MineralDepositViewData(int id, float x, int y, string roomKind, string rarity, string resourceId,
            int maximumDurability, int unitsPerHarvest, int requiredMiningLevel, IReadOnlyList<MineralCellViewData> cells)
        {
            Id = id; X = x; Y = y; RoomKind = roomKind; Rarity = rarity; ResourceId = resourceId;
            MaximumDurability = maximumDurability; UnitsPerHarvest = unitsPerHarvest; RequiredMiningLevel = requiredMiningLevel;
            if (cells == null || cells.Count < 1 || cells.Count > 64) throw new ArgumentException("矿床投影格数无效。");
            Cells = new List<MineralCellViewData>(cells).AsReadOnly();
        }
    }
}
