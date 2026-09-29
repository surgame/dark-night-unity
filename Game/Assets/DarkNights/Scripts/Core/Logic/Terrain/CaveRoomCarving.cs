using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>按洞厅用途生成可变顶高与岩棚地面；大形先于像素边缘，中心落脚空间和通路接点始终保留。</summary>
    internal static class CaveRoomCarving
    {
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
