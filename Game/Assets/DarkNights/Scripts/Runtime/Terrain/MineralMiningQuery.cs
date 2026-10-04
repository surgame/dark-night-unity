using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>前景净空中最近矿格的有界查询；射线包围盒最多覆盖工具可达区域，Unknown 阻止选取，不扫描全世界矿床。</summary>
    public static class MineralMiningQuery
    {
        public static bool First(IReadOnlyGrid minerals, IReadOnlyGrid foreground, float x, float height,
            float dx, float dh, float reach, out CellCoord target, out float distance)
        {
            target = default; distance = reach + .001f;
            if (minerals == null || foreground == null || !minerals.World.Equals(foreground.World)) return false;
            if (TerrainMiningQuery.FirstSurface(foreground, x, height, dx, dh, reach, out _, out float wall)) distance = wall;
            int minU = TerrainMiningGeometry.CellU(Math.Min(x, x + dx * reach) - 1);
            int maxU = TerrainMiningGeometry.CellU(Math.Max(x, x + dx * reach) + 1);
            int minV = TerrainMiningGeometry.CellV(Math.Min(height, height + dh * reach) - 1);
            int maxV = TerrainMiningGeometry.CellV(Math.Max(height, height + dh * reach) + 1);
            bool found = false;
            for (int v = minV; v <= maxV; v++) for (int u = minU; u <= maxU; u++)
            {
                var cell = new CellCoord(u, v);
                if (u < 0 || u >= TerrainGenerationSettings.Width || v > 0 || v <= -TerrainGenerationSettings.Height || !minerals.Read(cell).TryGetCell(out var ore) || ore.IsEmpty ||
                    !foreground.Read(cell).TryGetCell(out var cover) || !cover.IsEmpty ||
                    !TerrainMiningGeometry.RayCell(x, height, dx, dh, reach, u, v, TerrainCellShape.Full, out float near) || near >= distance) continue;
                found = true; target = cell; distance = near;
            }
            return found;
        }
    }
}
