using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>恢复前验证矿床与冻结初始蓝图一一对应；全部墓碑参与身份/容量核对，拒绝增删格、重叠和保护格，不重新生成矿床。</summary>
    internal static class MineralSnapshotValidator
    {
        internal static string Validate(SessionSnapshot snapshot)
        {
            var blueprints = snapshot.Terrain?.Deposits;
            if (blueprints == null) return snapshot.MineralDeposits.Count == 0 ? "" : "矿床缺少所属地图";
            if (snapshot.MineralDeposits.Count != blueprints.Count) return "矿床对象与蓝图数量不一致";
            var occupied = new HashSet<int>(); var protectedCells = snapshot.Terrain.CopyProtection();
            var placements = snapshot.Identities.ToDictionary(value => value.Id, value => value.PlacementKey);
            foreach (var mineral in snapshot.MineralDeposits)
            {
                if (mineral.RoomKind == null || mineral.RoomKind.Length > 32 || mineral.Rarity == null || mineral.Rarity.Length > 16 ||
                    !MineralCellValidation.Validate(mineral.Cells, 1000000, occupied)) return "矿床格状态无效";
                if (!placements.TryGetValue(mineral.Id, out var placement)) return "矿床缺少对象身份";
                var blueprint = blueprints.SingleOrDefault(value => placement == "terrain.deposit." + value.Id);
                if (blueprint == null || mineral.RoomKind != blueprint.RoomKind || mineral.Rarity != blueprint.Rarity ||
                    Math.Abs(mineral.X - (blueprint.X + .5) * PlayableTerrain.CellPixels) > .001 || mineral.Y != blueprint.Y ||
                    mineral.Cells.Count != blueprint.Cells.Count) return "矿床与初始蓝图不匹配";
                foreach (var cell in mineral.Cells)
                {
                    var initial = blueprint.Cells.SingleOrDefault(value => value.U == cell.U && value.V == cell.V);
                    if (initial.Capacity != cell.Capacity || protectedCells[-cell.V * TerrainGenerationSettings.Width + cell.U])
                        return "矿格分配与初始蓝图不匹配";
                }
            }
            return "";
        }
    }
}
