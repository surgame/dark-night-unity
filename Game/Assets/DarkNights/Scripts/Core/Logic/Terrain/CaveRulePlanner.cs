using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>先规划主干与多级支线，再冻结洞室／接口的开发端算法；尝试有界，只约束数据边界和围岩间距，不运行人物通行算法。</summary>
    public static class CaveRulePlanner
    {
        public static CaveRulePlan Build(TerrainGenerationSettings settings)
        {
            var s = settings.CopyValidated(); var rules = s.Structure;
            if (s.Layout != TerrainGenerationSettings.ConnectedRooms) throw new ArgumentException("当前布局没有连通洞室规划。");
            var random = new TerrainRandom(s.Seed + "|cave-space-v2|" + rules.SpaceRevision);
            var rooms = new List<TerrainRoom>(); var edges = new List<CavePassage>();
            var levels = new List<int>(); var distances = new List<double>(); var messages = new List<string>();
            int trunk = Math.Max(4, (int)Math.Round(rules.RoomCount * (1 - rules.BranchShare)));
            for (int n = 0; n < rules.RoomCount; n++)
            {
                bool main = n < trunk;
                int parent = rooms.Count == 0 ? -1 : main ? rooms.Count - 1 : Parent(rooms.Count, trunk, levels, rules, random);
                bool placed = false;
                for (int attempt = 0; attempt < 32 && !placed; attempt++)
                {
                    string kind = PickShape(rules.RoomMix, random);
                    if (rooms.Count > 0 && rooms[rooms.Count - 1].Kind == kind && random.Next() < .7)
                        kind = PickShape(rules.RoomMix, random);
                    double size = rules.RoomScale * (.8 + random.Next() * .4);
                    bool landmark = rules.Road == "cluster" && main && n % 3 == 1;
                    int width = (int)((kind == "slant-rift" ? 16 : landmark ? 37 : 22 + random.Next() * 9) * size);
                    int height = (int)((kind == "low-seam" ? 9 : kind == "slant-rift" ? 27 : landmark ? 24 : 15 + random.Next() * 6) * size);
                    Position(n, trunk, main, parent < 0 ? null : rooms[parent], rules.Road, random, out int x, out int y);
                    x += (int)(random.Next() * 9) - 4; y += (int)(random.Next() * 9) - 4;
                    var room = new TerrainRoom(kind, x, y, width, height);
                    if (room.Left < 64 || room.Left + width >= 307 || room.Top < 49 || room.Top + height >= 181 ||
                        rooms.Any(other => Overlap(room, other, 4))) continue;
                    int index = rooms.Count;
                    rooms.Add(room); levels.Add(main ? 0 : levels[parent] + 1);
                    if (parent < 0) distances.Add(0);
                    else
                    {
                        var p = rooms[parent];
                        int bx = (x + p.X) / 2, by = (y + p.Y) / 2;
                        if (rules.Road == "switchback") { bx = x; by = p.Y; }
                        else if (rules.Road == "rift") { bx = p.X; by = y; }
                        else { bx += (int)(random.Next() * 17) - 8; by += (int)(random.Next() * 13) - 6; }
                        int radius = 3 + (int)(random.Next() * 3) - 1;
                        int cover = !main && random.Next() < rules.RockGap ? 4 : 0;
                        edges.Add(new CavePassage(parent, index, CavePassageKind.Open, bx, by, radius, cover));
                        distances.Add(distances[parent] + Length(p.X, p.Y, bx, by) + Length(bx, by, x, y));
                    }
                    placed = true;
                }
                if (!placed) messages.Add("洞室请求" + n + "：32次范围／围岩尝试后跳过。");
            }
            if (rooms.Count < 3) throw new InvalidOperationException("规则候选空间不足，未生成至少三间洞室。");
            return new CaveRulePlan(rooms.ToArray(), edges.ToArray(), levels.ToArray(), distances.ToArray(), messages.ToArray());
        }

        private static int Parent(int count, int trunk, List<int> levels, CaveLayoutSettings rules, TerrainRandom r)
        {
            var candidates = Enumerable.Range(0, count).Where(i => levels[i] < rules.BranchDepth).ToArray();
            if (rules.Road == "roots" && count > trunk && r.Next() < .7)
            {
                var branches = candidates.Where(i => i >= trunk).ToArray();
                if (branches.Length != 0) candidates = branches;
            }
            return candidates[(int)(r.Next() * candidates.Length)];
        }

        private static void Position(int n, int trunk, bool main, TerrainRoom parent, string road, TerrainRandom r, out int x, out int y)
        {
            double t = n / (double)Math.Max(1, trunk - 1);
            if (!main)
            {
                double angle = r.Next() * Math.PI * 2, length = 30 + r.Next() * 28;
                x = parent.X + (int)(Math.Cos(angle) * length); y = parent.Y + (int)(Math.Sin(angle) * length * .8); return;
            }
            x = 86 + (int)(t * 198); y = 71 + (int)(Math.Sin(t * Math.PI * 1.4) * 22 + t * 42);
            if (road == "switchback")
            {
                int row = n / 3, col = n % 3;
                x = 88 + (row % 2 == 0 ? col : 2 - col) * 91; y = 67 + row * 39;
            }
            else if (road == "cluster") { x = 88 + (int)(t * 195); y = 90 + (int)(Math.Sin(n * 2.3) * 27); }
            else if (road == "rift") { x = 102 + n % 2 * 28; y = 65 + (int)(t * 96); }
            else if (road == "roots") { x = 90 + (int)(t * 190); y = 104 + (int)(Math.Sin(n * 1.4) * 39); }
        }

        private static string PickShape(string mix, TerrainRandom r)
        {
            int[] weights = mix == "fissures" ? new[] { 5, 1, 2, 5, 1, 1, 1 } : mix == "halls" ? new[] { 1, 5, 4, 1, 1, 4, 1 } :
                mix == "pockets" ? new[] { 1, 1, 1, 1, 5, 2, 5 } : new[] { 2, 2, 2, 2, 2, 2, 2 };
            int choice = (int)(r.Next() * weights.Sum());
            for (int i = 0; i < weights.Length; i++) { choice -= weights[i]; if (choice < 0) return CaveLayoutSettings.Shapes[i]; }
            return CaveLayoutSettings.Shapes[0];
        }

        private static bool Overlap(TerrainRoom a, TerrainRoom b, int gap) =>
            a.Left < b.Left + b.Width + gap && a.Left + a.Width + gap > b.Left && a.Top < b.Top + b.Height + gap && a.Top + a.Height + gap > b.Top;
        private static double Length(int ax, int ay, int bx, int by) => Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
    }
}
