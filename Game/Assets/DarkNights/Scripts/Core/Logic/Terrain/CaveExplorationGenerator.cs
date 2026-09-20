using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>分层错位的天然洞厅生成；空间骨架、可变通道和塌方先于独立坡形，输出冻结候选，不拥有运行状态。</summary>
    public static class CaveExplorationGenerator
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;
        public static TerrainBlueprint Generate(TerrainGenerationSettings input)
        {
            var settings = input.CopyValidated();
            var random = new TerrainRandom(settings.Seed + ":cave-exploration-v2");
            var cells = new byte[W * H]; var protection = new bool[cells.Length]; var soft = new bool[cells.Length];
            int[] surface = Surface(settings);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                protection[i] = x < 3 || x >= W - 3 || y >= H - 3;
                cells[i] = protection[i] ? (byte)8 : y < surface[x] ? (byte)0 : y < surface[x] + 3 ? (byte)1 : (byte)2;
            }
            var rooms = Rooms(random); var passages = Connect(rooms, random);
            foreach (var room in rooms) CaveRoomCarving.Carve(cells, room, random);
            foreach (var edge in passages)
            {
                var a = rooms[edge.From]; var b = rooms[edge.To];
                CarveLine(cells, a.X, a.Y + 2, edge.BendX, edge.BendY, edge.Radius);
                CarveLine(cells, edge.BendX, edge.BendY, b.X, b.Y + 2, edge.Radius);
            }
            CarveLine(cells, rooms[0].X - 8, surface[rooms[0].X - 8] - 2, rooms[0].X, rooms[0].Y, 4);
            CarveLine(cells, rooms[3].X + 12, surface[rooms[3].X + 12] - 2, rooms[3].X, rooms[3].Y, 3);
            foreach (var edge in passages)
            {
                var a = rooms[edge.From]; var b = rooms[edge.To];
                CaveRoomCarving.Shelves(cells, a.X, a.Y, edge.BendX, edge.BendY, rooms);
                CaveRoomCarving.Shelves(cells, edge.BendX, edge.BendY, b.X, b.Y, rooms);
            }
            CaveRoomCarving.Shelves(cells, rooms[0].X - 8, surface[rooms[0].X - 8], rooms[0].X, rooms[0].Y, rooms);
            CaveRoomCarving.Shelves(cells, rooms[3].X + 12, surface[rooms[3].X + 12], rooms[3].X, rooms[3].Y, rooms);
            foreach (var edge in passages) Cover(cells, soft, rooms, edge);
            for (int x = 24; x < 48; x++) for (int y = surface[x]; y <= surface[x] + 2; y++)
            { int i = y * W + x; cells[i] = 1; protection[i] = true; soft[i] = false; }
            var deposits = new List<TerrainDepositBlueprint>();
            for (int n = 0; n < rooms.Count; n++)
            {
                var room = rooms[n]; int x = room.X + room.Width / 5;
                int floor = room.Y;
                while (floor < H - 5 && cells[floor * W + x] == 0) floor++;
                if (floor < H - 5)
                {
                    deposits.Add(new TerrainDepositBlueprint("cave-" + n, room.Kind, x, floor - 1, n > 7 ? "rare" : "common", 80));
                    for (int dx = -2; dx <= 2; dx++) for (int dy = 0; dy < 3; dy++)
                    {
                        int i = (floor + dy) * W + x + dx;
                        if (cells[i] != 0 && !protection[i] && random.Next() > .25) cells[i] = (byte)(n % 3 == 0 ? 6 : 4);
                    }
                }
            }
            byte[] shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            return new TerrainBlueprint(settings, cells, protection, surface, rooms.ToArray(), soft, deposits.ToArray(), passages.ToArray(), shapes);
        }
        private static int[] Surface(TerrainGenerationSettings s)
        {
            var result = new int[W]; uint seed = TerrainRandom.Hash(s.Seed + ":surface");
            for (int x = 0; x < W; x++)
            {
                int y = 32 + (int)((TerrainRandom.ValueNoise(seed, x / 65.0) * 13 + TerrainRandom.ValueNoise(seed + 1, x / 18.0) * 4) * s.Amplitude);
                result[x] = x == 0 ? y : Math.Clamp(y, result[x - 1] - 1, result[x - 1] + 1);
            }
            for (int x = 24; x < 48; x++) result[x] = result[24];
            return result;
        }
        private static List<TerrainRoom> Rooms(TerrainRandom r)
        {
            var rooms = new List<TerrainRoom>();
            string[] kinds = { "gallery", "shelf", "rift", "vault" };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 4; col++)
            {
                int x = 43 + col * 77 + (int)(r.Next() * 19) - 9;
                int y = 65 + row * 43 + (int)(r.Next() * 25) - 12;
                int kind = (col + row) % 4;
                rooms.Add(new TerrainRoom(kinds[kind], x, y, kind == 2 ? 28 : 38 + (int)(r.Next() * 13),
                    kind == 2 ? 29 : 18 + (int)(r.Next() * 8)));
            }
            return rooms;
        }
        private static List<CavePassage> Connect(List<TerrainRoom> rooms, TerrainRandom r)
        {
            var edges = new List<CavePassage>();
            var joined = new HashSet<int> { 0 };
            while (joined.Count < rooms.Count)
            {
                int a = -1, b = -1; double best = double.MaxValue;
                foreach (int i in joined) for (int j = 0; j < rooms.Count; j++)
                {
                    if (joined.Contains(j)) continue;
                    double dx = rooms[i].X - rooms[j].X, dy = rooms[i].Y - rooms[j].Y;
                    double cost = dx * dx + dy * dy * 1.8;
                    if (cost < best) { best = cost; a = i; b = j; }
                }
                Add(edges, rooms, a, b, CavePassageKind.Open, r); joined.Add(b);
            }
            for (int k = 0; k < 4; k++)
            {
                int a = (int)(r.Next() * rooms.Count), b = -1; double best = double.MaxValue;
                for (int j = 0; j < rooms.Count; j++)
                {
                    if (j == a || edges.Exists(e => e.From == a && e.To == j || e.To == a && e.From == j)) continue;
                    double dx = rooms[a].X - rooms[j].X, dy = rooms[a].Y - rooms[j].Y;
                    if (dx * dx + dy * dy < best) { best = dx * dx + dy * dy; b = j; }
                }
                if (b >= 0) Add(edges, rooms, a, b, k < 2 ? CavePassageKind.LooseFill : k == 2 ? CavePassageKind.ThinRock : CavePassageKind.DeepRock, r);
            }
            for (int i = edges.Count - 1; i > 0; i--)
            { int j = (int)(r.Next() * (i + 1)); var swap = edges[i]; edges[i] = edges[j]; edges[j] = swap; }
            return edges;
        }
        private static void Add(List<CavePassage> edges, List<TerrainRoom> rooms, int a, int b, CavePassageKind kind, TerrainRandom r)
        {
            var p = rooms[a]; var q = rooms[b];
            int x = (p.X + q.X) / 2 + (int)(r.Next() * 7) - 3;
            int y = (p.Y + q.Y) / 2 + 2 + (int)(r.Next() * 5) - 2;
            edges.Add(new CavePassage(a, b, kind, x, y, 3, kind == CavePassageKind.Open ? 0 : kind == CavePassageKind.ThinRock ? 2 : 6));
        }
        private static void CarveLine(byte[] cells, int ax, int ay, int bx, int by, int radius)
        {
            int steps = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                double t = steps == 0 ? 0 : (double)i / steps;
                int cx = (int)Math.Round(ax + (bx - ax) * t), cy = (int)Math.Round(ay + (by - ay) * t);
                double size = radius + .7 * Math.Sin(t * Math.PI);
                for (int y = -radius - 1; y <= radius + 1; y++) for (int x = -radius - 1; x <= radius + 1; x++)
                    if (x * x + y * y <= size * size) Clear(cells, cx + x, cy + y);
            }
        }
        private static void Cover(byte[] cells, bool[] soft, List<TerrainRoom> rooms, CavePassage edge)
        {
            if (edge.Kind == CavePassageKind.Open) return;
            for (int y = edge.BendY - edge.CoverLength; y <= edge.BendY + edge.CoverLength; y++)
                for (int x = edge.BendX - edge.Radius - 2; x <= edge.BendX + edge.Radius + 2; x++)
                {
                    if (x < 3 || x >= W - 3 || y < 3 || y >= H - 3) continue;
                    if (rooms.Exists(room => Math.Abs(x - room.X) < room.Width * .4 && Math.Abs(y - room.Y) < room.Height * .35)) continue;
                    int i = y * W + x;
                    if (cells[i] == 0) { cells[i] = edge.Kind == CavePassageKind.LooseFill ? (byte)1 : (byte)2; soft[i] = edge.Kind == CavePassageKind.LooseFill; }
                }
        }
        private static void Clear(byte[] cells, int x, int y)
        { if (x >= 3 && x < W - 3 && y >= 3 && y < H - 3) cells[y * W + x] = 0; }
    }
}
