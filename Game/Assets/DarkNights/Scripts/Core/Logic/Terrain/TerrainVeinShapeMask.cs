using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 有厚度的近壁背景矿形配方；按短主脉、团块或错层带组合，不沿巢腔轮廓描边。
    /// 只计算有限局部占格，输入变化编号控制微小边缘变化，不读取业务RNG或运行世界。
    /// </summary>
    public static class TerrainVeinShapeMask
    {
        public static bool Contains(TerrainVeinShape shape, int x, int y, int variation)
        {
            if (y == 8 && x >= 3 && x <= 6) return true;
            double px = x - 4, py = y - 8;
            double offset = (variation % 5 - 2) * .18;
            switch (shape)
            {
                case TerrainVeinShape.ThickMass:
                    return Ellipse(px, py, 5.1, -1.8 + offset, 5, 3.5) ||
                        Ellipse(px, py, 1.5, -.2, 2.8, 1.8);
                case TerrainVeinShape.Branching:
                    return Segment(px, py, -.5, 0, 5.2, -2, 1.6) ||
                        Segment(px, py, 4.2, -1.7, 8, -5 + offset, 1.65) ||
                        Segment(px, py, 4.5, -1.5, 9, 1.8, 1.5);
                case TerrainVeinShape.AdjacentLobes:
                    return Ellipse(px, py, 1, -.4, 2.8, 2) ||
                        Ellipse(px, py, 5, -3 + offset, 3.1, 2.5) ||
                        Ellipse(px, py, 8.6, -.1, 2.8, 2.1) ||
                        Segment(px, py, 2, -1, 8, -1, .8);
                case TerrainVeinShape.LayeredBands:
                    return Segment(px, py, -.5, 0, 5, -3, 1.65) ||
                        Segment(px, py, 3.8, -3, 9, -4 + offset, 1.5) ||
                        Segment(px, py, 4, 1.6, 10, -.2, 1.5) ||
                        Segment(px, py, 4, -2, 4.5, 1.5, 1);
                default: throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }

        private static bool Ellipse(double x, double y, double cx, double cy, double rx, double ry)
        {
            double dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1;
        }

        private static bool Segment(double x, double y, double ax, double ay, double bx, double by, double radius)
        {
            double dx = bx - ax, dy = by - ay;
            double t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
            double ex = x - ax - t * dx, ey = y - ay - t * dy;
            return ex * ex + ey * ey <= radius * radius;
        }
    }
}
