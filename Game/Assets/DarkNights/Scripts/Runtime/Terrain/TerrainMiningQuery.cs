using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
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

        /// <summary>寻找瞄准直线中最近的真实岩壁表面；Unknown 与地图外区域作为阻挡，不跳过基岩或不可采材质。</summary>
        public static bool FirstSurface(IReadOnlyGrid map, float x, float height, float directionX,
            float directionHeight, float reach, out CellCoord target, out float distance)
        {
            target = default; distance = float.PositiveInfinity;
            if (map == null || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(height) || float.IsInfinity(height) ||
                float.IsNaN(reach) || reach <= 0 || reach > 144 ||
                !TerrainMiningGeometry.Direction(directionX, directionHeight, out float dx, out float dh)) return false;
            float endX = x + dx * reach, endHeight = height + dh * reach;
            int minU = TerrainMiningGeometry.CellU(Math.Min(x, endX)) - 1;
            int maxU = TerrainMiningGeometry.CellU(Math.Max(x, endX)) + 1;
            int minV = TerrainMiningGeometry.CellV(Math.Min(height, endHeight)) - 1;
            int maxV = TerrainMiningGeometry.CellV(Math.Max(height, endHeight)) + 1;
            for (int v = minV; v <= maxV; v++) for (int u = minU; u <= maxU; u++)
            {
                var position = new CellCoord(u, v);
                var sample = map.Read(position);
                TerrainCellShape shape = TerrainCellShape.Full;
                if (sample.TryGetCell(out var cell))
                {
                    if (cell.IsEmpty) continue;
                    shape = TerrainShapeGeometry.Decode(cell.Flags);
                }
                if (!TerrainMiningGeometry.RayCell(x, height, dx, dh, reach, u, v, shape, out float hit) ||
                    hit >= distance) continue;
                target = position; distance = hit;
            }
            return !float.IsPositiveInfinity(distance);
        }

        /// <summary>解释前景格的手采限制；空格中的独立矿床由调用方另行查询，不改变破坏规则。</summary>
        public static string BlockReason(IReadOnlyGrid map, TileCatalog tiles, CellCoord target)
        {
            if (map == null || !map.Read(target).TryGetCell(out var cell)) return "地图尚未就绪";
            if (cell.IsEmpty) return "这里没有可采集的矿床";
            if (!tiles.TryGet(cell.TileId, out var definition)) return "材质没有配置采集规则";
            return definition.Key.ToString() == "bedrock" ? "基岩不能破坏" : "";
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
