using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.NearWallMining
{
    /// <summary>
    /// 三种预制近壁矿团的离线有限配方；只持有本候选矿格、薄岩桥和冻结开挖序。
    /// 同一实岩露头、同种矿和相同首格容量，后方规模不同；浏览器不重新抽取内容。
    /// </summary>
    public sealed class CompactOreVariant
    {
        public string Id { get; }
        public string Name { get; }
        public string Label { get; }
        public int Stock { get; }
        public int BodyDepth { get; }
        public int NestU { get; }
        public int NestV { get; }
        public int[] Cells { get; }
        public int[] Bridges { get; }
        public int[] Excavation { get; }
        public int[] Amounts { get; }
        public int[] Components { get; }

        public CompactOreVariant(NearWallSite site, string id)
        {
            Id = id; string[] rows;
            switch (id)
            {
                case "shallow":
                    Name = "贴壁浅团"; Label = "小团即止"; Stock = 16;
                    rows = new[] { "OOO", ".OOO", "..OO" }; break;
                case "bulge":
                    Name = "短颈鼓包"; Label = "两格后开始增厚"; Stock = 64; NestU = 8; NestV = 3;
                    rows = new[] { "OOO", ".OOOO", ".OOOOO", "..OOOO", "..OOO", "...O" }; break;
                case "double":
                    Name = "邻接双团"; Label = "薄岩后再见一团"; Stock = 76; NestU = 7; NestV = 5;
                    rows = new[] { "OOO", ".OOO", "..OO", "..R", "..OO", ".OOOO", ".OOOO", "..OOO" }; break;
                default: throw new ArgumentException("未知近壁配方。");
            }
            var cells = new List<int>(); var bridges = new List<int>(); int depth = 0;
            for (int v = 0; v < rows.Length; v++)
                for (int u = 0; u < rows[v].Length; u++)
                {
                    if (rows[v][u] == '.') continue;
                    int index = site.Map(u, v);
                    if (!site.Rock(index)) throw new InvalidOperationException(id + "不适合当前近壁围岩，应另选位或缩小。");
                    if (rows[v][u] == 'R') bridges.Add(index);
                    else { cells.Add(index); depth = Math.Max(depth, u); }
                }
            Cells = cells.ToArray(); Bridges = bridges.ToArray(); BodyDepth = depth;
            Components = ComponentSizes(Cells);
            var allowed = new HashSet<int>(Cells.Concat(Bridges)); var order = new List<int>();
            Visit(site.Head, allowed, new HashSet<int>(), order);
            if (order.Count != allowed.Count) throw new InvalidOperationException("开挖序不连通。");
            Excavation = order.ToArray();
            int[] oreOrder = order.Where(cells.Contains).ToArray(); var amounts = new Dictionary<int, int>();
            for (int n = 0; n < oreOrder.Length; n++)
                amounts[oreOrder[n]] = n < 2 ? 2 : (Stock - 4) / (oreOrder.Length - 2) +
                    (n - 2 < (Stock - 4) % (oreOrder.Length - 2) ? 1 : 0);
            Amounts = Cells.Select(i => amounts[i]).ToArray();
            if (Cells.Count(i => NearWallSite.Neighbors(i).Any(site.Air)) != 1)
                throw new InvalidOperationException("相似露头比较要求仅一格初始可见矿。");
        }

        private static void Visit(int cell, HashSet<int> allowed, HashSet<int> seen, List<int> order)
        {
            if (!allowed.Contains(cell) || !seen.Add(cell)) return;
            order.Add(cell);
            foreach (int next in NearWallSite.Neighbors(cell)) Visit(next, allowed, seen, order);
        }

        private static int[] ComponentSizes(IEnumerable<int> cells)
        {
            var remaining = new HashSet<int>(cells); var sizes = new List<int>();
            while (remaining.Count > 0)
            {
                var queue = new Queue<int>(); int first = remaining.First(); remaining.Remove(first); queue.Enqueue(first); int count = 0;
                while (queue.Count > 0)
                {
                    count++;
                    foreach (int next in NearWallSite.Neighbors(queue.Dequeue()))
                        if (remaining.Remove(next)) queue.Enqueue(next);
                }
                sizes.Add(count);
            }
            return sizes.OrderBy(n => n).ToArray();
        }
    }
}
