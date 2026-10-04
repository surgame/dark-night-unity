using System.Collections.Generic;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>单矿床的冻结恢复合同；保存全部初始格及耗尽墓碑，不保存汇总存量或客户端 TileId，实际规则由匹配的定义恢复。</summary>
    public sealed class MineralDepositSnapshot
    {
        public int Id { get; }
        public double X { get; }
        public int Y { get; }
        public string RoomKind { get; }
        public string Rarity { get; }
        public IReadOnlyList<MineralCellViewData> Cells { get; }

        public MineralDepositSnapshot(int id, double x, int y, string roomKind, string rarity, IReadOnlyList<MineralCellViewData> cells)
        {
            Id = id; X = x; Y = y; RoomKind = roomKind; Rarity = rarity;
            Cells = cells == null ? null : new List<MineralCellViewData>(cells).AsReadOnly();
        }
    }
}
