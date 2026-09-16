using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>设计洞室、连接和可选细胞洞穴的生成步骤；独立随机流确保矿物设置不移动洞室。</summary>
    internal static class TerrainCaveGenerator
    {
        internal static void Carve(TerrainGenerationBuffer b, TerrainGenerationSettings settings)
        {
            var random = new TerrainRandom(settings.Seed + ":rooms");
            string[] kinds = { "entry", "mine", "roots", "gallery", "forge", "boss", "secret", "relic" };
            int[,] centers = { {97,90}, {127,119}, {65,131}, {189,104}, {195,144}, {242,168}, {140,166}, {278,125} };
            for (int id = 0; id < kinds.Length; id++)
            {
                int x = centers[id, 0] + (int)(random.Next() * 7) - 3;
                int y = centers[id, 1] + (int)(random.Next() * 5) - 2;
                string[] template = TerrainRoomTemplates.Get(id);
                var room = new TerrainRoom(kinds[id], x, y, template[0].Length * 2, template.Length * 2);
                b.Rooms.Add(room);
                if (room.Kind == "boss")
                {
                    for (int xx = room.Left - 1; xx <= room.Left + room.Width; xx++)
                    {
                        b.Set(xx, room.Top - 1, 8); b.Set(xx, room.Top + room.Height, 8);
                    }
                    for (int yy = room.Top; yy < room.Top + room.Height; yy++)
                    {
                        b.Set(room.Left - 1, yy, 8); b.Set(room.Left + room.Width, yy, 8);
                    }
                }
                for (int ty = 0; ty < template.Length; ty++)
                    for (int tx = 0; tx < template[ty].Length; tx++)
                        if (template[ty][tx] == '.')
                            for (int a = 0; a < 2; a++) for (int c = 0; c < 2; c++)
                                b.Set(room.Left + tx * 2 + a, room.Top + ty * 2 + c, 0);
                b.Disk(x, y, 3, 3);
            }
            int[,] edges = { {0,1}, {0,2}, {1,3}, {3,4}, {4,5}, {1,6}, {3,7} };
            for (int i = 0; i < 7; i++)
            {
                var a = b.Rooms[edges[i, 0]]; var c = b.Rooms[edges[i, 1]];
                int bx = TerrainRandom.Round(TerrainRandom.Lerp(a.X, c.X, .55));
                int by = a.Y + TerrainRandom.Round((c.Y - a.Y) * .22);
                b.Corridor(a.X, a.Y, bx, by, 2.5); b.Corridor(bx, by, c.X, c.Y, 2.5);
            }
            b.Corridor(94, b.Surface[94] - 2, 94, b.Rooms[0].Y, 3, false);
            b.Corridor(94, b.Rooms[0].Y, b.Rooms[0].X, b.Rooms[0].Y, 3, false);
            b.Corridor(292, b.Surface[292] - 2, 291, b.Rooms[7].Y, 2.8, false);
            b.Corridor(291, b.Rooms[7].Y, b.Rooms[7].X, b.Rooms[7].Y, 2.8, false);
            if (settings.OrganicCaves) CarveOrganic(b, settings.Seed);
            var secret = b.Rooms[6];
            for (int y = secret.Top - 2; y <= secret.Top + 2; y++)
                for (int x = secret.X - 4; x <= secret.X + 4; x++) if (b.At(x, y) == 0) b.Set(x, y, 2);
        }

        private static void CarveOrganic(TerrainGenerationBuffer b, string seed)
        {
            const int w = TerrainGenerationBuffer.W, h = TerrainGenerationBuffer.H;
            var a = new byte[w * h]; var random = new TerrainRandom(seed + ":cavities");
            for (int y = 84; y < h - 7; y++) for (int x = 8; x < w - 8; x++) a[y * w + x] = (byte)(random.Next() < .46 ? 1 : 0);
            for (int pass = 0; pass < 4; pass++)
            {
                var next = (byte[])a.Clone();
                for (int y = 86; y < h - 8; y++) for (int x = 9; x < w - 9; x++)
                {
                    int n = 0;
                    for (int yy = -1; yy <= 1; yy++) for (int xx = -1; xx <= 1; xx++)
                        if (xx != 0 || yy != 0) n += a[(y + yy) * w + x + xx];
                    next[y * w + x] = (byte)(n > 4 ? 1 : n < 4 ? 0 : a[y * w + x]);
                }
                a = next;
            }
            for (int y = 86; y < h - 7; y++) for (int x = 8; x < w - 8; x++)
            {
                if (a[y * w + x] == 0 || y <= b.Surface[x] + 9 || b.At(x, y) == 8) continue;
                bool nearRoom = false;
                foreach (var q in b.Rooms)
                    if (x >= q.Left - 2 && x <= q.Left + q.Width + 2 && y >= q.Top - 2 && y <= q.Top + q.Height + 2) nearRoom = true;
                if (!nearRoom) b.Set(x, y, 0);
            }
        }
    }
}
