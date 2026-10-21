using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>七类矿室和曲廊的确定性格子雕刻；双囊／分层腔保留岩柱与隔层，接口只表达雕刻意图，保护格永不写入。</summary>
    public static class CaveRuleGeometry
    {
        private const int W = TerrainGenerationSettings.Width, H = TerrainGenerationSettings.Height;

        public static void Room(byte[] cells, bool[] protection, TerrainRoom room, int variation = 0)
        {
            double phase = variation * .71;
            bool mirror = variation % 2 != 0;
            for (int y = room.Top; y <= room.Top + room.Height; y++)
                for (int x = room.Left; x <= room.Left + room.Width; x++)
                {
                    double u = (x - room.X) / (room.Width * .5), v = (y - room.Y) / (room.Height * .5);
                    if (mirror) u = -u;
                    double noise = .035 * Math.Sin(u * 19 + phase) * Math.Cos(v * 13 - phase);
                    bool inside;
                    switch (room.Kind)
                    {
                        case "low-seam": inside = Math.Abs(v - u * .25) < .42 * Math.Sqrt(Math.Max(0, 1 - u * u)) + noise; break;
                        case "high-arch": inside = u * u + Math.Pow((v + .15 - u * .25) / 1.04, 2) < 1 + noise && v < .6 + .12 * u; break;
                        case "fault-shelf": inside = u * u + v * v < 1 + noise && !(u > .02 && v > .18 + u * .35); break;
                        case "slant-rift": inside = Math.Pow((u - v * .4) / .58, 2) + v * v < 1 + noise; break;
                        case "twin-pocket":
                            inside = Ellipse(u + .45, v + .15, .55, .75) || Ellipse(u - .43, v - .12, .55, .8) || Math.Abs(u) < .3 && Math.Abs(v + .35) < .17;
                            break;
                        case "collapse-bowl": inside = u * u + Math.Pow((v - .15) / .85, 2) < 1 + noise && v > -.5 + Math.Abs(u) * .15; break;
                        case "layered-cavity":
                            inside = Ellipse(u + .12, v + .48, .85, .42) || Ellipse(u - .12, v - .48, .85, .42) || u > .65 && u < .85 && Math.Abs(v) < .65;
                            break;
                        default: throw new ArgumentException("未知矿室模板。");
                    }
                    if (inside) Set(cells, protection, x, y, 0);
                }
        }

        public static void Corridor(byte[] cells, bool[] protection, int ax, int ay, int bx, int by, int radius, double phase = 0, int cover = 0)
        {
            double length = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int steps = Math.Max(1, (int)Math.Ceiling(length * 2));
            for (int step = 0; step <= steps; step++)
            {
                double t = step / (double)steps;
                if (cover > 0 && Math.Abs(t - .5) < (cover + radius * 2 + 2) / Math.Max(1, length) * .5) continue;
                double wave = Math.Sin(t * Math.PI) * Math.Sin(t * 8 + phase) * Math.Min(4, length / 12);
                double x = ax + (bx - ax) * t - (by - ay) / Math.Max(1, length) * wave;
                double y = ay + (by - ay) * t + (bx - ax) / Math.Max(1, length) * wave;
                double size = Math.Max(1.5, radius + .6 * Math.Sin(t * 11 + phase));
                Disk(cells, protection, x, y, size, 0);
            }
        }

        public static void Disk(byte[] cells, bool[] protection, double cx, double cy, double radius, byte material)
        {
            for (int y = (int)(cy - radius); y <= cy + radius; y++)
                for (int x = (int)(cx - radius); x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) Set(cells, protection, x, y, material);
        }

        public static void Set(byte[] cells, bool[] protection, int x, int y, byte material)
        {
            if (x < 3 || x >= W - 3 || y < 3 || y >= H - 3 || protection[y * W + x]) return;
            cells[y * W + x] = material;
        }
        private static bool Ellipse(double u, double v, double rx, double ry) => u * u / (rx * rx) + v * v / (ry * ry) < 1;
    }
}
