using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>关卡矿洞扩展的纯格子雕刻；只写本次生成缓冲，以实际支撑和净空构造洞庭、短坡及竖井。</summary>
    internal sealed class MineMapCarving
    {
        private const int W = TerrainGenerationSettings.Width;
        private readonly MineMapDefinition definition;
        private readonly byte[] cells;
        private readonly bool[] soft;

        internal MineMapCarving(MineMapDefinition definition, byte[] cells, bool[] soft)
        {
            this.definition = definition; this.cells = cells; this.soft = soft;
        }

        internal void Carve()
        {
            foreach (var room in definition.Rooms.Where(r => !r.Base)) Room(room);
            foreach (var path in definition.Paths) Walk(path.Points);
            foreach (var shaft in definition.Shafts)
            {
                var lower = definition.Rooms.Single(r => r.Id == shaft.Lower);
                var upper = definition.Rooms.Single(r => r.Id == shaft.Upper);
                int exit = shaft.X + shaft.Side * 2;
                Walk(new[] { lower.X, lower.Floor, shaft.X, shaft.Bottom });
                Walk(new[] { exit, shaft.Top, upper.X, upper.Floor });
                for (int x = shaft.X - 1; x <= shaft.X + 1; x++)
                {
                    Clear(x, shaft.Top - 2, shaft.Bottom);
                    Solid(x, shaft.Bottom); Solid(x, shaft.Bottom + 1);
                }
                for (int x = exit; x != exit + shaft.Side * 3; x += shaft.Side)
                {
                    Clear(x, shaft.Top - 4, shaft.Top);
                    Solid(x, shaft.Top); Solid(x, shaft.Top + 1);
                }
            }
            foreach (var path in definition.Paths.Where(p => p.SealX >= 0)) Seal(path);
        }

        private void Room(MineMapRoom room)
        {
            for (int x = room.X - room.Radius; x <= room.X + room.Radius; x++)
            {
                double t = Math.Abs(x - room.X) / (double)(room.Radius + 1);
                int floor = room.Floor + (Math.Abs(x - room.X) <= 2 ? 0 : (int)Math.Round(t * t));
                int height = Math.Max(4, (int)Math.Round(3 + room.Height * Math.Sqrt(1 - t * t)));
                Clear(x, floor - height, floor);
            }
        }

        private void Walk(int[] points)
        {
            for (int n = 2; n < points.Length; n += 2)
            {
                int ax = points[n - 2], ay = points[n - 1], bx = points[n], by = points[n + 1];
                int width = Math.Abs(bx - ax);
                if (width == 0 && ay == by) continue;
                if (width == 0 || Math.Abs(by - ay) > width * 2)
                    throw new InvalidOperationException("矿洞接道必须横向延伸且每列落差不超过两格。");
                for (int step = 0; step <= width; step++)
                {
                    int x = ax + Math.Sign(bx - ax) * step;
                    int floor = (int)Math.Round(ay + (by - ay) * step / (double)width);
                    Clear(x, floor - 6, floor);
                }
            }
        }

        private void Seal(MineMapPath path)
        {
            for (int n = 2; n < path.Points.Length; n += 2)
            {
                int ax = path.Points[n - 2], bx = path.Points[n];
                if (path.SealX < Math.Min(ax, bx) || path.SealX > Math.Max(ax, bx)) continue;
                int floor = (int)Math.Round(path.Points[n - 1] + (path.Points[n + 1] - path.Points[n - 1]) *
                    (path.SealX - ax) / (double)(bx - ax));
                for (int x = path.SealX; x < path.SealX + 2; x++)
                    for (int y = floor - 8; y < floor; y++)
                    { Solid(x, y); soft[Index(x, y)] = true; }
                return;
            }
            throw new InvalidOperationException("封岩列不在所声明的通路上：" + path.Id);
        }

        private int Index(int x, int y)
        {
            if (x < 1 || x >= definition.Width - 1 || y < 1 || y >= definition.Height - 1)
                throw new InvalidOperationException("矿洞雕刻越界：" + x + "," + y);
            return (y + definition.OffsetY) * W + x + definition.OffsetX;
        }

        private void Clear(int x, int top, int bottom)
        {
            for (int y = Math.Max(1, top); y < bottom; y++)
            { int i = Index(x, y); cells[i] = 0; soft[i] = false; }
        }

        private void Solid(int x, int y)
        {
            int i = Index(x, y); cells[i] = 2; soft[i] = false;
        }
    }
}
