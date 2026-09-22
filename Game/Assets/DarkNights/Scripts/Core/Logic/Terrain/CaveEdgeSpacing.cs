using System;
using System.Collections.Generic;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>确定性边缘锚点的空间查询；仅加速已经接受的锚点查询，不改变 H5 全局优先级与间距判定。</summary>
    internal sealed class CaveEdgeSpacing
    {
        private readonly Dictionary<int, List<(int X, int Y, double Radius)>> buckets = new Dictionary<int, List<(int, int, double)>>();
        public bool Overlaps(int x, int y, double spacing, int vertical, bool rounded)
        {
            for (int by = Math.Max(0, y - vertical) / 64; by <= (y + vertical) / 64; by++)
                for (int bx = Math.Max(0, x - 64) / 64; bx <= (x + 64) / 64; bx++)
                    if (buckets.TryGetValue(by * 64 + bx, out var list))
                        foreach (var a in list)
                            if (Math.Abs(a.Y - y) < vertical && Math.Abs(a.X - x) <
                                (rounded ? Math.Max(spacing, a.Radius * 1.15) : spacing)) return true;
            return false;
        }
        public void Add(int x, int y, double radius)
        {
            int key = y / 64 * 64 + x / 64;
            if (!buckets.TryGetValue(key, out var list)) buckets.Add(key, list = new List<(int, int, double)>());
            list.Add((x, y, radius));
        }
    }
}
