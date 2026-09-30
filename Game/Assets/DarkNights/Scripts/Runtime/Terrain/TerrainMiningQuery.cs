using System;
using AnyRules.Next;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>权威和冻结地图副本共用的采矿查询；只检查材料、距离和真实坡形遮挡，不写格子或库存。</summary>
    public static class TerrainMiningQuery
    {
        public static byte Material(TileCatalog tiles, uint tileId)
        {
            if (tiles == null || !tiles.TryGet(tileId, out var definition)) return 0;
            switch (definition.Key.ToString())
            {
                case "loam": return 1; case "slate": return 2; case "basalt": return 3;
                case "copper": return 4; case "iron": return 5; case "gold": return 6;
                case "moss": return 7; case "bedrock": return 8; default: return 0;
            }
        }

        public static bool CanMine(IReadOnlyGrid map, TileCatalog tiles, CellCoord target)
            => BlockReason(map, tiles, target).Length == 0;

        /// <summary>解释前景格的手采限制；空格中的独立矿床由调用方另行查询，不改变破坏规则。</summary>
        public static string BlockReason(IReadOnlyGrid map, TileCatalog tiles, CellCoord target)
        {
            if (map == null || !map.Read(target).TryGetCell(out var cell)) return "地图尚未就绪";
            if (cell.IsEmpty) return "这里没有可采集的矿床";
            byte material = Material(tiles, cell.TileId);
            if ((cell.Flags & 1) != 0) return "保护区域不能采集";
            if (material == 8) return "基岩不能破坏";
            if (TerrainDestructionPolicy.CanDestroy(Core.Config.Terrain.TerrainEditAction.HandMine,
                material, false, (cell.Flags & TerrainMiningGeometry.SoftRockFlag) != 0)) return "";
            return material == 0 ? "这种材料不能手采" : "硬岩不能手采，请寻找软岩或矿床";
        }

        public static bool Reachable(IReadOnlyGrid map, float x, float height, CellCoord target, float reach)
        {
            if (map == null || !TerrainMiningGeometry.WithinReach(x, height, target.U, target.V, reach)) return false;
            float deltaX = TerrainMiningGeometry.CenterX(target.U) - x;
            float deltaHeight = TerrainMiningGeometry.CenterHeight(target.V) - height;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(deltaX * deltaX + deltaHeight * deltaHeight) / 2));
            for (int step = 0; step <= steps; step++)
            {
                float sampleX = x + deltaX * step / steps, sampleHeight = height + deltaHeight * step / steps;
                if (TerrainMiningGeometry.CellU(sampleX) == target.U && TerrainMiningGeometry.CellV(sampleHeight) == target.V) continue;
                if (TerrainHeroMotion.Solid(map, sampleX, sampleHeight)) return false;
            }
            return true;
        }
    }
}
