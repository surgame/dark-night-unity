using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>独立的连续地面几何探针；采样 10×22 权威单位占地和 2 单位步幅，仅判定无跳跃步行坡道。</summary>
    internal static class TerrainWalkProbe
    {
        internal static bool ReachesRoom(PlayableTerrain map, PlanetDefinition planet)
        {
            var shapes = map.CopyShapes();
            foreach (int direction in new[] { -1, 1 })
            {
                float x = planet.DockX, height = planet.DockHeight;
                for (int step = 0; step < TerrainGenerationSettings.Width * 8; step++)
                {
                    float next = x + direction * 2;
                    float surface = Surface(map, shapes, next, height);
                    if (float.IsNegativeInfinity(surface) || Blocked(map, shapes, next, surface)) break;
                    x = next; height = surface;
                    float column = x / 16 + .5f, row = 40 - height / 16;
                    if (row >= planet.DockRow + 8 && map.Rooms.Any(room =>
                        column >= room.Left + 2 && column <= room.Left + room.Width - 2 &&
                        row >= room.Top + 3 && row <= room.Top + room.Height + 3)) return true;
                }
            }
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
                for (int row = 0; row < TerrainGenerationSettings.Height; row++)
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
