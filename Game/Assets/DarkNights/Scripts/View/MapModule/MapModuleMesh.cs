using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.MapModule;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.MapModule
{
    /// <summary>从现有格及坡形生成扁平白模网格；仅为表现派生，不提供碰撞或修改权威格，调用者负责释放网格。</summary>
    public static class MapModuleMesh
    {
        public static Mesh Build(byte[] materials, byte[] shapes, Color32 color)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>(); var colors = new List<Color32>();
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == 0) continue;
                int x = i % TerrainGenerationSettings.Width, y = PlayableTerrain.CampRow - 1 - i / TerrainGenerationSettings.Width;
                var polygon = MapModuleRasterizer.Shape((TerrainCellShape)shapes[i]); int first = vertices.Count;
                foreach (var point in polygon)
                {
                    vertices.Add(new Vector3((float)(x - .5 + point.X) * MapModuleAuthoring.CellUnit,
                        (float)(y + point.Y) * MapModuleAuthoring.CellUnit, 0));
                    colors.Add(color);
                }
                for (int p = 1; p < polygon.Length - 1; p++)
                { indices.Add(first); indices.Add(first + p + 1); indices.Add(first + p); }
            }
            var mesh = new Mesh { name = "MapModule fixed terrain", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
