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
            var random = new TerrainRandom(settings.Seed + ":cave-exploration-v4");
            var cells = new byte[W * H]; var protection = new bool[cells.Length]; var soft = new bool[cells.Length];
            int[] surface = Surface(settings);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                protection[i] = x < 3 || x >= W - 3 || y >= H - 3;
                cells[i] = protection[i] ? (byte)8 : y < surface[x] ? (byte)0 : y < surface[x] + 3 ? (byte)1 : (byte)2;
            }
            var rooms = Rooms(random, settings); var passages = Connect(rooms, random, settings.CavePassageRadius);
            foreach (var room in rooms) CaveRoomCarving.Carve(cells, room, random);
            foreach (var edge in passages) CarvePassage(cells, rooms, edge);
            int entranceRadius = settings.CavePassageRadius;
            CarveLine(cells, rooms[0].X - 8, surface[rooms[0].X - 8] - 2, rooms[0].X, rooms[0].Y, entranceRadius);
            CarveLine(cells, rooms[3].X + 12, surface[rooms[3].X + 12] - 2, rooms[3].X, rooms[3].Y, entranceRadius);
            foreach (var edge in passages) DecoratePassage(cells, rooms, edge);
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
                    // 只有独立矿床产出矿物。部分锚点藏在普通岩体后，前景挖开后才可采。
                    deposits.Add(new TerrainDepositBlueprint("cave-" + n, room.Kind, x,
                        n % 3 == 1 ? floor : floor - 1, n > 7 ? "rare" : "common", 80));
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
        private static List<TerrainRoom> Rooms(TerrainRandom r, TerrainGenerationSettings settings)
        {
            var rooms = new List<TerrainRoom>();
            string[] kinds = { "gallery", "shelf", "rift", "vault" };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 4; col++)
            {
                int x = 74 + col * settings.CaveColumnSpacing + (int)(r.Next() * 7) - 3;
                int y = 60 + row * settings.CaveRowSpacing + (int)(r.Next() * 7) - 3;
                int kind = (col + row) % 4;
                int baseWidth = kind == 2 ? 16 : kind == 3 ? 32 : 22 + (int)(r.Next() * 9);
                int baseHeight = kind == 2 ? 22 : kind == 3 ? 16 : 10 + (int)(r.Next() * 6);
                int width = Math.Max(12, (int)Math.Round(baseWidth * settings.CaveRoomWidthScale));
                int height = Math.Max(6, (int)Math.Round(baseHeight * settings.CaveRoomHeightScale));
                rooms.Add(new TerrainRoom(kinds[kind], x, y, width, height));
            }
            return rooms;
        }
        private static List<CavePassage> Connect(List<TerrainRoom> rooms, TerrainRandom r, int radius)
        {
            var edges = new List<CavePassage>();
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                Add(edges, rooms, row * 4 + col, row * 4 + col + 1, CavePassageKind.Open, r, radius);
            for (int layer = 0; layer < 2; layer++)
            {
                int first = (int)(r.Next() * 4);
                int second = (first + 1 + (int)(r.Next() * 3)) % 4;
                Add(edges, rooms, layer * 4 + first, (layer + 1) * 4 + first, CavePassageKind.Open, r, radius);
                Add(edges, rooms, layer * 4 + second, (layer + 1) * 4 + second, CavePassageKind.Open, r, radius);
            }
            for (int k = 0; k < 3; k++)
            {
                int a = -1, b = -1;
                for (int attempt = 0; attempt < 32 && b < 0; attempt++)
                {
                    int candidateA = (int)(r.Next() * rooms.Count), candidateB = (int)(r.Next() * rooms.Count);
                    if (candidateA == candidateB || Connected(edges, candidateA, candidateB)) continue;
                    int rowDistance = Math.Abs(candidateA / 4 - candidateB / 4);
                    int columnDistance = Math.Abs(candidateA % 4 - candidateB % 4);
                    if (rowDistance + columnDistance > 2) continue;
                    a = candidateA; b = candidateB;
                }
                if (b >= 0) Add(edges, rooms, a, b,
                    k == 0 ? CavePassageKind.LooseFill : k == 1 ? CavePassageKind.ThinRock : CavePassageKind.DeepRock, r, radius);
            }
            for (int i = edges.Count - 1; i > 0; i--)
            { int j = (int)(r.Next() * (i + 1)); var swap = edges[i]; edges[i] = edges[j]; edges[j] = swap; }
            return edges;
        }
        private static bool Connected(List<CavePassage> edges, int a, int b) =>
            edges.Exists(e => e.From == a && e.To == b || e.From == b && e.To == a);
        private static void Add(List<CavePassage> edges, List<TerrainRoom> rooms, int a, int b,
            CavePassageKind kind, TerrainRandom r, int radius)
        {
            var p = rooms[a]; var q = rooms[b];
            int x = Math.Abs(p.X - q.X) >= Math.Abs(p.Y - q.Y) ?
                (p.X + q.X) / 2 + (int)(r.Next() * 5) - 2 : p.X + (int)(r.Next() * 5) - 2;
            int y = Math.Abs(p.X - q.X) >= Math.Abs(p.Y - q.Y) ?
                p.Y + 2 + (int)(r.Next() * 3) - 1 : (p.Y + q.Y) / 2 + 2 + (int)(r.Next() * 5) - 2;
            int cover = kind == CavePassageKind.Open ? 0 : kind == CavePassageKind.ThinRock ? 2 : 5;
            edges.Add(new CavePassage(a, b, kind, x, y, radius, cover));
        }
        private static void CarvePassage(byte[] cells, List<TerrainRoom> rooms, CavePassage edge)
        {
            var a = rooms[edge.From]; var b = rooms[edge.To];
            int ay = a.Y + 2, by = b.Y + 2;
            if (Math.Abs(a.X - b.X) >= Math.Abs(ay - by))
            {
                CarveLine(cells, a.X, ay, edge.BendX, ay, edge.Radius);
                CarveLine(cells, edge.BendX, ay, edge.BendX, by, edge.Radius);
                CarveLine(cells, edge.BendX, by, b.X, by, edge.Radius);
            }
            else
            {
                CarveLine(cells, a.X, ay, a.X, edge.BendY, edge.Radius);
                CarveLine(cells, a.X, edge.BendY, b.X, edge.BendY, edge.Radius);
                CarveLine(cells, b.X, edge.BendY, b.X, by, edge.Radius);
            }
        }
        private static void DecoratePassage(byte[] cells, List<TerrainRoom> rooms, CavePassage edge)
        {
            var a = rooms[edge.From]; var b = rooms[edge.To];
            CaveRoomCarving.Shelves(cells, a.X, a.Y, edge.BendX, edge.BendY, rooms);
            CaveRoomCarving.Shelves(cells, edge.BendX, edge.BendY, b.X, b.Y, rooms);
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
