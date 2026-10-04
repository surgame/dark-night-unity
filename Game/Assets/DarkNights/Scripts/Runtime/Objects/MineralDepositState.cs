using GameCore.Objects.NetworkStates;
using DarkNights.Core.Config;
using MemoryPack;
using System;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单个 YYGC 矿床拥有稳定格集合及每格存量；复制时克隆值数组，草稿、冻结及池归还互不影响，汇总值由 Behaviour 派生。</summary>
    [MemoryPackable, StateData]
    public partial record MineralDepositState
    {
        public uint Sequence { get; set; }
        public int Id { get; internal set; }
        public string PlacementKey { get; internal set; }
        public float X { get; internal set; }
        public int Y { get; internal set; }
        public string RoomKind { get; internal set; }
        public string Rarity { get; internal set; }
        public MineralCellState[] Cells { get; internal set; } = Array.Empty<MineralCellState>();

        public void CopyFrom(IStateData source)
        {
            if (!(source is MineralDepositState value)) throw new ArgumentException("Expected mineral deposit state.", nameof(source));
            Sequence = value.Sequence; Id = value.Id; PlacementKey = value.PlacementKey;
            X = value.X; Y = value.Y; RoomKind = value.RoomKind; Rarity = value.Rarity;
            Cells = value.Cells == null ? Array.Empty<MineralCellState>() : (MineralCellState[])value.Cells.Clone();
        }
    }
}
