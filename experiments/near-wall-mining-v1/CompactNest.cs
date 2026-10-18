using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.NearWallMining
{
    /// <summary>
    /// 可省略的近壁侧巢静态布局；保存封闭巢腔、原岩地板、可挖侧壁与接触时刻。
    /// 接触只启用侧挖选择，必须再挖完封岩才揭露，不因采矿比例自动穿墙或生成怪物。
    /// </summary>
    public sealed class CompactNest
    {
        public string SiteId { get; }
        public int Anchor { get; }
        public int EggAnchor => Anchor + 1;
        public int[] Pocket { get; }
        public int[] Floor { get; }
        public int[] Gate { get; }
        public int ContactAction { get; }
        public int ContactCell { get; }
        public int RemainingStock { get; }

        public CompactNest(NearWallSite site, CompactOreVariant ore)
        {
            SiteId = "rift-expanded:near-wall:" + ore.Id;
            int center = site.Map(ore.NestU, ore.NestV), x = center % 320 - 1, y = center / 320 - 1;
            var pocket = new HashSet<int>();
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 3; dx++)
                    if (dy > 0 || dx == 1) pocket.Add((y + dy) * 320 + x + dx);
            Pocket = pocket.OrderBy(i => i).ToArray();
            Floor = Enumerable.Range(0, 3).Select(dx => (y + 3) * 320 + x + dx).ToArray();
            Anchor = (y + 2) * 320 + x; var minerals = new HashSet<int>(ore.Cells);
            if (Pocket.Concat(Floor).Any(i => !site.Rock(i) || minerals.Contains(i) || ore.Bridges.Contains(i)))
                throw new InvalidOperationException("近壁侧巢占格冲突。");
            if (Pocket.Any(i => NearWallSite.Neighbors(i).Any(n => site.Air(n) || minerals.Contains(n) || site.OtherMineral(n))))
                throw new InvalidOperationException("近壁侧巢不封闭。");
            for (int n = 1; n < Math.Floor(ore.Excavation.Length * .65); n++)
            {
                int at = ore.Excavation[n]; if (!minerals.Contains(at)) continue;
                int[] path = FindGate(site, at, pocket, new HashSet<int>(Floor), new HashSet<int>(ore.Excavation));
                if (path == null) continue;
                ContactCell = at; ContactAction = n + 1; Gate = path;
                var mined = new HashSet<int>(ore.Excavation.Take(ContactAction));
                RemainingStock = ore.Cells.Select((i, k) => mined.Contains(i) ? 0 : ore.Amounts[k]).Sum();
                return;
            }
            throw new InvalidOperationException("近壁矿中段旁未找到1至3格可挖封岩。");
        }

        private static int[] FindGate(NearWallSite site, int start, HashSet<int> pocket,
            HashSet<int> floor, HashSet<int> minerals)
        {
            var queue = new Queue<int>(); var parents = new Dictionary<int, int>();
            var depth = new Dictionary<int, int> { [start] = 0 }; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); if (depth[current] >= 4) continue;
                foreach (int next in NearWallSite.Neighbors(current))
                {
                    if (depth.ContainsKey(next) || floor.Contains(next)) continue;
                    if (pocket.Contains(next))
                    {
                        var path = new List<int>(); int item = current;
                        while (item != start) { path.Add(item); item = parents[item]; }
                        path.Reverse(); if (path.Count > 0) return path.ToArray(); continue;
                    }
                    if (!site.Rock(next) || minerals.Contains(next)) continue;
                    parents[next] = current; depth[next] = depth[current] + 1; queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
