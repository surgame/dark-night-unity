using System;
using System.Collections.Generic;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// 沿露头法线向真实围岩内生长有弯曲、厚薄和分枝的连续矿体，输出顺序也是揭露顺序。
    /// 算法仅在开发端运行，有界停止，不改空腔、不穿基岩、不跨入另一处天然通道。
    /// </summary>
    public sealed class MiningVeinGrowth
    {
        private readonly MiningRouteField field;
        private readonly uint seed;

        public MiningVeinGrowth(MiningRouteField field, string seed)
        {
            this.field = field; this.seed = TerrainRandom.Hash(seed);
        }

        public List<int> Grow(int head, int wall, int target, HashSet<int> occupied)
        {
            var result = new List<int> { head };
            var seen = new HashSet<int> { head };
            var frontier = new List<int> { wall };
            int hx = head % 320, hy = head / 320;
            int dx = wall % 320 - hx, dy = wall / 320 - hy;
            int length = Math.Max(2, target / 3 + 2);
            while (frontier.Count > 0 && result.Count < target)
            {
                int best = -1; double bestScore = double.NegativeInfinity;
                for (int i = 0; i < frontier.Count; i++)
                {
                    int cell = frontier[i], x = cell % 320 - hx, y = cell / 320 - hy;
                    double along = x * dx + y * dy, across = x * -dy + y * dx;
                    double bend = Bend(along, head);
                    double score = along * .1 - Math.Abs(across - bend) * 1.5 +
                        TerrainRandom.Noise(seed, cell, head) * 1.8;
                    if (score > bestScore) { best = i; bestScore = score; }
                }
                int next = frontier[best]; frontier.RemoveAt(best);
                if (!seen.Add(next) || !field.IsWall(next) || occupied.Contains(next)) continue;
                int nx = next % 320 - hx, ny = next / 320 - hy;
                int penetration = nx * dx + ny * dy, sideways = nx * -dy + ny * dx;
                if (penetration < 1 || penetration > length || Math.Abs(sideways - Bend(penetration, head)) > 3 ||
                    penetration > 2 && field.Thickness(next) < 2) continue;
                result.Add(next);
                foreach (int neighbor in MiningRouteField.Neighbors(next))
                    if (!seen.Contains(neighbor) && !frontier.Contains(neighbor)) frontier.Add(neighbor);
            }
            return result;
        }

        private double Bend(double along, int head)
        {
            double phase = TerrainRandom.Noise(seed, head, 1) * Math.PI * 2;
            double drift = TerrainRandom.Noise(seed, head, 2) * 1.4 - .7;
            return Math.Min(1, along / 3) * (Math.Sin(along * .6 + phase) * 1.3 + drift * along);
        }
    }
}
