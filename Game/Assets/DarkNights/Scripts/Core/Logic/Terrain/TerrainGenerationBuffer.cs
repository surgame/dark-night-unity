using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>单次纯算法调用的临时工作数组；生成后丢弃，不能作为游戏运行世界或客户端副本。</summary>
    internal sealed class TerrainGenerationBuffer
    {
        internal const int W = TerrainGenerationSettings.Width;
        internal const int H = TerrainGenerationSettings.Height;
        internal readonly byte[] Cells = new byte[W * H];
        internal readonly bool[] Protected = new bool[W * H];
        internal readonly int[] Surface = new int[W];
        internal readonly List<TerrainRoom> Rooms = new List<TerrainRoom>();
        internal readonly int[] PadX = { 62, 179, 272 };
        internal readonly int[] PadY = new int[3];
        internal readonly int[] PadRadius = { 26, 22, 22 };
        internal int At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? -1 : Cells[y * W + x];
        internal void Set(int x, int y, byte t)
        {
            if (x >= 1 && x < W - 1 && y >= 2 && y < H - 3) Cells[y * W + x] = t;
        }
        internal void Disk(double cx, double cy, double rx, double ry)
        {
            for (int y = (int)Math.Floor(cy - ry); y <= cy + ry; y++)
                for (int x = (int)Math.Floor(cx - rx); x <= cx + rx; x++)
                    if (Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2) <= 1) Set(x, y, 0);
        }
        internal void Corridor(double ax, double ay, double bx, double by, double radius, bool winding = true)
        {
            double dx = bx - ax, dy = by - ay;
            int steps = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) * 2);
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)Math.Max(1, steps), wave = winding ? Math.Sin(t * Math.PI * 2) * 1.6 : 0;
                Disk(ax + dx * t + (Math.Abs(dy) > Math.Abs(dx) ? wave : 0),
                    ay + dy * t + (Math.Abs(dx) >= Math.Abs(dy) ? wave : 0), radius, radius);
            }
        }
    }
}
