using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>显式矿层联机夹具的初始出生点选择；只读同一正式最终地图，寻找有真实支撑、净空和矿格射线的位置，不改地形或运行状态。</summary>
    internal static class MineralQuickTestSpawn
    {
        internal static void Find(ObjectSession world, MiningToolRules tool, out float x, out float height, out float aim)
        {
            var map = world.Terrain.Map;
            foreach (var deposit in world.Terrain.Deposits.OrderBy(value => value.Id))
                foreach (var cell in deposit.Cells)
                {
                    var target = new CellCoord(cell.U, cell.V);
                    if (!map.Read(target).TryGetCell(out var foreground) || !foreground.IsEmpty) continue;
                    float center = TerrainMiningGeometry.CenterX(cell.U), targetHeight = TerrainMiningGeometry.CenterHeight(cell.V);
                    for (int offset = -48; offset <= 48; offset += 8)
                    {
                        float candidate = center + offset;
                        if (!TerrainBodyCollision.Ground(map, candidate, targetHeight + 32, targetHeight - 64,
                            HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight, out float ground)) continue;
                        if (!TerrainMiningGeometry.Direction(center - candidate, targetHeight - ground - tool.HandHeight,
                            out float dx, out float dh) || !TerrainMiningGeometry.RayCell(candidate, ground + tool.HandHeight,
                            dx, dh, tool.Reach, cell.U, cell.V, Core.Config.Terrain.TerrainCellShape.Full, out float distance)) continue;
                        if (TerrainMiningQuery.FirstSurface(map, candidate, ground + tool.HandHeight, dx, dh, tool.Reach,
                            out _, out float wall) && wall <= distance) continue;
                        x = candidate; height = ground; aim = (float)(Math.Atan2(dh, dx) * 180 / Math.PI); return;
                    }
                }
            throw new InvalidOperationException("当前正式地图没有同时满足净空、支撑与矿格射线的测试出生点。");
        }
    }
}
