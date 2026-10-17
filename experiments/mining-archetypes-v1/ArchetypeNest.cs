using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 同一矿脉旁的一处冻结巢腔，含贴地锚点、支撑及可从不同矿段揭露的封岩。
    /// 只属于开发端布局候选，关闭模块即可省略全部空腔补丁，不创建运行巢穴对象。
    /// </summary>
    public sealed class ArchetypeNest
    {
        public string SiteId { get; }
        public int Anchor { get; }
        public int EggAnchor { get; }
        public IReadOnlyList<int> Pocket { get; }
        public IReadOnlyList<int> Floor { get; }
        public IReadOnlyList<ArchetypeGate> Gates { get; }

        public ArchetypeNest(string id, int anchor, int[] pocket, int[] floor, IEnumerable<ArchetypeGate> gates)
        {
            SiteId = "rift-expanded:archetype:" + id; Anchor = anchor; EggAnchor = anchor + 1;
            Pocket = Array.AsReadOnly((int[])pocket.Clone()); Floor = Array.AsReadOnly((int[])floor.Clone());
            Gates = Array.AsReadOnly(gates.OrderBy(g => g.RevealCount).ToArray());
            if (Gates.Count == 0) throw new ArgumentException("巢腔必须具备可揭露的侧壁。");
        }
    }
}
