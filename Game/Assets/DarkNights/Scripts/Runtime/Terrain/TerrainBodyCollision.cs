using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 按身体覆盖区间查询真实格形状的占据与支撑；线性坡面取区间端点极值，不遗漏格内接缝。
    /// 只读取权威地图，支撑候选必须具有完整身体净空；不拥有角色状态或修改地形。
    /// </summary>
    internal static class TerrainBodyCollision
    {
        private const float Skin = .001f;

        public static bool Blocked(IReadOnlyGrid map, float x, float height, float halfWidth, float bodyHeight)
        {
            float left = x - halfWidth + Skin, right = x + halfWidth - Skin;
            float low = height + Skin, high = height + bodyHeight - Skin;
            int first = Column(left), last = Column(right);
            int top = Row(high), bottom = Row(low);
            if (first < 0 || last >= TerrainGenerationSettings.Width || bottom >= TerrainGenerationSettings.Height) return true;
            for (int row = Math.Max(0, top); row <= bottom; row++)
                for (int column = first; column <= last; column++)
                {
                    if (!map.Read(new CellCoord(column, -row)).TryGetCell(out var cell)) return true;
                    if (cell.IsEmpty) continue;
                    float tileBottom = PlayableTerrain.OriginY - (row + .5f) * PlayableTerrain.CellPixels;
                    float tileTop = tileBottom + PlayableTerrain.CellPixels;
                    var shape = TerrainShapeGeometry.Decode(cell.Flags);
                    Edges(shape, column, left, right, out float minimum, out float maximum);
                    float solidLow = TerrainShapeGeometry.Ceiling(shape) ? tileBottom + minimum : tileBottom;
                    float solidHigh = TerrainShapeGeometry.Ceiling(shape) ? tileTop : tileBottom + maximum;
                    if (high > solidLow + Skin && low < solidHigh - Skin) return true;
                }
            return false;
        }

        public static bool Ground(IReadOnlyGrid map, float x, float high, float low, float halfWidth,
            float bodyHeight, out float height)
        {
            height = float.NegativeInfinity;
            float left = x - halfWidth + Skin, right = x + halfWidth - Skin;
            int first = Math.Max(0, Column(left)), last = Math.Min(TerrainGenerationSettings.Width - 1, Column(right));
            int top = Math.Max(0, Row(high));
            int bottom = Math.Min(TerrainGenerationSettings.Height - 1, Row(low) + 1);
            for (int row = top; row <= bottom; row++)
                for (int column = first; column <= last; column++)
                {
                    if (!map.Read(new CellCoord(column, -row)).TryGetCell(out var cell) || cell.IsEmpty) continue;
                    var shape = TerrainShapeGeometry.Decode(cell.Flags);
                    Edges(shape, column, left, right, out _, out float maximum);
                    float edge = TerrainShapeGeometry.Ceiling(shape) ? PlayableTerrain.CellPixels : maximum;
                    float surface = PlayableTerrain.OriginY - (row + .5f) * PlayableTerrain.CellPixels + edge;
                    if (surface > high + Skin || surface < low - Skin || surface <= height) continue;
                    if (!Blocked(map, x, surface, halfWidth, bodyHeight)) height = surface;
                }
            return !float.IsNegativeInfinity(height);
        }

        private static int Column(float x) => (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
        private static int Row(float height) => (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5f);

        private static void Edges(TerrainCellShape shape, int column, float left, float right,
            out float minimum, out float maximum)
        {
            float origin = (column - .5f) * PlayableTerrain.CellPixels;
            float start = Math.Clamp((left - origin) / PlayableTerrain.CellPixels, 0, 1);
            float end = Math.Clamp((right - origin) / PlayableTerrain.CellPixels, 0, 1);
            float first = TerrainShapeGeometry.Edge(shape, start) * PlayableTerrain.CellPixels;
            float last = TerrainShapeGeometry.Edge(shape, end) * PlayableTerrain.CellPixels;
            minimum = Math.Min(first, last); maximum = Math.Max(first, last);
        }
    }
}
