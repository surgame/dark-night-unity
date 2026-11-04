using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.View
{
    /// <summary>
    /// 从本端已知地图读取角色脚下的真实坡面，只供外观贴地。
    /// 查询限定在矩形身体底部附近，要求格边连续且与身体支撑高度相符；
    /// 未知格、断崖、台阶或其他物体支撑时放弃补偿，不修改运动或地图。
    /// </summary>
    internal static class ActorGroundSurface
    {
        private const float HalfWidth = 8;
        private const float ContactTolerance = 2.5f;
        private const float MaximumDrop = HalfWidth * 2 + ContactTolerance;

        internal static bool TrySlope(IReadOnlyGrid map, float x, float height, out float floor)
        {
            floor = height;
            if (map == null ||
                !TryHeight(map, x, height, out float middle) ||
                !TryHeight(map, x - HalfWidth + .001f, height, out float left) ||
                !TryHeight(map, x + HalfWidth - .001f, height, out float right)) return false;
            float highest = Math.Max(middle, Math.Max(left, right));
            float lowest = Math.Min(middle, Math.Min(left, right));
            if (highest - lowest < .01f || highest - lowest > HalfWidth * 2 + .01f ||
                height - middle > HalfWidth + ContactTolerance ||
                Math.Abs(highest - height) > ContactTolerance) return false;
            int firstColumn = (int)Math.Floor((x - HalfWidth + .001f) / PlayableTerrain.CellPixels + .5f);
            int lastColumn = (int)Math.Floor((x + HalfWidth - .001f) / PlayableTerrain.CellPixels + .5f);
            if (firstColumn != lastColumn)
            {
                float boundary = (firstColumn + .5f) * PlayableTerrain.CellPixels;
                if (!TryHeight(map, boundary - .001f, height, out float before) ||
                    !TryHeight(map, boundary + .001f, height, out float after) ||
                    Math.Abs(before - after) > .1f) return false;
            }
            floor = middle;
            return true;
        }

        internal static bool TryHeight(IReadOnlyGrid map, float x, float referenceHeight, out float height)
        {
            height = referenceHeight;
            int column = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
            if (column < 0 || column >= TerrainGenerationSettings.Width) return false;
            int first = Math.Max(0, Row(referenceHeight + ContactTolerance));
            int last = Math.Min(TerrainGenerationSettings.Height - 1, Row(referenceHeight - MaximumDrop) + 1);
            float localX = x / PlayableTerrain.CellPixels - column + .5f;
            for (int row = first; row <= last; row++)
            {
                if (!map.Read(new CellCoord(column, -row)).TryGetCell(out var cell)) return false;
                if (cell.IsEmpty) continue;
                var shape = TerrainShapeGeometry.Decode(cell.Flags);
                float edge = TerrainShapeGeometry.Ceiling(shape) ? 1 : TerrainShapeGeometry.Edge(shape, localX);
                float surface = PlayableTerrain.OriginY + (edge - row - .5f) * PlayableTerrain.CellPixels;
                if (surface > referenceHeight + ContactTolerance || surface < referenceHeight - MaximumDrop) continue;
                height = surface;
                return true;
            }
            return false;
        }

        private static int Row(float height) =>
            (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5f);
    }
}
