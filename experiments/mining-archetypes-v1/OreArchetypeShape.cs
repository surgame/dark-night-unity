using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 五种矿脉轮廓的有限制作配方，组合细脉、分枝、粗细不齐的矿囊与富核。
    /// 构造期独占写入局部格，完成后只读提供真实地图占格；浏览器不生成或重抽矿脉。
    /// </summary>
    public sealed class OreArchetypeShape
    {
        private readonly ArchetypeSite site;
        private readonly Dictionary<int, string> cells = new Dictionary<int, string>();
        private readonly IReadOnlyDictionary<int, string> frozenCells;
        public string Id { get; }
        public string Name { get; private set; }
        public string Choice { get; private set; }
        public int NestU { get; private set; }
        public int NestV { get; private set; }
        public int NestWidth { get; private set; } = 5;
        public int NestHeight { get; private set; } = 4;
        public IReadOnlyDictionary<int, string> Cells => frozenCells;

        public OreArchetypeShape(ArchetypeSite site, string id)
        {
            this.site = site; Id = id;
            switch (id)
            {
                case "branch":
                    Name = "分叉夹巢"; Choice = "铁支可先收，金支从巢旁继续"; NestU = 7; NestV = 14;
                    Line(0, 0, 0, 3); Line(0, 3, 2, 6); Line(2, 6, 13, 11);
                    Line(2, 6, 1, 16); Line(13, 11, 13, 18, "gold");
                    Lobe(1, 15, 2, 3); Lobe(13, 17, 2, 3, "gold"); Lobe(11, 10, 2, 2, "gold");
                    break;
                case "core":
                    Name = "细脉富核"; Choice = "细线索通向富核，侧壁惊巢后仍有厚矿可采"; NestU = 12; NestV = 13;
                    Line(0, 0, 0, 3); Line(0, 3, 2, 6); Line(2, 6, 3, 9);
                    Lobe(4, 13, 4, 5); Lobe(4, 14, 2, 3, "gold");
                    Line(5, 17, 7, 20, "gold");
                    break;
                case "strata":
                    Name = "错层夹巢"; Choice = "先沿层采，再决定是否挖穿层间岩壁"; NestU = 7; NestV = 8; NestWidth = 3; NestHeight = 5;
                    Line(0, 0, 0, 4); Line(0, 4, 14, 4); Line(2, 5, 10, 5);
                    Line(1, 4, 1, 11); Line(1, 11, 13, 12, "gold");
                    Line(3, 13, 12, 13); Line(12, 13, 12, 19);
                    Line(3, 19, 15, 19, "gold"); Line(5, 20, 12, 20, "gold");
                    break;
                case "beads":
                    Name = "串珠矿囊"; Choice = "小矿囊可先带走，下一袋更厚也更靠近巢"; NestU = 11; NestV = 10;
                    Line(0, 0, 2, 4); Lobe(2, 4, 2, 2);
                    Line(2, 5, 4, 10); Lobe(4, 10, 3, 2); Lobe(5, 10, 1, 1, "gold");
                    Line(4, 11, 7, 15); Line(7, 15, 5, 18);
                    Lobe(5, 19, 4, 3); Lobe(5, 20, 3, 2, "gold");
                    break;
                case "crescent":
                    Name = "环抱矿弧"; Choice = "沿弧采矿逐渐贴近巢腔，可选择到此收手"; NestU = 6; NestV = 13; NestWidth = 5; NestHeight = 5;
                    Line(0, 0, 1, 5); Line(1, 5, 6, 7); Line(6, 7, 10, 10);
                    Line(10, 10, 11, 15, "gold"); Line(11, 15, 9, 19, "gold");
                    Line(9, 19, 4, 20, "gold"); Lobe(10, 13, 2, 3, "gold");
                    Lobe(7, 8, 2, 2); Lobe(6, 20, 3, 2, "gold");
                    break;
                default: throw new ArgumentException("未知矿脉方案。");
            }
            Line(0, 0, -2, -14);
            cells[site.Head] = "iron";
            frozenCells = new ReadOnlyDictionary<int, string>(cells);
        }

        private void Line(int u0, int v0, int u1, int v1, string kind = "iron")
        {
            int steps = Math.Max(Math.Abs(u1 - u0), Math.Abs(v1 - v0));
            int u = u0, v = v0; Add(u, v, kind);
            for (int n = 1; n <= steps; n++)
            {
                int nextU = (int)Math.Round(u0 + (u1 - u0) * n / (double)steps);
                int nextV = (int)Math.Round(v0 + (v1 - v0) * n / (double)steps);
                while (u != nextU) { u += Math.Sign(nextU - u); Add(u, v, kind); }
                while (v != nextV) { v += Math.Sign(nextV - v); Add(u, v, kind); }
            }
        }

        private void Lobe(int u, int v, int rx, int ry, string kind = "iron")
        {
            for (int dy = -ry; dy <= ry; dy++)
                for (int dx = -rx; dx <= rx; dx++)
                {
                    double distance = dx * dx / (double)(rx * rx) + dy * dy / (double)(ry * ry);
                    double irregularity = ((u + dx) * 17 + (v + dy) * 31) % 7 * .035;
                    if (distance <= 1 + irregularity) Add(u + dx, v + dy, kind);
                }
        }

        private void Add(int u, int v, string kind)
        {
            int cell = site.Map(u, v);
            if (cell < 0) throw new InvalidOperationException("矿脉超出真实地图。");
            cells[cell] = kind;
        }
    }
}
