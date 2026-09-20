using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>天然洞穴实验的纯生成器；先建立带回环的隐藏图，再栅格化洞室并覆盖部分通路，不改历史生成向量。</summary>
    public static class CaveExplorationGenerator
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static TerrainBlueprint Generate(TerrainGenerationSettings input)
        {
            var settings = input.CopyValidated();
            var random = new TerrainRandom(settings.Seed + ":cave-exploration-v1");
            var cells = new byte[W * H];
            var protection = new bool[cells.Length];
            var soft = new bool[cells.Length];
            int[] surface = Surface(settings);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                bool border = x < 3 || x >= W - 3 || y >= H - 3;
                protection[i] = border;
                cells[i] = border ? (byte)8 : y < surface[x] ? (byte)0 : y < surface[x] + 4 ? (byte)1 : (byte)2;
            }
            var rooms = Rooms(random);
            var passages = Connect(rooms, random);
            foreach (var room in rooms) CarveRoom(cells, room, random);
            foreach (var edge in passages)
            {
                var a = rooms[edge.From]; var b = rooms[edge.To];
                CarveLine(cells, a.X, a.Y, edge.BendX, edge.BendY, edge.Radius);
                CarveLine(cells, edge.BendX, edge.BendY, b.X, b.Y, edge.Radius);
            }
            // 地表入口远离着陆区；另一个入口通向不同支路。
            CarveLine(cells, rooms[0].X - 13, surface[rooms[0].X - 13] - 4, rooms[0].X, rooms[0].Y, 4);
            var other = rooms[1];
            CarveLine(cells, other.X + 12, surface[other.X + 12] - 3, other.X, other.Y, 3);
            foreach (var edge in passages) Cover(cells, soft, rooms, edge);
            // 着陆台只有二十四列，仍保留其余地表坡度。
            for (int x = 24; x <= 47; x++) for (int y = surface[x]; y <= surface[x] + 2; y++)
            { cells[y * W + x] = 1; protection[y * W + x] = true; soft[y * W + x] = false; }
            return new TerrainBlueprint(settings, cells, protection, surface, rooms.ToArray(), soft,
                Array.Empty<TerrainDepositBlueprint>(), passages.ToArray());
        }

        private static int[] Surface(TerrainGenerationSettings settings)
        {
            var result = new int[W]; uint seed = TerrainRandom.Hash(settings.Seed + ":surface");
            for (int x = 0; x < W; x++)
            {
                double broad = TerrainRandom.ValueNoise(seed, x / 52.0) * 22;
                double small = TerrainRandom.ValueNoise(seed + 1, x / 13.0) * 5;
                int target = 39 + (int)((broad + small) * settings.Amplitude);
                result[x] = x == 0 ? target : Math.Max(result[x - 1] - 1, Math.Min(result[x - 1] + 1, target));
            }
            int landing = result[24];
            for (int x = 24; x <= 47; x++) result[x] = landing;
            for (int x = 48; x < W; x++) result[x] = Math.Clamp(result[x], result[x - 1] - 1, result[x - 1] + 1);
            return result;
        }

        private static List<TerrainRoom> Rooms(TerrainRandom r)
        {
            var rooms = new List<TerrainRoom>
            {
                new TerrainRoom("entry", 85 + (int)(r.Next() * 25), 84, 30, 17),
                new TerrainRoom("mouth", 232 + (int)(r.Next() * 25), 86, 27, 20)
            };
            int count = 10 + (int)(r.Next() * 4);
            for (int attempt = 0; rooms.Count < count && attempt < 1000; attempt++)
            {
                int x = 23 + (int)(r.Next() * 271), y = 102 + (int)(r.Next() * 68);
                bool clear = true;
                foreach (var room in rooms)
                    if (Distance(x, y, room.X, room.Y) < 30 * 30) { clear = false; break; }
                if (clear) rooms.Add(new TerrainRoom("cavern-" + rooms.Count, x, y,
                    22 + (int)(r.Next() * 15), 13 + (int)(r.Next() * 11)));
            }
            if (rooms.Count != count) throw new InvalidOperationException("洞室布点预算耗尽。");
            return rooms;
        }

        private static List<CavePassage> Connect(List<TerrainRoom> rooms, TerrainRandom r)
        {
            var result = new List<CavePassage>();
            var joined = new HashSet<int> { 0 };
            while (joined.Count < rooms.Count)
            {
                int a = -1, b = -1; double best = double.MaxValue;
                for (int i = 0; i < rooms.Count; i++) if (joined.Contains(i))
                    for (int j = 0; j < rooms.Count; j++) if (!joined.Contains(j))
                    {
                        double distance = Distance(rooms[i].X, rooms[i].Y, rooms[j].X, rooms[j].Y);
                        if (distance < best) { a = i; b = j; best = distance; }
                    }
                Add(result, rooms, a, b, result.Count < 2 ? CavePassageKind.Open : Choose(r), r);
                joined.Add(b);
            }
            // 增加局部回环；不把所有死路连接成环。
            for (int k = 0; k < 3; k++)
            {
                int a = (int)(r.Next() * rooms.Count), b = -1; double best = double.MaxValue;
                for (int j = 0; j < rooms.Count; j++)
                {
                    if (j == a || result.Exists(e => e.From == a && e.To == j || e.From == j && e.To == a)) continue;
                    double distance = Distance(rooms[a].X, rooms[a].Y, rooms[j].X, rooms[j].Y);
                    if (distance < best) { b = j; best = distance; }
                }
                if (b >= 0) Add(result, rooms, a, b, k == 0 ? CavePassageKind.LooseFill : Choose(r), r);
            }
            return result;
        }

        private static CavePassageKind Choose(TerrainRandom r)
        {
            double n = r.Next();
            return n < .42 ? CavePassageKind.Open : n < .73 ? CavePassageKind.LooseFill :
                n < .92 ? CavePassageKind.ThinRock : CavePassageKind.DeepRock;
        }

        private static void Add(List<CavePassage> edges, List<TerrainRoom> rooms, int a, int b, CavePassageKind kind, TerrainRandom r)
        {
            int x = (rooms[a].X + rooms[b].X) / 2 + (int)(r.Next() * 9) - 4;
            int y = (rooms[a].Y + rooms[b].Y) / 2 + (int)(r.Next() * 11) - 5;
            int length = kind == CavePassageKind.Open ? 0 : kind == CavePassageKind.ThinRock ? 2 :
                kind == CavePassageKind.DeepRock ? 9 : 7;
            edges.Add(new CavePassage(a, b, kind, x, y, 3 + (int)(r.Next() * 2), length));
        }

        private static void CarveRoom(byte[] cells, TerrainRoom room, TerrainRandom r)
        {
            double phase = r.Next() * Math.PI * 2;
            for (int y = room.Top; y <= room.Top + room.Height; y++)
                for (int x = room.Left; x <= room.Left + room.Width; x++)
                {
                    double dx = (x - room.X) / (room.Width * .5), dy = (y - room.Y) / (room.Height * .5);
                    double angle = Math.Atan2(dy, dx);
                    double edge = .90 + .08 * Math.Sin(angle * 3 + phase) + .04 * Math.Cos(angle * 7 - phase);
                    if (dx * dx + dy * dy <= edge * edge) Clear(cells, x, y);
                }
        }

        private static void CarveLine(byte[] cells, int ax, int ay, int bx, int by, int radius)
        {
            int steps = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                double t = steps == 0 ? 0 : (double)i / steps;
                int cx = (int)Math.Round(ax + (bx - ax) * t), cy = (int)Math.Round(ay + (by - ay) * t);
                for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius) Clear(cells, cx + x, cy + y);
            }
        }

        private static void Cover(byte[] cells, bool[] soft, List<TerrainRoom> rooms, CavePassage edge)
        {
            if (edge.Kind == CavePassageKind.Open) return;
            var a = rooms[edge.From]; var b = rooms[edge.To];
            double dx = b.X - a.X, dy = b.Y - a.Y, distance = Math.Sqrt(dx * dx + dy * dy);
            dx /= distance; dy /= distance;
            int extent = edge.Radius + edge.CoverLength;
            for (int y = edge.BendY - extent; y <= edge.BendY + extent; y++)
                for (int x = edge.BendX - extent; x <= edge.BendX + extent; x++)
                {
                    if (x < 3 || x >= W - 3 || y < 3 || y >= H - 3) continue;
                    // 覆盖连接而非洞室核心；回环跨过第三个洞室时同样保留其观察空间。
                    if (rooms.Exists(room => Math.Abs(x - room.X) <= room.Width * .35 &&
                        Math.Abs(y - room.Y) <= room.Height * .35)) continue;
                    double u = (x - edge.BendX) * dx + (y - edge.BendY) * dy;
                    double v = -(x - edge.BendX) * dy + (y - edge.BendY) * dx;
                    if (Math.Abs(u) > edge.CoverLength * .5 || Math.Abs(v) > edge.Radius + 1) continue;
                    int i = y * W + x;
                    if (cells[i] != 0) continue;
                    cells[i] = edge.Kind == CavePassageKind.LooseFill ? (byte)1 : (byte)2;
                    soft[i] = edge.Kind == CavePassageKind.LooseFill;
                }
        }

        private static void Clear(byte[] cells, int x, int y)
        { if (x >= 3 && x < W - 3 && y >= 3 && y < H - 3) cells[y * W + x] = 0; }
        private static double Distance(int ax, int ay, int bx, int by) => (ax - bx) * (ax - bx) + (ay - by) * (ay - by);
    }
}
