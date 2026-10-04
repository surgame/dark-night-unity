using System;
using AnyRules.Next;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>局部表现的完整区块门控；订阅换代期间旧提交不能证明新区域可读，边界填充只忽略世界外的格子。</summary>
    public static class MineralRegionReadiness
    {
        /// <summary>内缩订阅的非世界边缘；原生兴趣服务自带一格 halo，展开后恰好覆盖完整加载区块，避免额外的半知区块。</summary>
        public static GridBounds Subscription(GridBounds loaded, GridBounds world)
        {
            int left = loaded.MinU + (loaded.MinU == world.MinU ? 0 : 1);
            int bottom = loaded.MinV + (loaded.MinV == world.MinV ? 0 : 1);
            int right = (int)loaded.MaxUExclusive - (loaded.MaxUExclusive == world.MaxUExclusive ? 0 : 1);
            int top = (int)loaded.MaxVExclusive - (loaded.MaxVExclusive == world.MaxVExclusive ? 0 : 1);
            return new GridBounds(left, bottom, right - left, top - bottom);
        }
        public static bool Complete(IReadOnlyGrid grid, WorldDescriptor descriptor, GridBounds region)
        {
            if (grid == null || descriptor == null || !region.IsValid || region.Width > SessionMineralNetwork.RegionSide ||
                region.Height > SessionMineralNetwork.RegionSide) return false;
            int size = descriptor.ChunkSize;
            int left = Math.Max(descriptor.Bounds.MinU, GridMath.FloorDiv(region.MinU, size) * size);
            int bottom = Math.Max(descriptor.Bounds.MinV, GridMath.FloorDiv(region.MinV, size) * size);
            int right = (int)Math.Min(descriptor.Bounds.MaxUExclusive, (GridMath.FloorDiv((int)region.MaxUExclusive - 1, size) + 1) * size);
            int top = (int)Math.Min(descriptor.Bounds.MaxVExclusive, (GridMath.FloorDiv((int)region.MaxVExclusive - 1, size) + 1) * size);
            for (int v = bottom; v < top; v++) for (int u = left; u < right; u++)
                if (!grid.Read(new CellCoord(u, v)).TryGetCell(out _)) return false;
            return true;
        }
    }
}
