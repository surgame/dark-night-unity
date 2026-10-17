using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 一处关联富矿的冻结巢穴方案，包含隐蔽空腔、保留封岩、地面锚点及有界出虫额度。
    /// 隐蔽空腔是开发端地形补丁，封岩仅在揭露示意中挖开；这里不保存触发或清理状态。
    /// </summary>
    public sealed class NestPlacement
    {
        public string Id { get; }
        public string VeinId { get; }
        public int Anchor { get; }
        public int EggAnchor { get; }
        public int Tier { get; }
        public int SpawnQuota { get; }
        public int RevealCount { get; }
        public int Contact { get; }
        public int Center { get; }
        public int RemainingCells { get; }
        public int RemainingReserves { get; }
        public IReadOnlyList<int> Pocket { get; }
        public IReadOnlyList<int> Gate { get; }
        public IReadOnlyList<int> Floor { get; }

        public NestPlacement(string mapId, VeinInput vein, int anchor, int eggAnchor,
            int[] pocket, int[] gate, int[] floor, int quota, int revealCount, int center)
        {
            if (pocket.Length != 15 || gate.Length < 1 || gate.Length > 3 || floor.Length < 3 ||
                revealCount < 2 || revealCount > Math.Floor(vein.Cells.Count * .6) ||
                pocket.Intersect(gate).Any()) throw new ArgumentException("巢穴空腔、封岩或地面模板无效。");
            Id = mapId + ":nest:" + vein.Id; VeinId = vein.Id; Anchor = anchor;
            EggAnchor = eggAnchor; Tier = vein.Tier; SpawnQuota = quota;
            RevealCount = revealCount; Contact = vein.Cells[revealCount - 1]; Center = center;
            RemainingCells = vein.Cells.Count - revealCount;
            RemainingReserves = vein.Capacity - vein.Capacity / vein.Cells.Count * revealCount -
                Math.Min(revealCount, vein.Capacity % vein.Cells.Count);
            Pocket = Array.AsReadOnly((int[])pocket.Clone()); Gate = Array.AsReadOnly((int[])gate.Clone());
            Floor = Array.AsReadOnly((int[])floor.Clone());
        }
    }
}
