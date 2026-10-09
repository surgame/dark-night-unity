using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>URP 回退的区块遮挡几何；整格按连续行合并，坡形使用原权威边界，未知格保守遮挡，不修改碰撞规则。</summary>
    public static class UrpTerrainShadowPaths
    {
        public static List<Vector2[]> Build(RectInt region, Func<int, int, byte> cell)
        {
            var paths = new List<Vector2[]>();
            for (int row = region.yMin; row < region.yMax; row++)
            {
                int run = -1;
                for (int u = region.xMin; u <= region.xMax; u++)
                {
                    byte value = u < region.xMax ? cell(u, row) : (byte)128;
                    bool full = (value & 128) == 0 || (value & 64) != 0 && (value & 15) == 0;
                    if (full) { if (run < 0) run = u; continue; }
                    if (run >= 0)
                    {
                        paths.Add(new[] { new Vector2(run - .5f, -row - .5f), new Vector2(u - .5f, -row - .5f),
                            new Vector2(u - .5f, -row + .5f), new Vector2(run - .5f, -row + .5f) });
                        run = -1;
                    }
                    if ((value & 64) == 0) continue;
                    var shape = (TerrainCellShape)(value & 15);
                    float left = TerrainShapeGeometry.Edge(shape, 0), right = TerrainShapeGeometry.Edge(shape, 1);
                    var points = TerrainShapeGeometry.Ceiling(shape) ? new[]
                    { new Vector2(0, left), new Vector2(1, right), new Vector2(1, 1), new Vector2(0, 1) } : new[]
                    { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, right), new Vector2(0, left) };
                    var unique = new List<Vector2>();
                    foreach (var point in points)
                        if (unique.Count == 0 || point != unique[unique.Count - 1]) unique.Add(point);
                    if (unique.Count > 1 && unique[0] == unique[unique.Count - 1]) unique.RemoveAt(unique.Count - 1);
                    for (int n = 0; n < unique.Count; n++) unique[n] += new Vector2(u - .5f, -row - .5f);
                    if (unique.Count >= 3) paths.Add(unique.ToArray());
                }
            }
            return paths;
        }
    }
}
