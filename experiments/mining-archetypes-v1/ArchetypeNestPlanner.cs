using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 按各矿脉配方的侧巢意图有界寻找真实围岩中的有机巢腔，避免矿格、保护格及天然空腔。
    /// 中途揭露后必须保留矿物；不以末梢兜底，不把静态净空当角色或生物通行验收。
    /// </summary>
    public sealed class ArchetypeNestPlanner
    {
        private readonly ArchetypeSite site;
        public ArchetypeNestPlanner(ArchetypeSite site) { this.site = site; }

        public ArchetypeNest Plan(OreArchetypeShape shape, IReadOnlyList<int> order)
        {
            var candidates = new List<(int U, int V)>();
            for (int dv = -4; dv <= 4; dv++)
                for (int du = -4; du <= 4; du++) candidates.Add((shape.NestU + du, shape.NestV + dv));
            foreach (var candidate in candidates.OrderBy(c =>
                (c.U - shape.NestU) * (c.U - shape.NestU) + (c.V - shape.NestV) * (c.V - shape.NestV)))
            {
                int center = site.Map(candidate.U, candidate.V);
                if (center < 0) continue;
                var layout = PlanAt(shape, order, center);
                if (layout != null) return layout;
            }
            return null;
        }

        private ArchetypeNest PlanAt(OreArchetypeShape shape, IReadOnlyList<int> order, int center)
        {
            int width = shape.NestWidth, height = shape.NestHeight;
            int x = center % 320 - width / 2, y = center / 320 - height / 2;
            var pocket = new HashSet<int>();
            for (int dy = 0; dy < height; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    if (dy == 0 && (dx == 0 || dx == width - 1)) continue;
                    pocket.Add((y + dy) * 320 + x + dx);
                }
            int[] floor = Enumerable.Range(0, width).Select(dx => (y + height) * 320 + x + dx).ToArray();
            if (pocket.Concat(floor).Any(i => !site.Rock(i) || shape.Cells.ContainsKey(i))) return null;
            if (pocket.Any(i => ArchetypeSite.Neighbors(i).Any(n => site.Air(n) || shape.Cells.ContainsKey(n) || site.OtherMineral(n)))) return null;
            int max = (int)Math.Floor(order.Count * .6), min = Math.Max(2, (int)(order.Count * .2));
            var gates = new List<ArchetypeGate>();
            for (int n = min; n < max; n++)
            {
                int[] path = Gate(order[n], pocket, new HashSet<int>(floor), shape.Cells);
                if (path == null) continue;
                gates.Add(new ArchetypeGate(order[n], n + 1, path)); break;
            }
            if (gates.Count == 0) return null;
            if (shape.Id == "branch" || shape.Id == "strata" || shape.Id == "crescent")
                for (int n = max; n < order.Count; n++)
                {
                    int[] path = Gate(order[n], pocket, new HashSet<int>(floor), shape.Cells);
                    if (path == null || path.Intersect(gates[0].Cells).Any()) continue;
                    gates.Add(new ArchetypeGate(order[n], n + 1, path)); break;
                }
            return new ArchetypeNest(shape.Id, (y + height - 1) * 320 + x + width / 2 - 1,
                pocket.OrderBy(i => i).ToArray(), floor, gates);
        }

        private int[] Gate(int start, HashSet<int> pocket, HashSet<int> floor, IReadOnlyDictionary<int, string> minerals)
        {
            var queue = new Queue<int>(); var parents = new Dictionary<int, int>();
            var depth = new Dictionary<int, int> { [start] = 0 }; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (depth[current] >= 4) continue;
                foreach (int next in ArchetypeSite.Neighbors(current))
                {
                    if (depth.ContainsKey(next) || floor.Contains(next)) continue;
                    if (pocket.Contains(next))
                    {
                        var path = new List<int>(); int item = current;
                        while (item != start) { path.Add(item); item = parents[item]; }
                        path.Reverse(); if (path.Count > 0) return path.ToArray();
                        continue;
                    }
                    if (!site.Rock(next) || minerals.ContainsKey(next)) continue;
                    parents[next] = current; depth[next] = depth[current] + 1; queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
