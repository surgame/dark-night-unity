using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>比较完整新旧表现快照并计算真正失效页；包含全局锚点重排及局部岩粒变化，再扩展材质距离场半径。</summary>
    public static class CaveMaskChanges
    {
        public static bool[] DirtyPages(CaveMaskField previous, CaveMaskField next, int pageSize = 256, int reach = CaveRockBaker.DistanceCap,
            Action checkpoint = null)
        {
            if (next == null || pageSize < 1 || reach < 0) throw new ArgumentException("轮廓差异参数无效。");
            int columns = (next.Width + pageSize - 1) / pageSize, rows = (next.Height + pageSize - 1) / pageSize;
            var dirty = new bool[columns * rows];
            if (previous == null || previous.Width != next.Width || previous.Height != next.Height)
            { for (int i = 0; i < dirty.Length; i++) dirty[i] = true; return dirty; }
            for (int y = 0; y < next.Height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < next.Width; x++)
                {
                    if (next.SamePixel(previous, y * next.Width + x)) continue;
                    for (int py = Math.Max(0, y - reach) / pageSize; py <= Math.Min(rows - 1, (y + reach) / pageSize); py++)
                        for (int px = Math.Max(0, x - reach) / pageSize; px <= Math.Min(columns - 1, (x + reach) / pageSize); px++)
                            dirty[py * columns + px] = true;
                }
            }
            return dirty;
        }
    }
}
