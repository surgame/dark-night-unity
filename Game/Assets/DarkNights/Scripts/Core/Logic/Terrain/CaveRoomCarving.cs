using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>按洞厅用途生成可变顶高与岩棚地面；大形先于像素边缘，中心落脚空间和通路接点始终保留。</summary>
    internal static class CaveRoomCarving
    {
        internal static void Shelves(byte[] cells, int ax, int ay, int bx, int by, List<TerrainRoom> rooms)
        {
            int distance = Math.Abs(by - ay);
            if (distance < 9 || Math.Abs(bx - ax) > distance * 1.5) return;
            const int w = TerrainGenerationSettings.Width;
            for (int step = 5; step < distance - 3; step += 5)
            {
                float t = step / (float)distance;
                int y = (int)Math.Round(ay + (by - ay) * t), x = (int)Math.Round(ax + (bx - ax) * t);
                if (rooms.Exists(room => Math.Abs(y - room.Y) <= 3 && Math.Abs(x - room.X) < room.Width / 2)) continue;
                if (cells[y * w + x] != 0) continue;
                int side = (step / 5) % 2 == 0 ? 1 : -1, wall = x;
                while (Math.Abs(wall - x) < 12 && wall > 4 && wall < w - 5 && cells[y * w + wall] == 0) wall += side;
                if (Math.Abs(wall - x) >= 12) continue;
                for (int n = 1; n <= 2; n++)
                {
                    int u = wall - side * n;
                    if (Math.Abs(u - x) < 1) break;
                    cells[y * w + u] = 2;
                }
            }
        }
        internal static void Carve(byte[] cells, TerrainRoom room, TerrainRandom random)
        {
            double phase = random.Next() * 6.28;
            int w = TerrainGenerationSettings.Width, h = TerrainGenerationSettings.Height;
            for (int x = room.Left; x <= room.Left + room.Width; x++)
            {
                double t = (x - room.Left) / (double)room.Width;
                double arch = Math.Pow(Math.Max(0, Math.Sin(t * Math.PI)), .42);
                double roof = room.Y - room.Height * .62 * arch + Math.Sin(t * 14 + phase) * 1.5;
                double floor = room.Y + room.Height * .40 * arch + Math.Sin(t * 7 + phase) * 2;
                if (room.Kind == "shelf") floor -= Math.Max(0, t - .52) * 11;
                if (room.Kind == "gallery") floor += (t - .5) * 6;
                for (int y = (int)Math.Ceiling(roof); y <= (int)Math.Floor(floor); y++)
                    if (x >= 3 && x < w - 3 && y >= 3 && y < h - 3) cells[y * w + x] = 0;
                // 侧壁上局部悬挑，避免横贯整间的人工楼板。
                if (room.Kind == "vault" && t > .12 && t < .28)
                    for (int y = room.Y + 2; y <= room.Y + 3; y++) cells[y * w + x] = 2;
            }
            cells[room.Y * w + room.X] = 0;
        }
    }
}
