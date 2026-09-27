using System;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tests
{
    /// <summary>测试专用冻结地图路径探针；只挑选可连续步行的洞室支撑点，不拥有或修改角色及地图状态。</summary>
    internal static class JourneyWalkwayRoute
    {
        internal static bool Find(PlayableTerrain map, PlanetDefinition planet, out float targetX, out float targetHeight)
        {
            byte[] shapes = map.CopyShapes();
            targetX = targetHeight = float.NaN;
            float shortest = float.PositiveInfinity;
            foreach (int direction in new[] { -1, 1 })
            {
                float x = planet.DockX, height = planet.DockHeight;
                int roomSteps = 0;
                for (int step = 0; step < TerrainGenerationSettings.Width * 8; step++)
                {
                    float next = x + direction * 2;
                    float surface = Surface(map, shapes, next, height);
                    if (float.IsNegativeInfinity(surface) || Blocked(map, shapes, next, surface)) break;
                    x = next; height = surface;
                    roomSteps = InRoom(map, planet, x, height) ? roomSteps + 1 : 0;
                    if (roomSteps < 3) continue;
                    if (Math.Abs(x - planet.DockX) < shortest)
                    { shortest = Math.Abs(x - planet.DockX); targetX = x; targetHeight = height; }
                    break;
                }
            }
            return !float.IsNaN(targetX);
        }

        internal static bool InRoom(PlayableTerrain map, PlanetDefinition planet, float x, float height)
        {
            float column = x / 16 + .5f, row = 40 - height / 16;
            if (row < planet.DockRow + 8) return false;
            foreach (var room in map.Rooms)
                if (column >= room.Left + 2 && column <= room.Left + room.Width - 2 &&
                    row >= room.Top + 3 && row <= room.Top + room.Height + 3) return true;
            return false;
        }

        private static float Surface(PlayableTerrain map, byte[] shapes, float x, float current)
        {
            float highest = float.NegativeInfinity;
            foreach (float foot in new[] { -5f, 0f, 5f })
            {
                float px = x + foot;
                int column = (int)Math.Floor(px / 16 + .5f);
                if (column < 0 || column >= TerrainGenerationSettings.Width) continue;
                int first = Math.Max(0, (int)Math.Floor((PlayableTerrain.OriginY - current - 2.05f) / 16 + .5f));
                int last = Math.Min(TerrainGenerationSettings.Height - 1,
                    (int)Math.Floor((PlayableTerrain.OriginY - current + 2.05f) / 16 + .5f) + 1);
                for (int row = first; row <= last; row++)
                {
                    if (map.Material(column, row) == 0) continue;
                    var shape = (TerrainCellShape)shapes[row * TerrainGenerationSettings.Width + column];
                    float edge = TerrainShapeGeometry.Ceiling(shape) ? 1 : TerrainShapeGeometry.Edge(shape, px / 16 - column + .5f);
                    float top = PlayableTerrain.OriginY + (-row - .5f + edge) * 16;
                    if (top >= current - 2.051f && top <= current + 2.051f) highest = Math.Max(highest, top);
                }
            }
            return highest;
        }

        private static bool Blocked(PlayableTerrain map, byte[] shapes, float x, float height)
        {
            foreach (float side in new[] { -5f, 5f })
                for (float head = 1; head <= 22; head += 7)
                {
                    float px = x + side, py = height + head;
                    int column = (int)Math.Floor(px / 16 + .5f);
                    int row = (int)Math.Floor((PlayableTerrain.OriginY - py) / 16 + .5f);
                    if (column < 0 || column >= TerrainGenerationSettings.Width || row >= TerrainGenerationSettings.Height) return true;
                    if (row < 0 || map.Material(column, row) == 0) continue;
                    if (TerrainShapeGeometry.Contains((TerrainCellShape)shapes[row * TerrainGenerationSettings.Width + column],
                        px / 16 - column + .5f, (py - PlayableTerrain.OriginY) / 16 + row + .5f)) return true;
                }
            return false;
        }
    }
}
