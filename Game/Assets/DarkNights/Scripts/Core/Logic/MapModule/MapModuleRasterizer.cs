using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.MapModule;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Core.Logic.MapModule
{
    /// <summary>
    /// 将模块实岩精确编译为当前320×192格及12坡形；仅生成初始冻结数据，不运行WFC或修改活动地图。
    /// 逐格裁剪三角片，拒绝正面积重叠及无法表达的形状，不用采样掩码、自动填洞或阶梯近似。
    /// </summary>
    public static class MapModuleRasterizer
    {
        public static PlayableTerrain Build(string worldId, string sourceId, IReadOnlyList<MapModulePolygon> solids)
        {
            if (solids == null || solids.Count == 0 || solids.Count > 256)
                throw new InvalidOperationException("模块实体数量必须为1至256。");
            int width = TerrainGenerationSettings.Width, height = TerrainGenerationSettings.Height;
            var pieces = new Dictionary<int, List<MapModuleFragment>>();
            foreach (var solid in solids)
                foreach (var triangle in MapModuleGeometry.Triangulate(solid.Vertices))
                {
                    int minX = (int)Math.Floor(triangle.Min(p => p.X));
                    int maxX = (int)Math.Ceiling(triangle.Max(p => p.X));
                    int minY = (int)Math.Floor(triangle.Min(p => p.Y));
                    int maxY = (int)Math.Ceiling(triangle.Max(p => p.Y));
                    if (minX < 0 || maxX > width || minY < PlayableTerrain.CampRow - height + 1 || maxY > PlayableTerrain.CampRow)
                        throw new InvalidOperationException("模块轮廓超出固定地图范围：" + solid.Id);
                    for (int x = minX; x < maxX; x++) for (int y = minY; y < maxY; y++)
                    {
                        var cell = new[] { V(x, y), V(x + 1, y), V(x + 1, y + 1), V(x, y + 1) };
                        var clip = MapModuleGeometry.Clip(triangle, cell);
                        if (Math.Abs(MapModuleGeometry.SignedArea(clip)) < MapModuleGeometry.Epsilon) continue;
                        int index = (PlayableTerrain.CampRow - 1 - y) * width + x;
                        if (!pieces.TryGetValue(index, out var list)) pieces.Add(index, list = new List<MapModuleFragment>());
                        list.Add(new MapModuleFragment(solid.Material, clip.Select(p => V(p.X - x, p.Y - y)).ToArray()));
                    }
                }
            var materials = new byte[width * height]; var shapes = new byte[materials.Length];
            var protection = new bool[materials.Length];
            foreach (var cell in pieces)
            {
                double area = 0;
                for (int i = 0; i < cell.Value.Count; i++)
                {
                    var fragment = cell.Value[i];
                    if (fragment.Material != cell.Value[0].Material) throw Failure(cell.Key, "同格存在不同材料。");
                    area += Math.Abs(MapModuleGeometry.SignedArea(fragment.Vertices));
                    for (int j = 0; j < i; j++)
                        if (Math.Abs(MapModuleGeometry.SignedArea(MapModuleGeometry.Clip(
                            fragment.Vertices, cell.Value[j].Vertices))) > MapModuleGeometry.Epsilon)
                            throw Failure(cell.Key, "模块实岩重叠。");
                }
                int matched = -1;
                for (int shape = 0; shape <= 12; shape++)
                {
                    var polygon = Shape((TerrainCellShape)shape);
                    if (Math.Abs(Math.Abs(MapModuleGeometry.SignedArea(polygon)) - area) > MapModuleGeometry.Epsilon) continue;
                    double covered = cell.Value.Sum(fragment => Math.Abs(MapModuleGeometry.SignedArea(
                        MapModuleGeometry.Clip(fragment.Vertices, polygon))));
                    if (Math.Abs(covered - area) < MapModuleGeometry.Epsilon) { matched = shape; break; }
                }
                if (matched < 0) throw Failure(cell.Key, "轮廓不能精确映射当前整格／12坡形。");
                materials[cell.Key] = cell.Value[0].Material; shapes[cell.Key] = (byte)matched;
            }
            for (int x = 0; x < width; x++)
            {
                int index = (height - 1) * width + x;
                materials[index] = 8; shapes[index] = 0; protection[index] = true;
            }
            return new PlayableTerrain(worldId, sourceId, materials, protection, new bool[materials.Length],
                Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), shapes, true,
                new BackgroundBakeDescriptor(worldId, sourceId, materials, shapes));
        }

        public static MapModuleVertex[] Shape(TerrainCellShape shape)
        {
            if (shape == TerrainCellShape.Full) return new[] { V(0, 0), V(1, 0), V(1, 1), V(0, 1) };
            double left = TerrainShapeGeometry.Edge(shape, 0), right = TerrainShapeGeometry.Edge(shape, 1);
            return TerrainShapeGeometry.Ceiling(shape) ?
                new[] { V(0, left), V(1, right), V(1, 1), V(0, 1) } :
                new[] { V(0, 0), V(1, 0), V(1, right), V(0, left) };
        }
        private static MapModuleVertex V(double x, double y) => new MapModuleVertex(x, y);
        private static Exception Failure(int index, string reason) => new InvalidOperationException(
            "格(" + index % TerrainGenerationSettings.Width + "," + index / TerrainGenerationSettings.Width + ")：" + reason);
    }
}
