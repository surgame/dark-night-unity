using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 在矿脉中段侧旁预留平行巢腔和短封岩；只输出不与原矿格／保护格重叠的局部补丁。
    /// 原天然通路不被挖改，模板净空仅作离线静态检查，实际角色和生物碰撞仍待Unity确认。
    /// </summary>
    public sealed class NestPocketPlanner
    {
        private readonly MiningInput input;
        private readonly NestPlacementSettings settings;

        public NestPocketPlanner(MiningInput input, NestPlacementSettings settings)
        {
            this.input = input; this.settings = settings;
        }

        public NestPlacement TryPlan(VeinInput vein, IReadOnlyList<NestPlacement> prior, HashSet<int> reserved)
        {
            var profile = new VeinSideProfile(vein);
            foreach (int contactIndex in profile.ContactIndices())
            {
                int contact = vein.Cells[contactIndex];
                var centers = new HashSet<int>();
                for (int side = -6; side <= 6; side++)
                    for (int along = -1; along <= 1; along++)
                    {
                        if (Math.Abs(side) < 3) continue;
                        int x = contact % 320 - side * profile.Dy + along * profile.Dx;
                        int y = contact / 320 + side * profile.Dx + along * profile.Dy;
                        if (x >= 5 && x < 315 && y >= 46 && y < 185) centers.Add(y * 320 + x);
                    }
                foreach (int center in centers.OrderBy(c => RichVeinNestPass.Hash(vein.Id + "|side-v2|" + c)))
                {
                    if (!profile.Beside(contact, center)) continue;
                    var result = PlanAt(vein, contactIndex, profile, center, prior, reserved);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private NestPlacement PlanAt(VeinInput vein, int contactIndex, VeinSideProfile profile, int center,
            IReadOnlyList<NestPlacement> prior, HashSet<int> reserved)
        {
            int width = profile.Width, height = profile.Height;
            int x = center % 320 - width / 2, y = center / 320 - height / 2;
            int[] pocket = Enumerable.Range(0, height).SelectMany(dy => Enumerable.Range(0, width)
                .Select(dx => (y + dy) * 320 + x + dx)).ToArray();
            int[] floor = Enumerable.Range(0, width).Select(dx => (y + height) * 320 + x + dx).ToArray();
            int anchor = (y + height - 1) * 320 + x + width / 2 - 1;
            if (prior.Any(p => SquaredDistance(p.Anchor, anchor) < settings.MinimumSpacing * settings.MinimumSpacing)) return null;
            if (pocket.Concat(floor).Any(i => !input.Carvable(i) || reserved.Contains(i))) return null;
            if (pocket.Any(i => Neighbors(i).Any(input.NaturalAir))) return null;
            if (pocket.Any(i => Neighbors(i).Any(input.Mineral))) return null;
            int[] gate = Gate(vein.Cells[contactIndex], new HashSet<int>(pocket), new HashSet<int>(floor), reserved);
            if (gate == null || gate.Length == 0 || gate.Length > 3) return null;
            return new NestPlacement(input.MapId, vein, anchor, anchor + 1,
                pocket, gate, floor, settings.SpawnQuota, contactIndex + 1, center);
        }

        private int[] Gate(int start, HashSet<int> pocket, HashSet<int> floor, HashSet<int> reserved)
        {
            var queue = new Queue<int>(); var parent = new Dictionary<int, int>();
            var depth = new Dictionary<int, int> { [start] = 0 }; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (depth[current] >= 4) continue;
                foreach (int next in Neighbors(current))
                {
                    if (depth.ContainsKey(next) || reserved.Contains(next) || floor.Contains(next)) continue;
                    if (pocket.Contains(next))
                    {
                        var path = new List<int>(); int item = current;
                        while (item != start) { path.Add(item); item = parent[item]; }
                        path.Reverse();
                        if (path.Count > 0) return path.ToArray();
                        continue;
                    }
                    if (!input.Carvable(next)) continue;
                    parent[next] = current; depth[next] = depth[current] + 1; queue.Enqueue(next);
                }
            }
            return null;
        }

        public static IEnumerable<int> Neighbors(int index)
        {
            int x = index % 320, y = index / 320;
            if (x < 319) yield return index + 1;
            if (y < 191) yield return index + 320;
            if (x > 0) yield return index - 1;
            if (y > 0) yield return index - 320;
        }

        private static int SquaredDistance(int a, int b)
        {
            int x = a % 320 - b % 320, y = a / 320 - b / 320;
            return x * x + y * y;
        }
    }
}
